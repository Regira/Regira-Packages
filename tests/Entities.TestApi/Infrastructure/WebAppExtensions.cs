using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Services.Abstractions;
using System.Text;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.TestApi.Infrastructure;

public static class WebAppExtensions
{
    public static WebApplication MapEndPoints(this WebApplication app)
    {
        app.MapGet("/", () => "Hello");
        app.MapPost("/db", ([FromServices] ContosoContext db) => db.Database.EnsureCreatedAsync());
        app.MapDelete("/db", ([FromServices] ContosoContext db) => db.Database.EnsureDeletedAsync());
        app.MapPost("/test-data", async ([FromServices] ContosoContext db, [FromServices] IEntityService<CourseAttachment, int> courseAttachmentService) =>
        {
            await db.Database.EnsureCreatedAsync();

            var departments = Enumerable.Range(1, 5).Select((_, i) => new Department { Title = $"Department #{i}", Budget = i * 1000, StartDate = DateTime.Today.AddDays(i * 3) }).ToArray();
            var courses = Enumerable.Range(1, 50).Select((_, i) => new Course { Title = $"Course #{i}", Credits = Random.Shared.Next(1, 5), Department = departments[Random.Shared.Next(0, 4)] }).ToArray();

            db.Departments.AddRange(departments);
            db.Courses.AddRange(courses);

            await db.SaveChangesAsync();

            var courseAttachments = Enumerable.Shuffle(courses).Take(10).Select(x => new CourseAttachment
            {
                ObjectId = x.Id,
                Attachment = new Attachment
                {
                    Bytes = Encoding.UTF8.GetBytes($"This is an attachment of course #{x.Id}"),
                    FileName = $"courses/course-{x.Id}.txt",
                    ContentType = "text/plain"
                }
            });

            foreach (var item in courseAttachments)
            {
                await courseAttachmentService.Add(item);
            }

            await courseAttachmentService.SaveChanges();
        });
        var departments = app.MapGroup("/minimal/departments");
        departments.MapGet("", async ([FromServices] ContosoContext db) => await db.Departments.ToListAsync());
        departments.MapPut("", async ([FromServices] ContosoContext db, [FromBody] Department input) =>
        {
            db.Departments.Add(input);
            await db.SaveChangesAsync();
        });

        return app;
    }
}