using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Courses;
using Entities.TestApi.Infrastructure.Departments;
using Entities.TestApi.Infrastructure.Enrollments;
using Entities.TestApi.Infrastructure.Persons;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Regira.DAL.EFcore.Services;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Mapping.AutoMapper;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Attachments.DependencyInjection;
using Regira.Entities.Web.DependencyInjection;
using Regira.IO.Storage.FileSystem;
using Regira.Licensing.DependencyInjection;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Testing.Library.Contoso;
using Testing.Library.Data;

var builder = WebApplication.CreateBuilder(args);

// Both option sets, deliberately: AddOpenApi() below generates its schemas from Http.Json.JsonOptions, which
// AddControllers().AddJsonOptions(...) does not touch. Configuring only the MVC side types every enum as an
// integer in the document while the API sends names — nothing errors, and the SPA generates the wrong types.
builder.Services.ConfigureDefaultJsonOptions();
builder.Services
    .AddControllers()
    .AddNewtonsoftJson(o =>
    {
        o.UseCamelCasing(true);
        var settings = o.SerializerSettings;
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.MissingMemberHandling = MissingMemberHandling.Ignore;
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        settings.DefaultValueHandling = DefaultValueHandling.Include;
        var converters = settings.Converters;
        converters.Add(new StringEnumConverter());
        //converters.Add(new BoolNumberConverter());
    });
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// OpenApi
builder.Services.AddOpenApi();

// Logging
builder.Host.UseSerilog((_, config) => config
    .WriteTo.Console()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
);

// Validate that all constructor-injected dependencies are registered when the app is built.
// This catches missing registrations at startup rather than on the first request.
// Note: does not cover GetRequiredEntityService<T> calls (those are resolved dynamically).
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateOnBuild = true;
    o.ValidateScopes = true;
});

builder.Services
    .AddProblemDetails();

builder.Services
    .AddHttpContextAccessor()
    .AddDbContext<ContosoContext>(db =>
    {
        // interceptors + UTC convention are auto-wired by UseEntities(o => o.UseDefaults()) below
        db.UseSqlite(ApiConfiguration.ConnectionString)
            .EnableSensitiveDataLogging();
    })
    .UseRegira(builder.Configuration)
    .UseEntities<ContosoContext>(o =>
    {
        o.UseDefaults();
        // resolve attachment Uris using ASP.NET Core LinkGenerator + IHttpContextAccessor
        o.UseAttachmentUris();
        // opt out of paging entirely (no cap) — full set unless the caller pages
        o.SetPageSize();
        o.AddGlobalFilterQueryBuilder<FilterHasNormalizedContentQueryBuilder>();
        o.AddPrepper<IHasAggregateKey>(x => x.AggregateKey ??= Guid.NewGuid());
        o.UseAutoMapper();
        //o.UseMapsterMapping();
    })
    // Entity types
    .AddEnrollments()
    .AddCourses()
    .AddDepartments()
    .AddPersons()
    // Attachments
    // FileSystem storage
    .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = ApiConfiguration.AttachmentsDirectory }))
    // Azure storage
    /*
    .ConfigureAttachmentService(_ => new BinaryBlobService(new AzureCommunicator(new AzureConfig
    {
        ConnectionString = builder.Configuration["Storage:Azure:ConnectionString"],
        ContainerName = "test-container"
    })))
    */
    // Attachment services
    .ConfigureTypedAttachmentService(db =>
    [
        db.CourseAttachments.ToDescriptor<Course>(),
        db.PersonAttachments.ToDescriptor<Person>()
    ]);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// add minimal api endpoints
app.MapEndPoints();
// add controller mappings
app.MapControllers();

app.Run();

// Make Program public
// https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-7.0
// ReSharper disable once PartialTypeWithSinglePart
namespace Entities.TestApi
{
    public partial class Program;
}
