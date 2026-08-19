using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Keywords;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

// The FileName column stores the client's own value verbatim — dots, hyphens and virtual folders included —
// so its LIKE patterns must be built from the RAW keyword: QKeyword.TrimmedQ/StartsWith/EndsWith. The
// Q/QW and Normalized* members carry the NORMALIZED pattern (the default normalizer drops '.' and turns
// '-' into a space), which no real file name can ever match. The helper is constructed exactly as DI
// registers it (normalizing), so these cases pin the wiring, not a convenient non-normalizing stand-in.
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
            new Attachment { FileName = "notes.txt", Length = 500 });
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
    public async Task Extension_Matches_Every_File_Ending_With_It(string extension)
    {
        var results = await Filter(new AttachmentSearchObject { Extension = extension });

        Assert.That(results.Select(x => x.FileName), Is.EquivalentTo(new[]
        {
            "my-report-v2.pdf", "my-report.pdf", "archive/2026/scan.pdf"
        }));
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

        Assert.That(results, Has.Count.EqualTo(5));
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
}
