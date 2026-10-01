using System.IO.Compression;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using SharpZipFile = ICSharpCode.SharpZipLib.Zip.ZipFile;
using Regira.IO.Compression.SharpZipLib;
using Regira.IO.Models;

namespace IO.Testing.SharpZipLib;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ZipManagerTests
{
    private const string Password = "s3cr3t";

    private static BinaryFileItem[] CreateFiles()
    {
        // incompressible and larger than one read buffer, so an entry only comes back whole when it is read to its end
        var large = new byte[1024 * 1024];
        new Random(42).NextBytes(large);
        return
        [
            new BinaryFileItem { FileName = "readme.txt", Bytes = Encoding.UTF8.GetBytes("hello") },
            new BinaryFileItem { FileName = "docs/report.bin", Bytes = large }
        ];
    }

    private static void AssertSameFiles(IEnumerable<BinaryFileItem> actual, IEnumerable<BinaryFileItem> expected)
    {
        var actualByName = actual.ToDictionary(f => f.FileName!, f => f.Bytes);
        var expectedByName = expected.ToDictionary(f => f.FileName!, f => f.Bytes);
        Assert.That(actualByName.Keys, Is.EquivalentTo(expectedByName.Keys));
        foreach (var (name, bytes) in expectedByName)
        {
            Assert.That(actualByName[name], Is.EqualTo(bytes), name);
        }
    }

    [Test]
    public async Task Zip_Then_Unzip_Returns_Every_File()
    {
        var files = CreateFiles();
        var zm = new ZipManager();

        await using var archive = zm.Zip(files);
        using var unzipped = await zm.Unzip(archive);

        AssertSameFiles(unzipped.Cast<BinaryFileItem>(), files);
    }

    [Test]
    public async Task Zip_Writes_An_Archive_Other_Readers_Open()
    {
        var files = CreateFiles();

        await using var archive = new ZipManager().Zip(files);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read);

        var read = new List<BinaryFileItem>();
        foreach (var entry in zip.Entries)
        {
            await using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            read.Add(new BinaryFileItem { FileName = entry.FullName, Bytes = ms.ToArray() });
        }
        AssertSameFiles(read, files);
    }

    [Test]
    public async Task Zip_Names_Entries_With_Forward_Slashes()
    {
        var files = new[] { new BinaryFileItem { FileName = @"docs\sub\note.txt", Bytes = [1, 2, 3] } };

        await using var archive = new ZipManager().Zip(files);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read);

        Assert.That(zip.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "docs/sub/note.txt" }));
    }

    [TestCase("/docs/note.txt")]
    [TestCase("C:/docs/note.txt")]
    public async Task Zip_Drops_A_Leading_Separator_Or_Drive(string fileName)
    {
        await using var archive = new ZipManager().Zip([new BinaryFileItem { FileName = fileName, Bytes = [1, 2, 3] }]);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read);

        Assert.That(zip.Entries.Select(e => e.FullName), Is.EqualTo(new[] { "docs/note.txt" }));
    }

    // the names Unzip would refuse are refused before an archive holding them is written
    [TestCase("../note.txt")]
    [TestCase(@"docs\..\..\note.txt")]
    public void Zip_Refuses_A_Name_That_Climbs_Out(string fileName)
        => Assert.Throws<UnauthorizedAccessException>(() => new ZipManager().Zip([new BinaryFileItem { FileName = fileName, Bytes = [1, 2, 3] }]));

    [Test]
    public async Task Zip_With_Password_Encrypts_Every_Entry_With_Aes256()
    {
        await using var archive = new ZipManager().Zip(CreateFiles(), Password);
        using var zip = new SharpZipFile(archive, leaveOpen: true);

        var entries = zip.Cast<ZipEntry>().ToList();
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries.Select(e => (e.IsCrypted, e.AESKeySize)), Has.All.EqualTo((true, 256)));
    }

    [Test]
    public async Task Unzip_With_Password_Returns_Every_File()
    {
        var files = CreateFiles();
        var zm = new ZipManager();

        await using var archive = zm.Zip(files, Password);
        using var unzipped = await zm.Unzip(archive, Password);

        AssertSameFiles(unzipped.Cast<BinaryFileItem>(), files);
    }

    [Test]
    public async Task Unzip_With_Wrong_Password_Throws()
    {
        var zm = new ZipManager();
        await using var archive = zm.Zip(CreateFiles(), Password);

        await Assert.ThrowsAsync<ZipException>(() => zm.Unzip(archive, "wrong"));
    }

    [Test]
    public async Task Unzip_Reads_A_Stream_That_Cannot_Seek()
    {
        var files = CreateFiles();
        var zm = new ZipManager();
        await using var archive = zm.Zip(files);

        await using var forwardOnly = new ForwardOnlyStream(archive);
        using var unzipped = await zm.Unzip(forwardOnly);

        AssertSameFiles(unzipped.Cast<BinaryFileItem>(), files);
    }

    // an archive another tool wrote, entry names as given
    private static MemoryStream ArchiveWithEntries(params string[] names)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in names)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write([1, 2, 3]);
            }
        }
        ms.Position = 0;
        return ms;
    }

    [TestCase("../../evil.txt")]
    [TestCase("docs/../../evil.txt")]
    [TestCase(@"..\evil.txt")]
    [TestCase("/etc/evil.txt")]
    [TestCase(@"\evil.txt")]
    [TestCase("C:/Windows/evil.txt")]
    public async Task Unzip_Refuses_An_Entry_That_Escapes_The_Archive(string name)
    {
        await using var archive = ArchiveWithEntries("readme.txt", name);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new ZipManager().Unzip(archive));
    }

    [Test]
    public async Task Unzip_Names_Entries_With_Forward_Slashes()
    {
        await using var archive = ArchiveWithEntries(@"docs\v1..2\notes.txt");

        using var unzipped = await new ZipManager().Unzip(archive);

        Assert.That(unzipped.Select(f => f.FileName), Is.EqualTo(new[] { "docs/v1..2/notes.txt" }));
    }

    [Test]
    public async Task Unzip_Stops_Past_MaxUnzippedSize()
    {
        var files = CreateFiles();
        var total = files.Sum(f => f.Bytes!.Length);
        await using var archive = new ZipManager().Zip(files);

        await Assert.ThrowsAsync<InvalidDataException>(() => new ZipManager { MaxUnzippedSize = total - 1 }.Unzip(archive));
        archive.Position = 0;
        using var unzipped = await new ZipManager { MaxUnzippedSize = total }.Unzip(archive);
        AssertSameFiles(unzipped.Cast<BinaryFileItem>(), files);
    }

    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
