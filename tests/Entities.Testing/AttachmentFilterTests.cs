using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Keywords;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

// The FileName column stores the client's own value verbatim — dots, hyphens and virtual folders included —
// so its LIKE patterns must be built from the RAW keyword: the Trimmed* family (QKeyword.TrimmedQ,
// TrimmedStartsWith, TrimmedEndsWith). The unprefixed members — Q/QW, StartsWith/EndsWith — carry the
// NORMALIZED pattern (the default normalizer drops '.' and turns '-' into a space), which no real file
// name can ever match. The helper is constructed exactly as DI registers it (normalizing), so these cases
// pin the wiring, not a convenient non-normalizing stand-in.
[TestFixture]
public class AttachmentFilterTests
{
    private SqliteConnection _connection = null!;
    private ContosoContext _db = null!;
    private readonly QKeywordHelper _qHelper = new();

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _db.Set<Attachment>().AddRange(
            new Attachment { FileName = "my-report-v2.pdf", Length = 100 },
            new Attachment { FileName = "my-report.pdf", Length = 200 },
            new Attachment { FileName = "archive/2026/scan.pdf", Length = 300 },
            new Attachment { FileName = "mypicture.jpg", Length = 400 },
            new Attachment { FileName = "notes.txt", Length = 500 },
            // no dot before "pdf": the counter-example for an extension filter that anchors on the
            // separator rather than matching any suffix
            new Attachment { FileName = "handbook-nopdf", Length = 600 });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Close();
    }

    private Task<List<Attachment>> Filter(AttachmentSearchObject so)
        => new AttachmentFilteredQueryBuilder(_qHelper)
            .Build(_db.Set<Attachment>(), so)
            .ToListAsync();

    // --- Extension ---

    [TestCase(".pdf")]
    [TestCase("pdf")]
    public async Task Extension_Matches_Every_File_With_That_Extension(string extension)
    {
        var results = await Filter(new AttachmentSearchObject { Extension = extension });

        Assert.That(results.Select(x => x.FileName), Is.EquivalentTo(new[]
        {
            "my-report-v2.pdf", "my-report.pdf", "archive/2026/scan.pdf"
        }));
    }

    [TestCase(".pdf")]
    [TestCase("pdf")]
    public async Task Extension_Does_Not_Match_A_Name_That_Merely_Ends_With_It(string extension)
    {
        // "%pdf" would take "handbook-nopdf" as well — "%.pdf" is what makes this an extension filter
        var results = await Filter(new AttachmentSearchObject { Extension = extension });

        Assert.That(results.Select(x => x.FileName), Has.No.Member("handbook-nopdf"));
    }

    [Test]
    public async Task Extension_Excludes_Other_Extensions()
    {
        var results = await Filter(new AttachmentSearchObject { Extension = ".jpg" });

        Assert.That(results.Select(x => x.FileName), Is.EqualTo(new[] { "mypicture.jpg" }));
    }

    [Test]
    public async Task Extension_Tolerates_A_Leading_Wildcard()
    {
        // "*.txt" is the same intent spelled with the input wildcard convention — it must not end up in the pattern
        var results = await Filter(new AttachmentSearchObject { Extension = "*.txt" });

        Assert.That(results.Select(x => x.FileName), Is.EqualTo(new[] { "notes.txt" }));
    }

    [Test]
    public async Task Extension_Of_Only_Wildcards_Filters_Nothing()
    {
        // "*" trims to the empty string, which would anchor on a bare "%." and match every dotted name
        // for no stated intent. Skipping the clause instead leaves the other filters to do the work —
        // the same result a caller gets by omitting the parameter.
        var results = await Filter(new AttachmentSearchObject { Extension = "*" });

        Assert.That(results, Has.Count.EqualTo(6));
    }

    // --- FileName, wildcard branch ---

    [Test]
    public async Task FileName_StartsWith_Keeps_Hyphens()
    {
        // normalized this would be "my report%" — matching nothing
        var results = await Filter(new AttachmentSearchObject { FileName = "my-report*" });

        Assert.That(results.Select(x => x.FileName), Is.EquivalentTo(new[] { "my-report-v2.pdf", "my-report.pdf" }));
    }

    [Test]
    public async Task FileName_EndsWith_Keeps_Dots()
    {
        // normalized this would be "%v2pdf" — matching nothing
        var results = await Filter(new AttachmentSearchObject { FileName = "*v2.pdf" });

        Assert.That(results.Select(x => x.FileName), Is.EqualTo(new[] { "my-report-v2.pdf" }));
    }

    [Test]
    public async Task FileName_Contains_Keeps_The_Virtual_Folder()
    {
        var results = await Filter(new AttachmentSearchObject { FileName = "*archive/2026*" });

        Assert.That(results.Select(x => x.FileName), Is.EqualTo(new[] { "archive/2026/scan.pdf" }));
    }

    [Test]
    public async Task FileName_Wildcard_Excludes_Non_Matching_Rows()
    {
        Assert.That(await Filter(new AttachmentSearchObject { FileName = "my report*" }), Is.Empty);
    }

    // --- SQL LIKE metacharacters ---
    // Raw file names reach the pattern unescaped, so '_' and '%' keep their LIKE meaning. No seeded file
    // contains either character, so any match below is the wildcard acting, not a literal hit. This
    // over-matches and never under-matches, and it is not injection — EF parameterizes the pattern.
    // Pinned rather than escaped: an ESCAPE clause plus provider-specific handling of '[' is a wider change.

    [Test]
    public async Task FileName_Underscore_Acts_As_A_Single_Character_Wildcard()
    {
        // "my_report*" reads as a literal underscore, but LIKE spends the '_' on the hyphen
        var results = await Filter(new AttachmentSearchObject { FileName = "my_report*" });

        Assert.That(results.Select(x => x.FileName), Is.EquivalentTo(new[] { "my-report-v2.pdf", "my-report.pdf" }));
    }

    [Test]
    public async Task FileName_Percent_Acts_As_A_Multi_Character_Wildcard()
    {
        // "*%*" becomes the pattern "%%%" — every row, though nothing is named with a percent sign
        var results = await Filter(new AttachmentSearchObject { FileName = "*%*" });

        Assert.That(results, Has.Count.EqualTo(6));
    }

    // --- FileName, exact branch (unchanged) ---

    [Test]
    public async Task FileName_Without_Wildcard_Matches_Exactly()
    {
        var results = await Filter(new AttachmentSearchObject { FileName = "my-report.pdf" });

        Assert.That(results.Select(x => x.FileName), Is.EqualTo(new[] { "my-report.pdf" }));
    }

    [Test]
    public async Task FileName_Without_Wildcard_Does_Not_Match_A_Prefix()
    {
        Assert.That(await Filter(new AttachmentSearchObject { FileName = "my-report" }), Is.Empty);
    }

    // --- Combined with the size filters ---

    [Test]
    public async Task Extension_Combines_With_Size()
    {
        var results = await Filter(new AttachmentSearchObject { Extension = ".pdf", MinSize = 150 });

        Assert.That(results.Select(x => x.FileName), Is.EquivalentTo(new[] { "my-report.pdf", "archive/2026/scan.pdf" }));
    }

    // --- The same rule on the entity-attachment builder ---

    [Test]
    public async Task EntityAttachment_FileName_Wildcard_Keeps_Dots_And_Hyphens()
    {
        var course = new Course { Title = "Chemistry", Credits = 3 };
        _db.Set<Course>().Add(course);
        await _db.SaveChangesAsync();
        _db.Set<CourseAttachment>().AddRange(
            new CourseAttachment { ObjectId = course.Id, Attachment = new Attachment { FileName = "my-report-v2.pdf" } },
            new CourseAttachment { ObjectId = course.Id, Attachment = new Attachment { FileName = "syllabus.docx" } });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var results = await new EntityAttachmentFilteredQueryBuilder<CourseAttachment, EntityAttachmentSearchObject>(_qHelper)
            .Build(_db.Set<CourseAttachment>().Include(x => x.Attachment), new EntityAttachmentSearchObject { FileName = "my-report*" })
            .ToListAsync();

        Assert.That(results.Select(x => x.Attachment!.FileName), Is.EqualTo(new[] { "my-report-v2.pdf" }));
    }

    // --- ...and on the EntityAttachmentQueryExtensions.Filter extension ---
    // The static extension has no DI, so it builds its own QKeywordHelper. It must still reach the same
    // meaning of Extension as the builders: anchored on the dot, input wildcards stripped.

    private async Task<List<CourseAttachment>> SeedCourseAttachments(params string[] fileNames)
    {
        var course = new Course { Title = "Chemistry", Credits = 3 };
        _db.Set<Course>().Add(course);
        await _db.SaveChangesAsync();
        _db.Set<CourseAttachment>().AddRange(fileNames.Select(fileName =>
            new CourseAttachment { ObjectId = course.Id, Attachment = new Attachment { FileName = fileName } }));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return await _db.Set<CourseAttachment>().Include(x => x.Attachment).ToListAsync();
    }

    private Task<List<CourseAttachment>> FilterViaExtension(EntityAttachmentSearchObject aso)
        => _db.Set<CourseAttachment>().Include(x => x.Attachment).Filter(aso).ToListAsync();

    [TestCase(".pdf")]
    [TestCase("pdf")]
    [TestCase("*.pdf")]
    public async Task Extension_Via_The_Query_Extension_Anchors_On_The_Dot(string extension)
    {
        // before: "%pdf" took "handbook-nopdf" too, and "*.pdf" reached the pattern as "%*.pdf" — matching nothing
        await SeedCourseAttachments("my-report.pdf", "handbook-nopdf", "notes.txt");

        var results = await FilterViaExtension(new EntityAttachmentSearchObject { Extension = extension });

        Assert.That(results.Select(x => x.Attachment!.FileName), Is.EqualTo(new[] { "my-report.pdf" }));
    }

    [Test]
    public async Task Extension_Via_The_Query_Extension_Of_Only_Wildcards_Filters_Nothing()
    {
        await SeedCourseAttachments("my-report.pdf", "handbook-nopdf", "notes.txt");

        var results = await FilterViaExtension(new EntityAttachmentSearchObject { Extension = "*" });

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Extension_Via_The_Query_Extension_Combines_With_ObjectId()
    {
        var seeded = await SeedCourseAttachments("my-report.pdf", "handbook-nopdf");

        var results = await FilterViaExtension(new EntityAttachmentSearchObject
        {
            ObjectId = [seeded[0].ObjectId],
            Extension = "pdf"
        });

        Assert.That(results.Select(x => x.Attachment!.FileName), Is.EqualTo(new[] { "my-report.pdf" }));
    }
}
