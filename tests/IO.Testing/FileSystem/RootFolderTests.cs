using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

/// <summary>
/// An identifier is relative to the root, however the root is written: with either separator, a trailing one,
/// dot segments or relative to the working directory. A contained service resolves paths in full, so the root it
/// strips from them must be compared in that form too, or an absolute path leaks out as the identifier.
/// </summary>
[TestFixture]
public class RootFolderTests
{
    private const string Identifier = "Unit/Attachments/1/photo.jpg";

    private string _temp = null!;

    [SetUp]
    public void SetUp()
        => _temp = Directory.CreateTempSubdirectory("root-folder-test-").FullName;

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temp))
            Directory.Delete(_temp, true);
    }

    // each spelling names the folder "uploads" (or "App_Data/uploads") below the temp folder
    private string GetRoot(string spelling) => spelling switch
    {
        "combined" => Path.Combine(_temp, "App_Data/uploads"),
        "forward" => _temp + "/uploads",
        "trailing" => _temp + "\\uploads\\",
        "dots" => Path.Combine(_temp, "app", "..", "uploads"),
        "relative" => Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(_temp, "uploads")),
        _ => throw new ArgumentOutOfRangeException(nameof(spelling))
    };
    private string GetFullRoot(string spelling)
        => Path.GetFullPath(FileNameUtility.ConvertForwardSlashes(GetRoot(spelling)));

    private static readonly string[] Spellings = ["combined", "forward", "trailing", "dots", "relative"];
    private static IEnumerable<TestCaseData> SpellingsAndContainment()
        => Spellings.SelectMany(s => new[] { new TestCaseData(s, true), new TestCaseData(s, false) });

    private BinaryFileService CreateService(string spelling, bool contained = true)
        => new(new FileSystemOptions { RootFolder = GetRoot(spelling), Contained = contained });

    [TestCaseSource(nameof(SpellingsAndContainment))]
    public async Task Save_Returns_The_Identifier(string spelling, bool contained)
    {
        var svc = CreateService(spelling, contained);

        var saved = await svc.Save(Identifier, [1, 2, 3]);

        Assert.That(saved, Is.EqualTo(Identifier));
        Assert.That(File.Exists(Path.Combine(GetFullRoot(spelling), "Unit", "Attachments", "1", "photo.jpg")));
    }

    [TestCaseSource(nameof(SpellingsAndContainment))]
    public async Task GetIdentifier_Of_The_Absolute_Path_Strips_The_Root(string spelling, bool contained)
    {
        var svc = CreateService(spelling, contained);
        await svc.Save(Identifier, [1]);

        // as an earlier release stored it: the path the service resolved (in full when contained), with '/'
        var stored = svc.GetAbsoluteUri(Identifier).Replace('\\', '/');

        Assert.That(svc.GetIdentifier(stored), Is.EqualTo(Identifier));
        Assert.That(svc.GetIdentifier(svc.GetAbsoluteUri(Identifier)), Is.EqualTo(Identifier));
        Assert.That(svc.GetIdentifier(Identifier), Is.EqualTo(Identifier));
        Assert.That(await svc.GetBytes(svc.GetIdentifier(stored)), Is.EqualTo(new byte[] { 1 }));
    }

    [TestCase("combined")]
    [TestCase("forward")]
    [TestCase("trailing")]
    [TestCase("dots")]
    [TestCase("relative")]
    public void GetIdentifier_Of_The_Full_Path_Strips_The_Root_When_Contained(string spelling)
    {
        var svc = CreateService(spelling);
        var stored = Path.Combine(GetFullRoot(spelling), Identifier).Replace('\\', '/');

        Assert.That(svc.GetIdentifier(stored), Is.EqualTo(Identifier));
    }

    [TestCaseSource(nameof(SpellingsAndContainment))]
    public void GetIdentifier_Of_A_Path_Spelled_With_The_Configured_Root_Strips_It(string spelling, bool contained)
    {
        var svc = CreateService(spelling, contained);

        Assert.That(svc.GetIdentifier(Path.Combine(svc.Root, Identifier)), Is.EqualTo(Identifier));
    }

    [TestCaseSource(nameof(SpellingsAndContainment))]
    public void GetRelativeFolder_Is_Relative_To_The_Root(string spelling, bool contained)
    {
        var svc = CreateService(spelling, contained);

        var expected = Path.Combine("Unit", "Attachments", "1");
        Assert.That(svc.GetRelativeFolder(Identifier), Is.EqualTo(expected));
        Assert.That(svc.GetRelativeFolder(svc.GetAbsoluteUri(Identifier)), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(SpellingsAndContainment))]
    public async Task List_Returns_Identifiers(string spelling, bool contained)
    {
        var svc = CreateService(spelling, contained);
        await svc.Save(Identifier, [1]);

        var listed = await svc.List(new FileSearchObject { Recursive = true, Type = FileEntryTypes.Files });

        Assert.That(listed.Select(f => f.Replace('\\', '/')), Is.EqualTo(new[] { Identifier }));
    }

    [Test]
    public void GetIdentifier_Matches_The_Root_Regardless_Of_Case_Where_The_File_System_Does()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("Folder names differ by case on this file system.");
        }
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = Path.Combine(_temp, "UPLOADS") });

        Assert.That(svc.GetIdentifier(Path.Combine(_temp, "uploads", Identifier)), Is.EqualTo(Identifier));
    }

    [Test]
    public void GetIdentifier_Does_Not_Strip_A_Sibling_Whose_Name_Starts_With_The_Root()
    {
        // "uploads-archive" starts with "uploads" but lies beside it: no identifier below the root names it
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = Path.Combine(_temp, "uploads"), Contained = false });
        var sibling = Path.Combine(_temp, "uploads-archive", "photo.jpg");

        // a path outside the root comes back whole, without the leading separator a relative folder trims
        Assert.That(svc.GetIdentifier(sibling), Is.EqualTo(sibling.Replace('\\', '/').TrimStart('/')));
        Assert.That(svc.GetRelativeFolder(sibling), Is.EqualTo(Path.GetDirectoryName(sibling)!.TrimStart(Path.DirectorySeparatorChar)));
    }

    [Test]
    public async Task The_Empty_Identifier_Names_A_Root_Of_Only_A_Separator()
    {
        // "/" trims to nothing, which an empty path must not match: it would resolve to "" rather than the root
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = "/" });

        Assert.That(FileNameUtility.GetAbsoluteUri("", "/"), Is.EqualTo(Path.DirectorySeparatorChar.ToString()));
        Assert.That(await svc.Exists(""), Is.True);
    }

    // --- TextFileService ---

    [TestCase(true)]
    [TestCase(false)]
    public async Task TextFileService_Save_Returns_The_Identifier(bool contained)
    {
        var svc = new TextFileService(new FileSystemOptions { RootFolder = GetRoot("combined"), Contained = contained });
        Directory.CreateDirectory(Path.Combine(GetFullRoot("combined"), "notes"));

        var saved = await svc.Save("notes/readme.txt", "hello");

        Assert.That(saved, Is.EqualTo("notes/readme.txt"));
        Assert.That(await svc.GetContents(saved), Is.EqualTo("hello"));
    }
}
