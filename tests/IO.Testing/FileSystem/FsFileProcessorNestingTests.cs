using Regira.IO.Storage;
using Regira.IO.Storage.FileSystem;
using Regira.IO.Storage.Helpers;

namespace IO.Testing.FileSystem;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FsFileProcessorNestingTests
{
    private string _root = null!;
    private FileProcessor _processor = null!;
    private string[] _allFiles = null!;
    private string[] _topFiles = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "regira-file-processor-" + Guid.NewGuid().ToString("N"));
        var files = new[] { "a.txt", "b.txt", Path.Combine("d1", "c.txt"), Path.Combine("d1", "d2", "d.txt"), Path.Combine("d3", "e.txt") };
        foreach (var file in files)
        {
            var path = Path.Combine(_root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file);
        }
        _allFiles = files.Select(f => Path.Combine(_root, f)).ToArray();
        _topFiles = _allFiles.Take(2).ToArray();
        _processor = new FileProcessor(new BinaryFileService(new FileSystemOptions { RootFolder = _root }));
    }
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private async Task<List<string>> Process(bool recursive, bool? processRecursively)
    {
        var handled = new List<string>();
        var running = 0;
        await _processor.ProcessFiles(new FileSearchObject { Recursive = recursive }, async (uri, _) =>
        {
            Assert.That(Interlocked.Increment(ref running), Is.EqualTo(1), "one file at a time");
            await Task.Delay(5);
            lock (handled)
            {
                handled.Add(uri);
            }
            Interlocked.Decrement(ref running);
        }, processRecursively);
        return handled;
    }

    [Test]
    public async Task Recursive_SearchObject_Handles_Every_File_Once()
    {
        var handled = await Process(recursive: true, processRecursively: null);
        Assert.That(handled, Is.EquivalentTo(_allFiles));
    }
    [Test]
    public async Task Recursive_SearchObject_Without_Folder_Recursion_Handles_Every_File_Once()
    {
        var handled = await Process(recursive: true, processRecursively: false);
        Assert.That(handled, Is.EquivalentTo(_allFiles));
    }
    [Test]
    public async Task Folder_Recursion_Handles_Every_File_Once()
    {
        var handled = await Process(recursive: false, processRecursively: true);
        Assert.That(handled, Is.EquivalentTo(_allFiles));
    }
    [Test]
    public async Task Folder_Recursion_Ignores_A_Recursive_SearchObject()
    {
        var handled = await Process(recursive: true, processRecursively: true);
        Assert.That(handled, Is.EquivalentTo(_allFiles));
    }
    [Test]
    public async Task Flat_SearchObject_Handles_The_Top_Folder_Only()
    {
        var handled = await Process(recursive: false, processRecursively: null);
        Assert.That(handled, Is.EquivalentTo(_topFiles));
    }
}
