using Entities.TestApi.Infrastructure.Courses;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.AutoMapper;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.Web.Attachments.Abstractions;
using Regira.Entities.Web.Attachments.DependencyInjection;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Validation;
using Regira.IO.Storage.FileSystem;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.EFcore.Primers;
using System.Reflection;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

/// <summary>
/// Tests for <see cref="ControllerRegistrationValidator"/>: an EntityControllerBase subclass whose
/// generic arguments don't match any For&lt;&gt;() registration must fail startup (in Development /
/// when enabled) with the explanatory message, instead of a request-time 500.
/// </summary>
public class StartupValidationTests
{
    // internal on purpose: keeps these controllers invisible to the default ControllerFeatureProvider
    // (which only picks up public types), so the real TestApi host is unaffected
    internal enum ValidationTestSortBy { Default }
    [Flags]
    internal enum ValidationTestIncludes { None = 0 }
    internal class ComplexCourseValidationController : EntityControllerBase<Course, int, CourseSearchObject, ValidationTestSortBy, ValidationTestIncludes, Course, Course>;
    internal class SimpleCourseValidationController : EntityControllerBase<Course, int>;

    private sealed class FixedControllerFeatureProvider(params TypeInfo[] controllers) : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in controllers)
            {
                feature.Controllers.Add(controller);
            }
        }
    }

    private sealed class FakeMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => (TTarget)source;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => target;
    }

    private static ApplicationPartManager CreatePartManager(params Type[] controllers)
    {
        var partManager = new ApplicationPartManager();
        partManager.FeatureProviders.Add(new FixedControllerFeatureProvider(controllers.Select(t => t.GetTypeInfo()).ToArray()));
        return partManager;
    }

    private static async Task RunHostedServices(ServiceProvider serviceProvider)
    {
        foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Validator_Reports_Arity_Mismatch()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course, int, CourseSearchObject>();
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(ComplexCourseValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        var mismatch = issues.Where(i => i.Severity == EntityValidationSeverity.Error && i.Message.Contains(nameof(ComplexCourseValidationController))).ToArray();
        Assert.NotEmpty(mismatch);
        Assert.Contains("IEntityService", mismatch[0].Message);
        Assert.Contains("match", mismatch[0].Message);
    }

    [Fact]
    public void Validator_Passes_For_Matching_Registrations()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.AddSingleton<IEntityMapper>(new FakeMapper());
        services.UseEntities<ContosoContext>().For<Course, int, CourseSearchObject, ValidationTestSortBy, ValidationTestIncludes>();
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(ComplexCourseValidationController), typeof(SimpleCourseValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        Assert.Empty(issues);
    }

    // Reproduces a consumer configuration: attachment controllers are mapped and AddHttpContextAccessor() is
    // called, but UseAttachmentUris() is not — so every DTO ships Uri = null and the SPA downloads nothing.
    // The resolver itself can't report this: it is never constructed, the null one is registered instead.
    internal class CourseAttachmentValidationController : EntityAttachmentControllerBase<CourseAttachment>;

    [Fact]
    public void Validator_Warns_When_Attachment_Controller_Is_Mapped_Without_UseAttachmentUris()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>(o => o.UseAutoMapper())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() }))
            // the DTO mapping is what brings a Uri resolver into play at all
            .For<Course, int, CourseSearchObject>(e => e.HasAttachments(course => course.Attachments, a => a.WithDefaultMapping()));
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(CourseAttachmentValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        var warning = Assert.Single(issues, i => i.Message.Contains("null Uri"));
        Assert.Equal(EntityValidationSeverity.Warning, warning.Severity);
        Assert.Contains(nameof(CourseAttachment), warning.Message);
        Assert.Contains("UseAttachmentUris", warning.Message);
    }

    [Fact]
    public void Validator_Is_Silent_When_UseAttachmentUris_Is_Configured()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddRouting(); // LinkGenerator — the real resolver's dependency
        services.UseEntities<ContosoContext>(o =>
            {
                o.UseAutoMapper();
                o.UseAttachmentUris();
            })
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() }))
            // the DTO mapping is what brings a Uri resolver into play at all
            .For<Course, int, CourseSearchObject>(e => e.HasAttachments(course => course.Attachments, a => a.WithDefaultMapping()));
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(CourseAttachmentValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        Assert.DoesNotContain(issues, i => i.Message.Contains("null Uri"));
    }

    // IAttachmentUriResolver<T> is a public extension point and its natural spelling is a NON-generic class,
    // so the null-resolver check must not assume a generic implementation type.
    private sealed class CustomCourseAttachmentUriResolver : IAttachmentUriResolver<CourseAttachment>
    {
        public string? Resolve(CourseAttachment source) => "custom";
    }

    [Fact]
    public void Validator_Tolerates_A_Custom_NonGeneric_UriResolver()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>(o => o.UseAutoMapper())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() }))
            .For<Course, int, CourseSearchObject>(e => e.HasAttachments(course => course.Attachments, a => a.WithDefaultMapping()));
        services.AddTransient<IAttachmentUriResolver<CourseAttachment>, CustomCourseAttachmentUriResolver>();
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(CourseAttachmentValidationController)));

        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        Assert.DoesNotContain(issues, i => i.Message.Contains("null Uri"));
    }

    [Fact]
    public void Validator_Reports_Missing_EntityMapper()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course, int, CourseSearchObject, ValidationTestSortBy, ValidationTestIncludes>();
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(ComplexCourseValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        Assert.Contains(issues, i => i.Message.Contains(nameof(IEntityMapper)));
    }

    [Fact]
    public async Task Startup_Fails_On_Arity_Mismatch_When_Enabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ContosoContext>();
        services.AddSingleton(CreatePartManager(typeof(ComplexCourseValidationController)));
        services.ValidateEntityControllers();
        services.UseEntities<ContosoContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Course, int, CourseSearchObject>();

        await using var sp = services.BuildServiceProvider();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunHostedServices(sp));

        Assert.Contains(nameof(ComplexCourseValidationController), ex.Message);
        Assert.Contains("IEntityService", ex.Message);
    }

    [Fact]
    public async Task Startup_Passes_When_Registrations_Match()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ContosoContext>();
        services.AddSingleton(CreatePartManager(typeof(ComplexCourseValidationController)));
        services.ValidateEntityControllers();
        services.AddSingleton<IEntityMapper>(new FakeMapper());
        services.UseEntities<ContosoContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Course, int, CourseSearchObject, ValidationTestSortBy, ValidationTestIncludes>();

        await using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp); // must not throw
    }

    // ── Finding 10 (review 2026-07-12): no false positives on legitimate configurations ────────────

    private class OpenGenericEntityService<TEntity, TKey> : Regira.Entities.Services.Abstractions.IEntityService<TEntity, TKey>
        where TEntity : class, Regira.Entities.Models.Abstractions.IEntity<TKey>
    {
        public Task<TEntity?> Details(TKey id, CancellationToken token = default) => throw new NotImplementedException();
        public Task<IList<TEntity>> List(object? so = null, Regira.DAL.Paging.PagingInfo? pagingInfo = null, CancellationToken token = default) => throw new NotImplementedException();
        public Task<long> Count(object? so, CancellationToken token = default) => throw new NotImplementedException();
        public Task Add(TEntity item, CancellationToken token = default) => throw new NotImplementedException();
        public Task<TEntity?> Modify(TEntity item, CancellationToken token = default) => throw new NotImplementedException();
        public Task Save(TEntity item, CancellationToken token = default) => throw new NotImplementedException();
        public Task Remove(TEntity item, CancellationToken token = default) => throw new NotImplementedException();
        public Task<int> SaveChanges(CancellationToken token = default) => throw new NotImplementedException();
    }

    [Fact]
    public void IsRegistered_Accepts_An_Open_Generic_Registration()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(Regira.Entities.Services.Abstractions.IEntityService<,>), typeof(OpenGenericEntityService<,>));

        Assert.True(EntityServiceDiagnostics.IsRegistered(services, typeof(Regira.Entities.Services.Abstractions.IEntityService<Course, int>)));
    }

    [Fact]
    public void Validator_Accepts_An_Open_Generic_Service_Registration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEntityMapper>(new FakeMapper());
        services.AddTransient(typeof(Regira.Entities.Services.Abstractions.IEntityService<,>), typeof(OpenGenericEntityService<,>));
        using var sp = services.BuildServiceProvider();

        var validator = new ControllerRegistrationValidator(CreatePartManager(typeof(SimpleCourseValidationController)));
        var issues = validator.Validate(new EntityValidationContext(sp, services, new EntityRegistrationLog())).ToArray();

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Startup_Boots_When_Primers_Lack_An_Interceptor()
    {
        // UseDefaults registers primers/normalizers; the context has no interceptor. An overridden
        // SaveChanges is undetectable, so this must WARN, not gate startup.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ContosoContext>(db => db.UseSqlite("Filename=:memory:"));
        services.UseEntities<ContosoContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Course, int, CourseSearchObject>();

        await using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp); // must not throw
    }

    [Fact]
    public async Task Startup_Boots_When_Only_The_Primer_Container_Is_Registered()
    {
        // The documented non-interceptor pattern: RegisterPrimerContainer + explicit ApplyPrimers().
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ContosoContext>(db => db.UseSqlite("Filename=:memory:"));
        services.RegisterPrimerContainer<ContosoContext>();
        services.UseEntities<ContosoContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Course, int, CourseSearchObject>();

        await using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp); // must not throw — deliberate configuration, Info only
    }
}
