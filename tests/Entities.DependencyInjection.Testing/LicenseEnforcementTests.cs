using Entities.DependencyInjection.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.IO.Storage.FileSystem;
using Regira.Licensing.DependencyInjection;
using Regira.Licensing.Models;
using Regira.Licensing.Services;
using System.Security.Cryptography;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

/// <summary>
/// Tests that entity license enforcement respects the free tier:
/// up to 5 simple entities or 2 complex entities require no license.
/// Exceeding either limit triggers validation.
/// </summary>
[TestFixture]
public class LicenseEnforcementTests
{
    private RSA _testRsa = null!;

    [SetUp]
    public void Setup()
    {
        _testRsa = RSA.Create(2048);
        LicenseValidator.TestPublicKey = _testRsa;
    }

    [TearDown]
    public void Teardown()
    {
        LicenseValidator.TestPublicKey = null;
        _testRsa.Dispose();
    }

    private string MakeValidLicenseKey()
    {
        var license = new License
        {
            CustomerId = "test-customer",
            Products = ["regira.entities"],
            Tier = "paid",
            Version = "5",
            IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds()
            // No Limits = unlimited
        };
        return LicenseSigner.Sign(license, _testRsa);
    }

    private string MakeFreeLicenseKey()
    {
        var license = new License
        {
            CustomerId = "free",
            Products = ["regira.entities"],
            Version = "5",
            IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            Limits = new Dictionary<string, int>
            {
                ["entities.simple"] = 5,
                ["entities.complex"] = 2
            }
        };
        return LicenseSigner.Sign(license, _testRsa);
    }

    // --- Free-tier boundary tests (using free license key) ---

