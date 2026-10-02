using Entities.Web.Testing.Infrastructure;
using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Courses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;
using Regira.IO.Utilities;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

public class CourseAttachmentsControllerTests : IClassFixture<ContosoApiFactory>, IDisposable
{
    Department[] Departments { get; }
    Course[] Courses { get; }

    private readonly ContosoContext _dbContext;
    private readonly ContosoApiFactory _factory;
    public CourseAttachmentsControllerTests(ContosoApiFactory factory)
    {
        _factory = factory;
        Directory.CreateDirectory(_factory.AttachmentsDirectory);

        _dbContext = factory.CreateDbContext();
        _dbContext.Database.EnsureCreated();

        Departments = Enumerable.Range(1, 5).Select((_, i) => new Department { Title = $"Department #{i}", Budget = i * 1000, StartDate = DateTime.Today.AddDays(i * 3) }).ToArray();
        Courses = Enumerable.Range(1, 50).Select((_, i) => new Course { Title = $"Course #{i}", Credits = Random.Shared.Next(1, 5), Department = Departments[Random.Shared.Next(0, 4)] }).ToArray();

        _dbContext.Departments.AddRange(Departments);
        _dbContext.Courses.AddRange(Courses);
        _dbContext.SaveChanges();
    }


    [Fact]
    public async Task Empty_Get()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/courses/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();
        Assert.NotNull(result!.Items);
        Assert.Empty(result.Items);
    }
    [Fact]
    public async Task Insert_And_Get_Details()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var fileTextContent = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(fileTextContent);
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);
        //Assert.Single(Directory.GetFiles(_factory.AttachmentsDirectory, "", SearchOption.AllDirectories));
        var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
        Assert.NotNull(saveResult);
        Assert.NotNull(saveResult.Item);
        Assert.True(saveResult.IsNew);

        var detailsResponse = await client.GetAsync($"/courses/attachments/{saveResult.Item.Id}");
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<EntityAttachmentDto>>();
        Assert.NotNull(detailsResult!.Item);
        Assert.Equal(saveResult.Item.Id, detailsResult.Item.Id);
        Assert.Equal(attachmentFileName, detailsResult.Item.Attachment!.FileName);
        Assert.Equal(fileStream.Length, detailsResult.Item.Attachment.Length);
    }
    [Fact]
    public async Task Download_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var fileTextContent = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(fileTextContent);
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        using var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        inputResponse.EnsureSuccessStatusCode();
        var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
        Assert.NotNull(saveResult?.Item.Uri);

        //using var downloadResponse = await client.GetAsync($"/courses/{courseId}/files/{saveResult!.Item.Attachment!.FileName}");
        using var downloadResponse = await client.GetAsync(saveResult.Item.Uri!);
        await using var downloadStream = await downloadResponse.Content.ReadAsStreamAsync();
        Assert.NotNull(downloadStream);
        Assert.Equal(fileStream.Length, downloadStream.Length);
        Assert.Equal(FileUtility.GetString(downloadStream), fileTextContent);
    }
    [Fact]
    public async Task Insert_And_Get_List()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var count = 15;
        for (var i = 1; i <= count; i++)
        {
            var fileTextContent = $"This is the {i}th testmessage for attachments";
            var inputContent = new MultipartFormDataContent
            {
                {new StreamContent(FileUtility.GetStreamFromString(fileTextContent)), "file", attachmentFileName}
            };
            var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
            inputResponse.EnsureSuccessStatusCode();

            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
            Assert.NotNull(saveResult);
            Assert.NotNull(saveResult.Item);
            Assert.True(saveResult.IsNew);
            Assert.Equal(courseId, saveResult.Item.ObjectId);
            //Assert.Equal(i == 1
            //        ? attachmentFileName
            //        : attachmentFileName.Replace(".txt", $"-({i}).txt"),// NextAvailableFileName
            //    saveResult.Item.Attachment!.FileName
            //);
        }

        var listResponse = await client.GetAsync($"/courses/{courseId}/attachments");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();
        Assert.Equal(count, listResult!.Items.Count);
        foreach (var item in listResult.Items)
        {
            Assert.Equal(item.ObjectId, item.ObjectId);
            Assert.True(item.Id > 0);
            Assert.True(item.Attachment!.Id > 0);
        }
        //Assert.Equal(count, Directory.GetFiles(_factory.AttachmentsDirectory, "", SearchOption.AllDirectories).Length);
    }
    [Fact]
    public async Task Insert_And_Force_404()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var fileTextContent = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(fileTextContent);
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var detailsResponse99 = await client.GetAsync("/courses/attachments/999");
        Assert.False(detailsResponse99.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse99.StatusCode);

        var detailsResponse0 = await client.GetAsync("/courses/attachments/0");
        Assert.False(detailsResponse0.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse0.StatusCode);
    }

    [Fact]
    public async Task Reordering_Parent_Attachments_Array_Persists_SortOrder_By_Position()
    {
        // Attachment order travels by ARRAY POSITION: HasAttachments wires SetSortOrder() over the incoming
        // collection on every parent save (the input DTO deliberately carries no SortOrder).
        using var client = _factory.CreateClient();

        var courseId = 3;
        for (var i = 1; i <= 3; i++)
        {
            var inputContent = new MultipartFormDataContent{
                { new StreamContent(FileUtility.GetStreamFromString($"attachment #{i}")), "file", $"test-attachment{i}.txt" }
            };
            var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
            inputResponse.EnsureSuccessStatusCode();
        }

        var detailsResponse = await client.GetAsync($"/courses/{courseId}");
        detailsResponse.EnsureSuccessStatusCode();
        var details = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var reversed = details!.Item.Attachments!
            .OrderByDescending(x => x.Id)
            .Select(x => new CourseAttachmentInputDto { Id = x.Id, ObjectId = x.ObjectId, AttachmentId = x.AttachmentId, Description = x.Description })
            .ToList();

        var courseInput = new CourseInputDto
        {
            Id = details.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = reversed,
        };
        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}", courseInput);
        updateResponse.EnsureSuccessStatusCode();

        var detailsResponse2 = await client.GetAsync($"/courses/{courseId}");
        var details2 = await detailsResponse2.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var byId = details2!.Item.Attachments!.ToDictionary(x => x.Id);
        for (var pos = 0; pos < reversed.Count; pos++)
        {
            Assert.Equal(pos, byId[reversed[pos].Id].SortOrder);
        }
        // the Uri after-mapper pairs by Id — each row must carry its OWN download link, whatever the order
        foreach (var dto in details2.Item.Attachments!)
        {
            Assert.Contains($"/files/{dto.Attachment!.FileName}", dto.Uri);
        }
    }

    [Fact]
    public async Task Attachment_List_Is_Ordered_By_SortOrder()
    {
        // The per-owner attachment services are registered by HasAttachments(), so a consumer has no .For<>()
        // of their own to hang a SortBy on. Without a default the list came back unordered while paging still
        // applied a Take — an EF row-limiting-without-OrderBy warning on every request, unfixable from
        // consumer code, and a list whose order was whatever the provider happened to return.
        using var client = _factory.CreateClient();

        var courseId = 7;
        for (var i = 1; i <= 3; i++)
        {
            var inputContent = new MultipartFormDataContent{
                { new StreamContent(FileUtility.GetStreamFromString($"attachment #{i}")), "file", $"ordered-attachment{i}.txt" }
            };
            (await client.PostAsync($"/courses/{courseId}/files", inputContent)).EnsureSuccessStatusCode();
        }

        var detailsResponse = await client.GetAsync($"/courses/{courseId}");
        var details = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var reversed = details!.Item.Attachments!
            .OrderByDescending(x => x.SortOrder)
            .Select(x => new CourseAttachmentInputDto { Id = x.Id, ObjectId = x.ObjectId, AttachmentId = x.AttachmentId, Description = x.Description })
            .ToList();

        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}", new CourseInputDto
        {
            Id = details.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = reversed,
        });
        updateResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/courses/{courseId}/attachments");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();

        var sortOrders = list!.Items!.Select(x => x.SortOrder).ToList();
        Assert.Equal(sortOrders.OrderBy(x => x), sortOrders);
        Assert.Equal(reversed.Select(x => x.Id), list.Items!.Select(x => x.Id));
    }

    [Fact]
    public async Task Uploaded_Attachments_Have_A_Stable_Order_Before_The_Owner_Is_Ever_Saved()
    {
        // Only the owner-save path stamps SortOrder (HasAttachments passes SetSortOrder() as its prepareFunc),
        // so rows that arrive by upload alone all carry 0. Ordering on SortOrder by itself is then not a total
        // order and the provider picks the tiebreak — which is exactly the unstable paging the row-limiting
        // warning used to flag. Id has to break the tie.
        using var client = _factory.CreateClient();

        var courseId = 11;
        for (var i = 1; i <= 4; i++)
        {
            var inputContent = new MultipartFormDataContent{
                { new StreamContent(FileUtility.GetStreamFromString($"attachment #{i}")), "file", $"unsaved-owner{i}.txt" }
            };
            (await client.PostAsync($"/courses/{courseId}/files", inputContent)).EnsureSuccessStatusCode();
        }

        var listResponse = await client.GetAsync($"/courses/{courseId}/attachments");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();
        var ids = list!.Items!.Select(x => x.Id).ToList();

        Assert.Equal(4, ids.Count);
        Assert.All(list.Items!, x => Assert.Equal(0, x.SortOrder)); // pins the premise: nothing stamped them
        Assert.Equal(ids.OrderBy(x => x), ids);                     // …and the order is still deterministic
    }

    [Fact]
    public async Task Upload_To_Nonexistent_Owner_Returns_409_Conflict()
    {
        // Pins the framework assumption behind [EntityConstraintConflict]: the attribute is declared on the
        // generic EntityAttachmentControllerBase<,,> and must reach this derived controller through MVC's
        // inherited-attribute collection — nothing at compile time verifies that. If the inheritance path
        // broke, every attachment constraint violation would silently regress to a 500.
        using var client = _factory.CreateClient();

        var inputContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString("This is a testmessage for an attachment")), "file", "test-attachment.txt" }
        };
        // nonexistent course → ObjectId FK constraint violation at SaveChanges
        var uploadResponse = await client.PostAsync("/courses/999999/files", inputContent);

        Assert.Equal(HttpStatusCode.Conflict, uploadResponse.StatusCode);
        var body = await uploadResponse.Content.ReadAsStringAsync();
        Assert.Contains(EntityConstraintException.ClientMessage, body);
        Assert.DoesNotContain("FOREIGN KEY", body); // the provider's constraint detail stays server-side
    }

    [Fact]
    public async Task Update_Route_Is_Authoritative()
    {
        // the body must neither retarget another row nor reparent it, and a mismatched parent route 400s
        using var client = _factory.CreateClient();

        var courseId = 3;
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString("This is a testmessage for an attachment")), "file", "test-attachment.txt" }
        };
        var insertResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        insertResponse.EnsureSuccessStatusCode();
        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        var insertedItem = insertResult!.Item;

        // wrong parent in the route → 400, even though the body matches the row
        var itemToUpdate = new CourseAttachmentInputDto
        {
            Id = insertedItem.Id,
            ObjectId = insertedItem.ObjectId,
            AttachmentId = insertedItem.AttachmentId,
            Description = "via wrong parent",
        };
        var wrongParentResponse = await client.PutAsJsonAsync($"/courses/4/attachments/{insertedItem.Id}", itemToUpdate);
        Assert.Equal(HttpStatusCode.BadRequest, wrongParentResponse.StatusCode);
        var problem = await wrongParentResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["Not a link of this owner."], problem!.Errors["objectId"]);

        // body pointing at a different row/parent is ignored — the route's row is the one updated
        var retargetingBody = new CourseAttachmentInputDto
        {
            Id = 999999,
            ObjectId = 999999,
            AttachmentId = insertedItem.AttachmentId,
            Description = "route wins",
        };
        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{insertedItem.Id}", retargetingBody);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        Assert.Equal(insertedItem.Id, updated!.Item.Id);
        Assert.Equal(courseId, updated.Item.ObjectId);
        Assert.Equal("route wins", updated.Item.Description);
    }

    // FileName is the client's value and may carry a virtual folder. Both upload routes must keep it intact,
    // and the catch-all download route must resolve the full path — a single-segment token never would.
    [Theory]
    [InlineData(true)]  // POST {objectId}/files
    [InlineData(false)] // PUT {courseId} with a new attachment in the collection
    public async Task Uploading_A_Foldered_Name_Keeps_The_Virtual_Path_And_Stays_Downloadable(bool directUpload)
    {
        using var client = _factory.CreateClient();

        var courseId = 2;
        var bareName = $"{(directUpload ? "direct" : "nested")}-scan.txt";
        var suppliedName = $"archive/2026/{bareName}";

        if (directUpload)
        {
            await using var fileStream = FileUtility.GetStreamFromString(bareName);
            var content = new MultipartFormDataContent { { new StreamContent(fileStream), "file", suppliedName } };
            (await client.PostAsync($"/courses/{courseId}/files", content)).EnsureSuccessStatusCode();
        }
        else
        {
            var details = await (await client.GetAsync($"/courses/{courseId}")).Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
            var courseInput = new CourseInputDto
            {
                Id = details!.Item.Id,
                Title = details.Item.Title,
                DepartmentId = details.Item.DepartmentId,
                Credits = details.Item.Credits,
                Attachments = [new CourseAttachmentInputDto
                {
                    ObjectId = courseId,
                    NewFileName = suppliedName,
                    NewBytes = FileUtility.GetBytesFromString(bareName)
                }]
            };
            (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();
        }

        // the folder survives the round-trip...
        var saved = await (await client.GetAsync($"/courses/{courseId}")).Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var stored = Assert.Single(saved!.Item.Attachments!, a => a.Attachment!.FileName == suppliedName);

        // ...the multi-segment name downloads...
        var download = await client.GetAsync($"/courses/{courseId}/files/{suppliedName}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bareName, await download.Content.ReadAsStringAsync());

        // ...including through the resolver-generated Uri. LinkGenerator percent-encodes the separators
        // (…/files/archive%2F2026%2Fscan.txt), so following that link is the case the %2F branch in GetFile
        // exists for — assert the round-trip, not the spelling.
        Assert.NotNull(stored.Uri);
        Assert.Contains("%2F", stored.Uri);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(stored.Uri)).StatusCode);

        // ...and none of the client's folders reached storage
        var entityFolder = Path.Combine(_factory.AttachmentsDirectory, "Course", "Attachments", courseId.ToString());
        Assert.False(Directory.Exists(Path.Combine(entityFolder, "archive")), "the virtual folder must stay virtual");
    }

    // A store or a link serves a file with the type it holds, so the upload is typed by its file name — the thing an app
    // checks — and a .png declared text/html is not served as a page.
    [Fact]
    public async Task An_Upload_Is_Typed_By_Its_File_Name_Not_By_The_Type_The_Client_Declared()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var file = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        var content = new MultipartFormDataContent { { file, "file", "declared-html.png" } };
        (await client.PostAsync($"/courses/{courseId}/files", content)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/declared-html.png");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    // NewContentType is ignored: a client that sends one still gets the type the file name gives
    [Fact]
    public async Task A_Content_Type_Sent_With_New_Bytes_Is_Ignored()
    {
        using var client = _factory.CreateClient();

        var courseId = 5;
        var details = await (await client.GetAsync($"/courses/{courseId}")).Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
#pragma warning disable CS0618 // the obsolete member is the point of the test
        var attachment = new CourseAttachmentInputDto
        {
            ObjectId = courseId,
            NewFileName = "declared-in-json.png",
            NewBytes = "<script>alert(1)</script>"u8.ToArray(),
            NewContentType = "text/html"
        };
#pragma warning restore CS0618
        var courseInput = new CourseInputDto
        {
            Id = details!.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = [attachment]
        };
        (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/declared-in-json.png");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    // The attachment's own metadata route changes the file the way the owner's save does: a new name retypes it,
    // new bytes replace what is stored.
    [Fact]
    public async Task Renaming_Through_The_Metadata_Route_Retypes_The_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var name = $"renamed-{Guid.NewGuid():N}";
        var content = new MultipartFormDataContent { { new ByteArrayContent([137, 80, 78, 71]), "file", $"{name}.png" } };
        var uploaded = await (await client.PostAsync($"/courses/{courseId}/files", content)).Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var rename = new CourseAttachmentInputDto
        {
            Id = uploaded!.Item.Id,
            ObjectId = courseId,
            AttachmentId = uploaded.Item.AttachmentId,
            NewFileName = $"{name}.pdf"
        };
        (await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{uploaded.Item.Id}", rename)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/{name}.pdf");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/courses/{courseId}/files/{name}.png")).StatusCode);
    }

    [Fact]
    public async Task Renaming_Through_The_Owner_Retypes_The_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 4;
        var name = $"renamed-by-owner-{Guid.NewGuid():N}";
        var content = new MultipartFormDataContent { { new ByteArrayContent([137, 80, 78, 71]), "file", $"{name}.png" } };
        var uploaded = await (await client.PostAsync($"/courses/{courseId}/files", content)).Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var details = await (await client.GetAsync($"/courses/{courseId}")).Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var courseInput = new CourseInputDto
        {
            Id = details!.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = details.Item.Attachments!.Select(a => new CourseAttachmentInputDto
            {
                Id = a.Id,
                ObjectId = a.ObjectId,
                AttachmentId = a.AttachmentId,
                NewFileName = a.Id == uploaded!.Item.Id ? $"{name}.pdf" : null
            }).ToList()
        };
        (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/{name}.pdf");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
    }

    // A validator judges a rename: the item carries the new name and the original the stored one, and a refusal leaves
    // the stored file as it was. The rule compares the types the names give, not their extensions, so a rename between
    // spellings of one type (.JPEG to .jpg) passes.
    [Fact]
    public async Task A_Validator_Judges_A_Rename_Through_The_Metadata_Route()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddValidator<CourseAttachment>(ctx =>
            {
                var stored = ctx.Original?.Attachment?.FileName;
                var renamed = ctx.Item.Attachment?.FileName;
                if (stored != null && renamed != null
                    && ContentTypeUtility.GetContentType(stored) != ContentTypeUtility.GetContentType(renamed))
                {
                    ctx.AddError(nameof(CourseAttachmentInputDto.NewFileName), "A rename keeps the file's type.");
                }
            })));
        using var client = app.CreateClient();

        var courseId = 6;
        var name = $"validated-{Guid.NewGuid():N}";
        var content = new MultipartFormDataContent { { new ByteArrayContent([255, 216, 255]), "file", $"{name}.JPEG" } };
        var uploaded = await (await client.PostAsync($"/courses/{courseId}/files", content)).Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        CourseAttachmentInputDto Rename(string fileName) => new()
        {
            Id = uploaded!.Item.Id,
            ObjectId = courseId,
            AttachmentId = uploaded.Item.AttachmentId,
            NewFileName = fileName
        };

        var refused = await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{uploaded!.Item.Id}", Rename($"{name}.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var unchanged = await client.GetAsync($"/courses/{courseId}/files/{name}.JPEG");
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal("image/jpeg", unchanged.Content.Headers.ContentType?.MediaType);

        (await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{uploaded.Item.Id}", Rename($"{name}.jpg"))).EnsureSuccessStatusCode();
        var renamed = await client.GetAsync($"/courses/{courseId}/files/{name}.jpg");
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("image/jpeg", renamed.Content.Headers.ContentType?.MediaType);
    }

    // New bytes replace the stored file, leaving one file in storage: through the link's metadata route and the owner's
    // save they go under a key of their own and the stored file goes once the save is committed, and the file route
    // stores a new attachment and removes the old one.
    [Theory]
    [InlineData("metadata")]
    [InlineData("owner")]
    [InlineData("file")]
    public async Task New_Bytes_Replace_The_File(string route)
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        // letters only, so the storage key keeps it as written
        var stem = "replaced-" + string.Concat(Guid.NewGuid().ToString("N").Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));
        var name = $"{stem}.txt";
        var uploaded = await Upload(client, courseId, name, "first version");
        var storedPath = StoredPath(uploaded.AttachmentId);
        var newBytes = FileUtility.GetBytesFromString("second version");

        var response = route switch
        {
            "metadata" => await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{uploaded.Id}",
                new CourseAttachmentInputDto { Id = uploaded.Id, ObjectId = courseId, AttachmentId = uploaded.AttachmentId, NewBytes = newBytes }),
            "owner" => await SaveOwnerWithNewBytes(client, courseId, uploaded.Id, newBytes),
            _ => await client.PutAsync($"/courses/{courseId}/files/{uploaded.Id}", new MultipartFormDataContent { { new ByteArrayContent(newBytes), "file", name } })
        };
        response.EnsureSuccessStatusCode();

        Assert.Equal("second version", await client.GetStringAsync($"/courses/{courseId}/files/{name}"));
        Assert.Single(Directory.GetFiles(_factory.AttachmentsDirectory, $"*{stem}*", SearchOption.AllDirectories));
        if (route != "file")
        {
            Assert.NotEqual(storedPath, StoredPath(uploaded.AttachmentId));
            Assert.Equal(newBytes.Length, _dbContext.Attachments.AsNoTracking().Single(x => x.Id == uploaded.AttachmentId).Length);
        }
    }

    // A save the database refuses leaves the stored file as it was, as it leaves the row: the new bytes went under a key
    // of their own, and the stored file is removed only once a save is committed.
    [Fact]
    public async Task A_Refused_Save_Leaves_The_Stored_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 10;
        var stem = "refused-" + string.Concat(Guid.NewGuid().ToString("N").Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));
        var name = $"{stem}.txt";
        var uploaded = await Upload(client, courseId, name, "first version");
        var storedPath = StoredPath(uploaded.AttachmentId);
        var theirs = await Upload(client, 9, $"theirs-{Guid.NewGuid():N}.txt", "their file");

        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var attachments = details!.Item.Attachments!.Select(a => new CourseAttachmentInputDto
        {
            Id = a.Id,
            ObjectId = a.ObjectId,
            AttachmentId = a.AttachmentId,
            NewBytes = a.Id == uploaded.Id ? FileUtility.GetBytesFromString("second version") : null
        }).ToList();
        // a new link naming another owner's attachment fails its foreign key, so the whole save is refused
        attachments.Add(new CourseAttachmentInputDto { ObjectId = courseId, AttachmentId = theirs.AttachmentId });
        var courseInput = new CourseInputDto
        {
            Id = details.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = attachments
        };

        var response = await client.PutAsJsonAsync($"/courses/{courseId}", courseInput);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("first version", await client.GetStringAsync($"/courses/{courseId}/files/{name}"));
        Assert.Equal(storedPath, StoredPath(uploaded.AttachmentId));
        // nor the file it wrote for the new bytes
        Assert.Single(Directory.GetFiles(_factory.AttachmentsDirectory, $"*{stem}*", SearchOption.AllDirectories));
    }

    // A save the database refuses keeps the file of a link it would have deleted: the file goes once a save is committed.
    [Fact]
    public async Task A_Refused_Save_Keeps_The_File_Of_A_Link_It_Would_Delete()
    {
        using var client = _factory.CreateClient();

        var courseId = 11;
        var name = $"dropped-{Guid.NewGuid():N}.txt";
        var dropped = await Upload(client, courseId, name, "kept content");
        var theirs = await Upload(client, 9, $"theirs-{Guid.NewGuid():N}.txt", "their file");

        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var attachments = details!.Item.Attachments!
            .Where(a => a.Id != dropped.Id)
            .Select(a => new CourseAttachmentInputDto { Id = a.Id, ObjectId = a.ObjectId, AttachmentId = a.AttachmentId })
            .ToList();
        // the link left out is deleted with its attachment; the new one fails its foreign key, so the save is refused
        attachments.Add(new CourseAttachmentInputDto { ObjectId = courseId, AttachmentId = theirs.AttachmentId });
        var courseInput = new CourseInputDto
        {
            Id = details.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = attachments
        };

        var response = await client.PutAsJsonAsync($"/courses/{courseId}", courseInput);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("kept content", await client.GetStringAsync($"/courses/{courseId}/files/{name}"));
    }

    // New bytes saved through the attachment's own service, in a transaction rolled back, leave the stored file as it was.
    [Fact]
    public async Task A_Rolled_Back_Replace_Through_The_Attachment_Service_Leaves_The_Stored_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 12;
        var name = $"rolled-back-{Guid.NewGuid():N}.txt";
        var uploaded = await Upload(client, courseId, name, "first version");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContosoContext>();
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Attachment, int>>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var attachment = await service.Details(uploaded.AttachmentId);
            attachment!.Bytes = FileUtility.GetBytesFromString("second version");
            await service.Save(attachment);
            await service.SaveChanges();
            await transaction.RollbackAsync();
        }

        Assert.Equal("first version", await client.GetStringAsync($"/courses/{courseId}/files/{name}"));
    }

    private string? StoredPath(int attachmentId)
        => _dbContext.Attachments.AsNoTracking().Single(x => x.Id == attachmentId).Path;

    private static async Task<HttpResponseMessage> SaveOwnerWithNewBytes(HttpClient client, int courseId, int linkId, byte[] newBytes)
    {
        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var courseInput = new CourseInputDto
        {
            Id = details!.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = details.Item.Attachments!.Select(a => new CourseAttachmentInputDto
            {
                Id = a.Id,
                ObjectId = a.ObjectId,
                AttachmentId = a.AttachmentId,
                NewBytes = a.Id == linkId ? newBytes : null
            }).ToList()
        };
        return await client.PutAsJsonAsync($"/courses/{courseId}", courseInput);
    }

    // The body's AttachmentId cannot point a link at another attachment, another owner's file among them: the link keeps
    // the one it has, and a rename sent with it renames that one.
    [Fact]
    public async Task The_Metadata_Route_Keeps_A_Link_On_Its_Attachment()
    {
        using var client = _factory.CreateClient();

        var mine = await Upload(client, 3, $"mine-{Guid.NewGuid():N}.txt", "my file");
        var theirsName = $"theirs-{Guid.NewGuid():N}.txt";
        var theirs = await Upload(client, 4, theirsName, "their file");
        var renamed = $"mine-renamed-{Guid.NewGuid():N}.txt";

        var retarget = new CourseAttachmentInputDto { Id = mine.Id, ObjectId = 3, AttachmentId = theirs.AttachmentId, NewFileName = renamed };
        (await client.PutAsJsonAsync($"/courses/3/attachments/{mine.Id}", retarget)).EnsureSuccessStatusCode();

        var link = await client.GetFromJsonAsync<DetailsResult<CourseAttachmentDto>>($"/courses/attachments/{mine.Id}");
        Assert.Equal(mine.AttachmentId, link!.Item.AttachmentId);
        Assert.Equal("my file", await client.GetStringAsync($"/courses/3/files/{renamed}"));
        Assert.Equal("their file", await client.GetStringAsync($"/courses/4/files/{theirsName}"));
    }

    // The owner's save holds a kept link to the same: the AttachmentId its body sends for the link is not followed.
    [Fact]
    public async Task The_Owner_Keeps_A_Link_On_Its_Attachment()
    {
        using var client = _factory.CreateClient();

        var courseId = 5;
        var mineName = $"mine-{Guid.NewGuid():N}.txt";
        var mine = await Upload(client, courseId, mineName, "my file");
        var theirs = await Upload(client, 6, $"theirs-{Guid.NewGuid():N}.txt", "their file");

        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var courseInput = new CourseInputDto
        {
            Id = details!.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = details.Item.Attachments!.Select(a => new CourseAttachmentInputDto
            {
                Id = a.Id,
                ObjectId = a.ObjectId,
                AttachmentId = a.Id == mine.Id ? theirs.AttachmentId : a.AttachmentId
            }).ToList()
        };
        (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();

        var link = await client.GetFromJsonAsync<DetailsResult<CourseAttachmentDto>>($"/courses/attachments/{mine.Id}");
        Assert.Equal(mine.AttachmentId, link!.Item.AttachmentId);
        Assert.Equal("my file", await client.GetStringAsync($"/courses/{courseId}/files/{mineName}"));
    }

    // A new link in the owner's save may point at an attachment the owner already links, and at no other: one naming
    // another owner's attachment links nothing, so that owner's file is neither served here nor deleted with the link.
    [Fact]
    public async Task The_Owner_Cannot_Link_Another_Owners_Attachment()
    {
        using var client = _factory.CreateClient();

        var courseId = 8;
        var theirsName = $"theirs-{Guid.NewGuid():N}.txt";
        var theirs = await Upload(client, 9, theirsName, "their file");

        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var courseInput = new CourseInputDto
        {
            Id = details!.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = [new CourseAttachmentInputDto { ObjectId = courseId, AttachmentId = theirs.AttachmentId }]
        };
        // a link left without an attachment fails its foreign key, as one sent without a file does
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/courses/{courseId}/files/{theirsName}")).StatusCode);

        // saved again without it: a link made above would be removed now, and its attachment with it
        courseInput.Attachments = [];
        (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();
        Assert.Equal("their file", await client.GetStringAsync($"/courses/9/files/{theirsName}"));
    }

    // on an insert every link is new, whatever id it carries
    [Theory]
    [InlineData(0)]
    [InlineData(965233)]
    public async Task A_New_Owner_Cannot_Link_Another_Owners_Attachment(int linkId)
    {
        using var client = _factory.CreateClient();

        var theirsName = $"theirs-{Guid.NewGuid():N}.txt";
        var theirs = await Upload(client, 9, theirsName, "their file");

        var department = (await client.GetFromJsonAsync<DetailsResult<CourseDto>>("/courses/8"))!.Item.DepartmentId;
        var courseInput = new CourseInputDto
        {
            Title = $"New course {Guid.NewGuid():N}",
            DepartmentId = department,
            Credits = 1,
            Attachments = [new CourseAttachmentInputDto { Id = linkId, AttachmentId = theirs.AttachmentId }]
        };
        var response = await client.PostAsJsonAsync("/courses", courseInput);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("their file", await client.GetStringAsync($"/courses/9/files/{theirsName}"));
    }

    // The upload route creates a link: an Id in its form cannot turn the upload into a write to another link.
    [Fact]
    public async Task An_Upload_Creates_A_Link_Whatever_Id_Its_Form_Sends()
    {
        using var client = _factory.CreateClient();

        var theirsName = $"theirs-{Guid.NewGuid():N}.txt";
        var theirs = await Upload(client, 9, theirsName, "their file");
        var content = new MultipartFormDataContent
        {
            { new StreamContent(FileUtility.GetStreamFromString("my file")), "file", $"mine-{Guid.NewGuid():N}.txt" },
            { new StringContent(theirs.Id.ToString()), nameof(CourseAttachmentInputDto.Id) }
        };
        var response = await client.PostAsync("/courses/8/files", content);
        response.EnsureSuccessStatusCode();
        var mine = (await response.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>())!.Item;

        Assert.NotEqual(theirs.Id, mine.Id);
        var link = await client.GetFromJsonAsync<DetailsResult<CourseAttachmentDto>>($"/courses/attachments/{theirs.Id}");
        Assert.Equal(9, link!.Item.ObjectId);
        Assert.Equal(theirs.AttachmentId, link.Item.AttachmentId);
        Assert.Equal("their file", await client.GetStringAsync($"/courses/9/files/{theirsName}"));
    }

    // A rename retypes the attachment before the validators run, so a rule on the type judges the type the file is stored
    // with: renaming an allowed file into a refused type is refused.
    [Fact]
    public async Task A_Validator_Judges_The_Type_A_Rename_Gives()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddValidator<CourseAttachment>(ctx =>
            {
                if (ctx.Item.Attachment?.ContentType is { } type && !type.StartsWith("image/"))
                {
                    ctx.AddError(nameof(CourseAttachmentInputDto.NewFileName), "ImagesOnly");
                }
            })));
        using var client = app.CreateClient();

        var courseId = 6;
        var name = $"photo-{Guid.NewGuid():N}";
        var content = new MultipartFormDataContent { { new ByteArrayContent([137, 80, 78, 71]), "file", $"{name}.png" } };
        var uploaded = await (await client.PostAsync($"/courses/{courseId}/files", content)).Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        var rename = new CourseAttachmentInputDto
        {
            Id = uploaded!.Item.Id,
            ObjectId = courseId,
            AttachmentId = uploaded.Item.AttachmentId,
            NewFileName = $"{name}.html"
        };

        var refused = await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{uploaded.Item.Id}", rename);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var unchanged = await client.GetAsync($"/courses/{courseId}/files/{name}.png");
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal("image/png", unchanged.Content.Headers.ContentType?.MediaType);
    }

    // A new attachment added through the owner's save reaches the validators typed by its name, as an upload does.
    [Fact]
    public async Task A_New_Attachment_Through_The_Owner_Reaches_The_Validators_Typed()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddValidator<Course>(ctx =>
            {
                if (ctx.Item.Attachments?.Any(x => x.Attachment != null && x.Attachment.ContentType?.StartsWith("image/") != true) == true)
                {
                    ctx.AddError(nameof(Course.Attachments), "ImagesOnly");
                }
            })));
        using var client = app.CreateClient();

        var courseId = 7;
        var name = $"b-{Guid.NewGuid():N}.png";
        var details = await client.GetFromJsonAsync<DetailsResult<CourseDto>>($"/courses/{courseId}");
        var attachments = (details!.Item.Attachments ?? [])
            .Select(a => new CourseAttachmentInputDto { Id = a.Id, ObjectId = a.ObjectId, AttachmentId = a.AttachmentId })
            .ToList();
        attachments.Add(new CourseAttachmentInputDto { ObjectId = courseId, NewFileName = name, NewBytes = [137, 80, 78, 71] });
        var courseInput = new CourseInputDto
        {
            Id = details.Item.Id,
            Title = details.Item.Title,
            DepartmentId = details.Item.DepartmentId,
            Credits = details.Item.Credits,
            Attachments = attachments
        };

        (await client.PutAsJsonAsync($"/courses/{courseId}", courseInput)).EnsureSuccessStatusCode();
        var download = await client.GetAsync($"/courses/{courseId}/files/{name}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<CourseAttachmentDto> Upload(HttpClient client, int courseId, string fileName, string text)
    {
        var content = new MultipartFormDataContent { { new StreamContent(FileUtility.GetStreamFromString(text)), "file", fileName } };
        var response = await client.PostAsync($"/courses/{courseId}/files", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>())!.Item;
    }

    // an upload an app accepts by name may still render as a page, so every file but a PDF is served in a sandbox that
    // runs no script; a PDF goes without, for the browser's viewer
    [Theory]
    [InlineData("page.html", true)]
    [InlineData("drawing.svg", true)]
    [InlineData("photo.png", true)]
    [InlineData("report.pdf", false)]
    public async Task Every_File_But_A_Pdf_Is_Served_In_A_Sandbox(string fileName, bool sandboxed)
    {
        using var client = _factory.CreateClient();

        var courseId = 6;
        var content = new MultipartFormDataContent { { new ByteArrayContent("<script>alert(1)</script>"u8.ToArray()), "file", fileName } };
        (await client.PostAsync($"/courses/{courseId}/files", content)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/{fileName}");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(sandboxed, download.Headers.TryGetValues("Content-Security-Policy", out var csp) && csp.Contains("sandbox"));
    }

    /// <summary>A policy the app sends for every response; a browser enforces each policy it receives.</summary>
    private sealed class AppPolicy : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
                return nextMiddleware();
            });
            next(app);
        };
    }

    [Fact]
    public async Task The_Sandbox_Adds_To_A_Policy_The_App_Sent()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddTransient<IStartupFilter, AppPolicy>()));
        using var client = app.CreateClient();

        var courseId = 7;
        var content = new MultipartFormDataContent { { new ByteArrayContent("<script>alert(1)</script>"u8.ToArray()), "file", "page.html" } };
        (await client.PostAsync($"/courses/{courseId}/files", content)).EnsureSuccessStatusCode();

        var download = await client.GetAsync($"/courses/{courseId}/files/page.html");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(["default-src 'self'", "sandbox"], download.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task Refiling_An_Attachment_Moves_It_Virtually_Without_Touching_Storage()
    {
        using var client = _factory.CreateClient();

        var courseId = 4;
        var bareName = "refile-me.txt";
        await using var fileStream = FileUtility.GetStreamFromString(bareName);
        var content = new MultipartFormDataContent { { new StreamContent(fileStream), "file", $"archive/2026/{bareName}" } };
        var inserted = await (await client.PostAsync($"/courses/{courseId}/files", content))
            .Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var storedFilesBefore = Directory.GetFiles(
            Path.Combine(_factory.AttachmentsDirectory, "Course", "Attachments", courseId.ToString()),
            "*", SearchOption.AllDirectories);

        // the "move" is a rename to a different virtual folder
        var refiled = new CourseAttachmentInputDto
        {
            Id = inserted!.Item.Id,
            ObjectId = courseId,
            AttachmentId = inserted.Item.AttachmentId,
            NewFileName = $"archive/2027/{bareName}"
        };
        (await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{inserted.Item.Id}", refiled)).EnsureSuccessStatusCode();

        // it now answers on the new path and no longer on the old one
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/courses/{courseId}/files/archive/2027/{bareName}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/courses/{courseId}/files/archive/2026/{bareName}")).StatusCode);

        // ...while the bytes never moved
        var storedFilesAfter = Directory.GetFiles(
            Path.Combine(_factory.AttachmentsDirectory, "Course", "Attachments", courseId.ToString()),
            "*", SearchOption.AllDirectories);
        Assert.Equal(storedFilesBefore, storedFilesAfter);
    }

    [Fact]
    public async Task Update_Entity_With_New_EntityAttachment()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName1 = "test-attachment1.txt";
        await using var fileStream1 = FileUtility.GetStreamFromString(attachmentFileName1);
        var inputContent1 = new MultipartFormDataContent{
            { new StreamContent(fileStream1), "file", attachmentFileName1 }
        };
        var inputResponse1 = await client.PostAsync($"/courses/{courseId}/files", inputContent1);
        Assert.Equal(HttpStatusCode.OK, inputResponse1.StatusCode);

        var detailsResponse = await client.GetAsync($"/courses/{courseId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();

        var attachmentFileName2 = "test-attachment2.txt";

        await using var fileStream2 = FileUtility.GetStreamFromString(attachmentFileName2);

        var attachments = detailsResult!.Item
            .Attachments!
            .Select(x => new CourseAttachmentInputDto
            {
                Id = x.Id,
                Description = x.Description,
                AttachmentId = x.AttachmentId,
                ObjectId = x.ObjectId,
            })
            .ToList();
        attachments.Insert(0, new CourseAttachmentInputDto
        {
            NewFileName = attachmentFileName2,
            NewBytes = FileUtility.GetBytesFromString(attachmentFileName2),
            ObjectId = courseId,
        });
        var courseInput = new CourseInputDto
        {
            Id = detailsResult.Item.Id,
            Title = detailsResult.Item.Title,
            DepartmentId = detailsResult.Item.DepartmentId,
            Credits = detailsResult.Item.Credits,
            Attachments = attachments
        };

        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseInput.Id}", courseInput);
        var updateResult = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.EnsureSuccessStatusCode();
        
        var detailsResponse3 = await client.GetAsync($"/courses/{courseId}");
        var detailsResult3 = await detailsResponse3.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        Assert.Equal(2, detailsResult3!.Item.Attachments!.Count);
        var firstAttachment = detailsResult3.Item.Attachments.First();
        Assert.Equal(detailsResult.Item.Attachments!.Last().Id, firstAttachment.Id);
    }

    [Fact]
    public async Task Update_Entity_And_Replace_Attachment()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName1 = "test-attachment1.txt";
        await using var fileStream1 = FileUtility.GetStreamFromString(attachmentFileName1);
        var inputContent1 = new MultipartFormDataContent{
            { new StreamContent(fileStream1), "file", attachmentFileName1 }
        };
        var inputResponse1 = await client.PostAsync($"/courses/{courseId}/files", inputContent1);
        Assert.Equal(HttpStatusCode.OK, inputResponse1.StatusCode);

        var detailsResponse = await client.GetAsync($"/courses/{courseId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();

        var attachmentFileName2 = "test-attachment2.txt";

        var entityAttachment = detailsResult!.Item.Attachments!.First();
        var courseInput = new CourseInputDto
        {
            Id = detailsResult.Item.Id,
            Title = detailsResult.Item.Title,
            DepartmentId = detailsResult.Item.DepartmentId,
            Credits = detailsResult.Item.Credits,
            Attachments = [new CourseAttachmentInputDto
            {
                Id = entityAttachment.Id,
                Description = entityAttachment.Description,
                AttachmentId = entityAttachment.AttachmentId,
                ObjectId = entityAttachment.ObjectId,
                NewFileName =  attachmentFileName2,
                NewBytes = FileUtility.GetBytesFromString(attachmentFileName2)
            }]
        };

        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseInput.Id}", courseInput);
        updateResponse.EnsureSuccessStatusCode();

        var bytesResponse = await client.GetAsync($"/courses/{courseId}/files/{attachmentFileName2}");
        var bytesResult = await bytesResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(courseInput.Attachments.First().NewBytes, bytesResult);
    }
    [Fact]
    public async Task Delete()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString("This is a testmessage for an attachment")), "file", "test-attachment.txt" }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        inputResponse.EnsureSuccessStatusCode();

        var insertResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        var deleteResponse = await client.DeleteAsync($"/courses/attachments/{insertResult!.Item.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<DeleteResult<CourseDto>>();

        Assert.Equal(insertResult.Item.Id, deleteResult!.Item.Id);

        var detailsResponse = await client.GetAsync($"/courses/attachments/{insertResult.Item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);

        Assert.Empty(Directory.GetFiles(_factory.AttachmentsDirectory, "", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Update_FileName()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var insertFileText = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(insertFileText);
        var insertContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var insertResponse = await client.PostAsync($"/courses/{courseId}/files", insertContent);
        insertResponse.EnsureSuccessStatusCode();

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        var insertedItem = insertResult!.Item;

        var itemToUpdate = new CourseAttachmentInputDto
        {
            Id = insertedItem.Id,
            ObjectId = insertedItem.ObjectId,
            AttachmentId = insertedItem.AttachmentId,
            Description = insertedItem.Description,
            NewFileName = "updated-filename.txt"
        };
        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{insertedItem.Id}", itemToUpdate);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();


        var detailsResponse = await client.GetAsync($"/courses/attachments/{updateResult!.Item.Id}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseAttachmentDto>>();

        Assert.Equal(itemToUpdate.NewFileName, detailsResult!.Item.Attachment!.FileName);

        // check if file contents can be fetched with new file name
        using var downloadResponse = await client.GetAsync($"/courses/{courseId}/files/{itemToUpdate.NewFileName}");
        await using var downloadStream = await downloadResponse.Content.ReadAsStreamAsync();
        Assert.NotNull(downloadStream);
        Assert.Equal(fileStream.Length, downloadStream.Length);
        Assert.Equal(FileUtility.GetString(downloadStream), insertFileText);
    }
    [Fact]
    public async Task Update_Description()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var insertFileText = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(insertFileText);
        var insertContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var insertResponse = await client.PostAsync($"/courses/{courseId}/files", insertContent);
        insertResponse.EnsureSuccessStatusCode();
        var insertedResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();
        var itemToUpdate = insertedResult!.Item;
        itemToUpdate.Description = "Testing";

        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}/attachments/{itemToUpdate.Id}", itemToUpdate);
        updateResponse.EnsureSuccessStatusCode();
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var detailsResponse = await client.GetAsync($"/courses/attachments/{updateResult!.Item.Id}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseAttachmentDto>>();

        Assert.Equal(itemToUpdate.Description, detailsResult!.Item.Description);
    }
    [Fact]
    public async Task Update_File()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var insertFileText = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(insertFileText);
        var insertContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var insertResponse = await client.PostAsync($"/courses/{courseId}/files", insertContent);
        insertResponse.EnsureSuccessStatusCode();

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
        var insertedItem = insertResult!.Item;

        var updateFileText = $"This is an updated testmessage for attachment #{insertedItem.Id}";
        var updateContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString(updateFileText)), "file", "new-attachment.txt" }
        };
        var updateResponse = await client.PutAsync($"/courses/{courseId}/files/{insertedItem.Id}", updateContent);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();

        Assert.NotEqual(insertResult.Item.Attachment!.Id, updateResult!.Item.Attachment!.Id);
        Assert.NotEqual(insertResult.Item.Attachment!.Length, updateResult.Item.Attachment!.Length);
        Assert.NotEqual(insertResult.Item.Attachment!.FileName, updateResult.Item.Attachment!.FileName);

        using var downloadResponse = await client.GetAsync($"/courses/{courseId}/files/{updateResult.Item.Attachment!.FileName}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        await using var downloadStream = await downloadResponse.Content.ReadAsStreamAsync();
        Assert.NotNull(downloadStream);
        Assert.NotEqual(fileStream.Length, downloadStream.Length);
        Assert.Equal(FileUtility.GetString(downloadStream), updateFileText);
    }
    [Fact]
    public async Task Update_ObjectEntity_Data_Only()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var insertFileText = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(insertFileText);
        var insertContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var insertResponse = await client.PostAsync($"/courses/{courseId}/files", insertContent);
        insertResponse.EnsureSuccessStatusCode();

        var courseResponse = await client.GetAsync($"/courses/{courseId}");
        courseResponse.EnsureSuccessStatusCode();
        var courseResult = await courseResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var course = courseResult!.Item;

        Assert.Equal(1, course.Attachments?.Count);

        course.Credits = 0;
        var courseUpdateResponse = await client.PutAsJsonAsync($"/courses/{courseId}", course);
        courseUpdateResponse.EnsureSuccessStatusCode();
        var courseSavedResult = await courseUpdateResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        Assert.Equal(0, courseSavedResult!.Item.Credits);
    }
    [Fact]
    public async Task Update_FileName_By_ObjectEntity_Update()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString("This is a testmessage for an attachment")), "file", "test-attachment.txt" }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        inputResponse.EnsureSuccessStatusCode();

        var courseResponse = await client.GetAsync($"/courses/{courseId}");
        courseResponse.EnsureSuccessStatusCode();
        var courseResult = await courseResponse.Content.ReadFromJsonAsync<DetailsResult<CourseInputDto>>();
        var course = courseResult!.Item;

        course.Attachments!.First().NewFileName = "updated-attachment.txt";
        var courseUpdateResponse = await client.PutAsJsonAsync($"/courses/{courseId}", course);
        courseUpdateResponse.EnsureSuccessStatusCode();
        var courseSavedResult = await courseUpdateResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        Assert.Equal(course.Attachments!.First().NewFileName, courseSavedResult!.Item.Attachments!.First().Attachment!.FileName);
        Assert.Equal(course.Attachments!.Count, courseSavedResult.Item.Attachments?.Count);
    }
    [Fact]
    public async Task Delete_By_ObjectEntity_Update()
    {
        using var client = _factory.CreateClient();

        var courseId = 3;
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(FileUtility.GetStreamFromString("This is a testmessage for an attachment")), "file", "test-attachment.txt" }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        inputResponse.EnsureSuccessStatusCode();

        var insertResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var courseResponse = await client.GetAsync($"/courses/{courseId}");
        courseResponse.EnsureSuccessStatusCode();
        var courseResult = await courseResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        var course = courseResult!.Item;

        course.Attachments = course.Attachments!.Where(a => a.Id != insertResult!.Item.Id).ToList();
        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseId}", course);
        updateResponse.EnsureSuccessStatusCode();
        var courseSavedResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        Assert.Equal(course.Attachments.Count, courseSavedResult?.Item.Attachments?.Count);
    }
    [Fact]
    public async Task Get_Included_Attachments()
    {
        using var client = _factory.CreateClient();

        var courseId = 5;
        var attachmentFileName = "test-attachment.txt";
        var insertedAttachments = new List<EntityAttachmentDto>();
        for (var i = 1; i <= 3; i++)
        {
            var fileTextContent = $"This is the {i}th testmessage for attachments";
            var inputContent = new MultipartFormDataContent{
                { new StreamContent(FileUtility.GetStreamFromString(fileTextContent)), "file", attachmentFileName }
            };
            var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
            inputResponse.EnsureSuccessStatusCode();
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
            insertedAttachments.Add(saveResult!.Item);
        }

        var detailsResponse = await client.GetAsync($"/courses/{courseId}");
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();

        Assert.NotNull(detailsResult?.Item.Attachments);
        Assert.NotEmpty(detailsResult.Item.Attachments!);
        Assert.Equal(insertedAttachments.Count, detailsResult.Item.Attachments.Count);
        foreach (var courseAttachment in detailsResult.Item.Attachments)
        {
            Assert.NotNull(courseAttachment.Attachment);
        }
    }


    public void Dispose()
    {
        // delete all attachment files
        Directory.Delete(_factory.AttachmentsDirectory, true);
        // delete DB
        _dbContext.Database.EnsureDeleted();
    }
}