using Entities.TestApi.Infrastructure;
using Entities.Web.Testing.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Web.Models;
using Regira.IO.Utilities;
using System.Net;
using System.Net.Http.Json;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;


[Collection(nameof(NonParallelCollectionDefinition))]
public class AttachmentTests : IDisposable
{
    Course[] Courses { get; }
    Person[] Persons { get; }
    private readonly ContosoContext _dbContext;
    public AttachmentTests()
    {
        Directory.CreateDirectory(ApiConfiguration.AttachmentsDirectory);

        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ApiConfiguration.ConnectionString).Options);
        _dbContext.Database.EnsureCreated();

        var departments = Enumerable.Range(1, 5).Select((_, i) => new Department { Title = $"Department #{i}", Budget = i * 1000, StartDate = DateTime.Today.AddDays(i * 3) }).ToArray();
        Courses = Enumerable.Range(1, 5).Select((_, i) => new Course { Title = $"Course #{i}", Credits = Random.Shared.Next(1, 5), Department = departments[Random.Shared.Next(0, 4)] }).ToArray();
        Persons = PersonNames.EN.Take(5).Select(name => new Person { GivenName = name.GivenName, LastName = name.FamilyName }).ToArray();

        _dbContext.Courses.AddRange(Courses);
        _dbContext.Persons.AddRange(Persons);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Insert_And_Get_Typed_List()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();


        var courseId = Enumerable.Shuffle(Courses).First().Id;
        var courseAttachments = new List<EntityAttachmentDto>();
        for (var i = 1; i <= 10; i++)
        {
            var attachmentFileName = $"course-attachment-{i}.txt";
            var fileTextContent = $"This is the {i}th testmessage for attachments";
            var inputContent = new MultipartFormDataContent
            {
                { new StreamContent(FileUtility.GetStreamFromString(fileTextContent)), "file", attachmentFileName }
            };
            var inputResponse = await client.PostAsync($"/courses/{courseId}/files", inputContent);
            inputResponse.EnsureSuccessStatusCode();
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
            courseAttachments.Add(saveResult!.Item);
        }
        var personAttachments = new List<EntityAttachmentDto>();
        for (var i = 1; i <= 5; i++)
        {
            var personId = Enumerable.Shuffle(Persons).First().Id;
            var attachmentFileName = $"person-attachment-{i}.txt";
            var fileTextContent = $"This is the {i}th testmessage for attachments";
            var inputContent = new MultipartFormDataContent
            {
                { new StreamContent(FileUtility.GetStreamFromString(fileTextContent)), "file", attachmentFileName }
            };
            var inputResponse = await client.PostAsync($"/persons/{personId}/files", inputContent);
            inputResponse.EnsureSuccessStatusCode();
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<EntityAttachmentDto>>();
            personAttachments.Add(saveResult!.Item);
        }

        var response = await client.GetAsync("/attachments/typed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ListResult<EntityAttachmentDto>>();
        Assert.NotNull(result);

        var courseResultItems = result.Items.Where(x => x.ObjectType == nameof(Course)).ToArray();
        var personResultItems = result.Items.Where(x => x.ObjectType == nameof(Person)).ToArray();
        Assert.Equal(courseAttachments.Count, courseResultItems.Length);
        Assert.Equal(personAttachments.Count, personResultItems.Length);
        Assert.Equivalent(
            courseAttachments.Select(x => x.Attachment!.FileName),
            courseResultItems.Select(x => x.Attachment!.FileName)
        );
        Assert.Equivalent(
            personAttachments.Select(x => x.Attachment!.FileName),
            personResultItems.Select(x => x.Attachment!.FileName)
        );
    }


    public void Dispose()
    {
        // delete all attachment files
        Directory.Delete(ApiConfiguration.AttachmentsDirectory, true);
        // delete DB
        _dbContext.Database.EnsureDeleted();
    }
}