    [Test]
    public void For5SimpleEntities_WithFreeLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>());
    }

    [Test]
    public void For6thSimpleEntity_WithFreeLicense_Throws()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>()); // 6th entity exceeds free limit of 5
    }

    [Test]
    public void Attachments_OwnerPlusJoinCountOnce_SharedBaseFree_WithFreeLicense_DoesNotThrow()
    {
        // Attachment licensing model:
        //  - the shared Attachment base (registered via WithAttachments) is framework infrastructure and
        //    consumes NO slot, no matter how many owners use attachments;
        //  - the per-owner join entity (registered via HasAttachments) consumes exactly ONE simple slot,
        //    even though it is registered twice internally;
        //  - the owner entity counts as its own slot.
        // => Course + CourseAttachment + Department + Enrollment + Instructor = 5 simple, exactly the free
        //    limit. Without the shared-base exemption this would be 6 (Attachment); without per-type dedup
        //    it would be 6 (CourseAttachment counted twice). Either regression makes this throw.
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()))
                .For<Course, int, CourseSearchObject>(e => e.HasAttachments(item => item.Attachments))
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>());
    }

    [Test]
    public void For2ComplexEntities_WithFreeLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Department, CourseSearchObject, CourseSortBy, CourseIncludes>());
    }

    [Test]
    public void For3rdComplexEntity_WithFreeLicense_Throws()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Department, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Enrollment, CourseSearchObject, CourseSortBy, CourseIncludes>()); // 3rd exceeds free limit of 2
    }

    [Test]
    public void For5Simple2Complex_WithFreeLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Person, CourseSearchObject, CourseSortBy, CourseIncludes>());
    }

    [Test]
    public void For5Simple_Then3rdComplex_WithFreeLicense_Throws()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Person, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Instructor, CourseSearchObject, CourseSortBy, CourseIncludes>()); // 3rd complex
    }

    [Test]
    public void SplitAcrossMultipleUseEntities_WithFreeLicense_Throws()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeFreeLicenseKey());
        // Counts are shared per IServiceCollection, so splitting across two UseEntities() calls
        // does not reset the counter.
        services.UseEntities<ContosoContext>()
            .For<Course>()
            .For<Department>()
            .For<Enrollment>();
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>()); // 6th entity across both calls
    }

    // --- Virtual free tier (no UseRegira call at all) ---

    [Test]
    public void For5SimpleEntities_WithNoLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>().For<Department>().For<Enrollment>()
                .For<Instructor>().For<OfficeAssignment>());
    }

    [Test]
    public void For6thSimpleEntity_WithNoLicense_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>().For<Department>().For<Enrollment>()
                .For<Instructor>().For<OfficeAssignment>().For<Student>());
    }

    [Test]
    public void For2ComplexEntities_WithNoLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Department, CourseSearchObject, CourseSortBy, CourseIncludes>());
    }

    [Test]
    public void For3rdComplexEntity_WithNoLicense_Throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Department, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Enrollment, CourseSearchObject, CourseSortBy, CourseIncludes>());
    }

    // --- Exception message content ---

    [Test]
    public void For6thSimpleEntity_ExceptionMessage_StatesCountsLimitsAndEntityNames()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>());
        Assert.That(ex!.Message, Does.Contain("allows 5 simple and 2 complex"));
        Assert.That(ex.Message, Does.Contain("registers 6 simple and 0 complex"));
        Assert.That(ex.Message, Does.Contain("Course").And.Contain("Student"));
        Assert.That(ex.Message, Does.Contain("https://regira.com/licensing"));
    }

    [Test]
    public void For3rdComplexEntity_ExceptionMessage_ListsComplexEntitiesAndDefinesComplex()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Department, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Enrollment, CourseSearchObject, CourseSortBy, CourseIncludes>());
        Assert.That(ex!.Message, Does.Contain("registers 0 simple and 3 complex"));
        Assert.That(ex.Message, Does.Contain("Complex (3): Course, Department, Enrollment"));
        Assert.That(ex.Message, Does.Contain("TSortBy"));
        Assert.That(ex.Message, Does.Contain("https://regira.com/licensing"));
    }

    [Test]
    public void MixedOverflow_ExceptionMessage_ShowsBothSimpleAndComplexLists()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Person, CourseSearchObject, CourseSortBy, CourseIncludes>()
                .For<Instructor, CourseSearchObject, CourseSortBy, CourseIncludes>()); // 3rd complex
        Assert.That(ex!.Message, Does.Contain("- Simple (5):"));
        Assert.That(ex.Message, Does.Contain("- Complex (3):"));
    }

    [Test]
    public void UseRegira_WithInvalidLicenseKey_ThrowsLicenseException()
    {
        var services = new ServiceCollection();
        Assert.Throws<LicenseException>(() =>
            services.UseRegira("not.a.valid.license")); // invalid key fails at parse time
    }

    [Test]
    public void UseEntities_WithValidLicense_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.UseRegira(MakeValidLicenseKey());
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>()); // no Limits in license = unlimited
    }

    [Test]
    public void UseEntities_WithExpiredLicense_ThrowsLicenseException()
    {
        var license = new License
        {
            CustomerId = "test-customer",
            Products = ["regira.entities"],
            Version = "5",
            IssuedAtUnix = DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds(),
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds()
        };
        var services = new ServiceCollection();
        services.UseRegira(LicenseSigner.Sign(license, _testRsa));
        var ex = Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>());
        Assert.That(ex!.Message, Does.Contain("expired"));
    }

    [Test]
    public void UseEntities_WithWrongProductInLicense_FallsBackToFreeTier()
    {
        // An office-only license doesn't cover regira.entities; free-tier limits apply.
        var license = new License
        {
            CustomerId = "test-customer",
            Products = ["regira.services"],
            Tier = "paid",
            Version = "5",
            IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds()
        };
        var services = new ServiceCollection();
        services.UseRegira(LicenseSigner.Sign(license, _testRsa));
        // 6 entities exceeds the free-tier limit of 5 simple
        Assert.Throws<LicenseException>(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>());
    }

    // --- Multi-license: paid wins over free ---

    [Test]
    public void UseEntities_PaidLicenseWinsOverFree_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.UseRegira((string?)null);           // free tier
        services.UseRegira(MakeValidLicenseKey());  // paid entities — should win
        Assert.DoesNotThrow(() =>
            services.UseEntities<ContosoContext>()
                .For<Course>()
                .For<Department>()
                .For<Enrollment>()
                .For<Instructor>()
                .For<OfficeAssignment>()
                .For<Student>()); // 6 entities — allowed because paid license has no limits
    }
}
