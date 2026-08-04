using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Courses;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models;
using Regira.Entities.Web.Models;
using Regira.IO.Utilities;
using System.Net;
using System.Net.Http.Json;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

[Collection(nameof(NonParallelCollectionDefinition))]
public class CourseAttachmentsControllerTests : IDisposable
{
    Department[] Departments { get; }
    Course[] Courses { get; }

    private readonly ContosoContext _dbContext;
    public CourseAttachmentsControllerTests()
    {
        Directory.CreateDirectory(ApiConfiguration.AttachmentsDirectory);

        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ApiConfiguration.ConnectionString).Options);
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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/courses/attachments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();
        Assert.NotNull(result!.Items);
        Assert.Empty(result.Items);
    }
    [Fact]
    public async Task Insert_And_Get_Details()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var courseId = 3;
        var attachmentFileName = "test-attachment.txt";
        var fileTextContent = "This is a testmessage for an attachment";
        await using var fileStream = FileUtility.GetStreamFromString(fileTextContent);
        var inputContent = new MultipartFormDataContent{
            { new StreamContent(fileStream), "file", attachmentFileName }
        };
        var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);
        //Assert.Single(Directory.GetFiles(ApiConfiguration.AttachmentsDirectory, "", SearchOption.AllDirectories));
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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        //Assert.Equal(count, Directory.GetFiles(ApiConfiguration.AttachmentsDirectory, "", SearchOption.AllDirectories).Length);
    }
    [Fact]
    public async Task Insert_And_Force_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var entityFolder = Path.Combine(ApiConfiguration.AttachmentsDirectory, "Course", "Attachments", courseId.ToString());
        Assert.False(Directory.Exists(Path.Combine(entityFolder, "archive")), "the virtual folder must stay virtual");
    }

    [Fact]
    public async Task Refiling_An_Attachment_Moves_It_Virtually_Without_Touching_Storage()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var courseId = 4;
        var bareName = "refile-me.txt";
        await using var fileStream = FileUtility.GetStreamFromString(bareName);
        var content = new MultipartFormDataContent { { new StreamContent(fileStream), "file", $"archive/2026/{bareName}" } };
        var inserted = await (await client.PostAsync($"/courses/{courseId}/files", content))
            .Content.ReadFromJsonAsync<SaveResult<CourseAttachmentDto>>();

        var storedFilesBefore = Directory.GetFiles(
            Path.Combine(ApiConfiguration.AttachmentsDirectory, "Course", "Attachments", courseId.ToString()),
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
            Path.Combine(ApiConfiguration.AttachmentsDirectory, "Course", "Attachments", courseId.ToString()),
            "*", SearchOption.AllDirectories);
        Assert.Equal(storedFilesBefore, storedFilesAfter);
    }

    [Fact]
    public async Task Update_Entity_With_New_EntityAttachment()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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

        Assert.Empty(Directory.GetFiles(ApiConfiguration.AttachmentsDirectory, "", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Update_FileName()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

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
        Directory.Delete(ApiConfiguration.AttachmentsDirectory, true);
        // delete DB
        _dbContext.Database.EnsureDeleted();
    }
}