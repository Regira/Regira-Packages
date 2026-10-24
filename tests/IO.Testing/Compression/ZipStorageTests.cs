using IO.Testing.Helpers;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Models;
using Regira.IO.Storage.Compression;
using System.IO.Compression;

namespace IO.Testing.Compression;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ZipStorageTests
{
    protected IMemoryFile SourceZip { get; set; } = null!;
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; }

    [SetUp]
    public void Setup()
    {
        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((files, _) =>
        {
            SourceZip = files.Zip();
            return new ZipFileService(new ZipFileCommunicator { SourceFile = SourceZip });
        });
    }
    [TearDown]
    public async Task TearDown()
    {
        await StorageTestContext.DisposeAsync();
        SourceZip.Dispose();
    }

    [Test]
    public async Task List() => await StorageTestContext.Test_List();
    [Test]
    public async Task GetBytes() => await StorageTestContext.Test_GetBytes();
    [Test]
    public async Task GetInfo() => await StorageTestContext.Test_GetInfo();
    [Test]
    public async Task Filter_By_Folder() => await StorageTestContext.Test_Filter_By_Folder();
    [Test]
    public async Task Filter_By_Extension() => await StorageTestContext.Test_Filter_By_Extension();
    [Test]
    public async Task Filter_Recursive() => await StorageTestContext.Test_Filter_Recursive();
    [Test]
    public async Task Filter_By_EntryType() => await StorageTestContext.Test_Filter_By_EntryType();
    [Test]
    public async Task Filter_Non_Recursive() => await StorageTestContext.Test_Filter_Non_Recursive();

#if NET10_0_OR_GREATER
    [Test]
    public async Task ListAsync_Yields_What_List_Returns() => await StorageTestContext.Test_ListAsync_Yields_What_List_Returns();
#endif

    [Test]
    public async Task Add_File() => await StorageTestContext.Test_Add_File();
    [Test]
    public async Task Update_File() => await StorageTestContext.Test_Update_File();
    [Test]
    public async Task Update_File_With_Shorter_Content() => await StorageTestContext.Test_Update_File_With_Shorter_Content();
    [Test]
    public async Task Remove_File() => await StorageTestContext.Test_Remove_File();

    [Test]
    public async Task Move_File()
    {
        var identifier = StorageTestContext.SourceFiles.First().Identifier!.Replace('\\', '/');
        var expected = await StorageTestContext.FileService.GetBytes(identifier);
        var target = $"moved/{Path.GetFileName(identifier)}";

        await StorageTestContext.FileService.Move(identifier, target);

        Assert.That(await StorageTestContext.FileService.Exists(identifier), Is.False);
        Assert.That(await StorageTestContext.FileService.GetBytes(target), Is.EqualTo(expected));
    }

    // a target naming the same entry, whatever its separators, leaves it where it is
    [TestCase(false)]
    [TestCase(true)]
    public async Task Move_Onto_The_Same_Entry_Keeps_It(bool backslashes)
    {
        var identifier = StorageTestContext.SourceFiles.First(x => x.Identifier!.Contains('\\') || x.Identifier.Contains('/')).Identifier!.Replace('\\', '/');
        var expected = await StorageTestContext.FileService.GetBytes(identifier);

        await StorageTestContext.FileService.Move(backslashes ? identifier.Replace('/', '\\') : identifier, identifier);

        Assert.That(await StorageTestContext.FileService.GetBytes(identifier), Is.EqualTo(expected));
    }


    [Test]
    public Task GetStream_With_Forward_Slashes() => GetStream_Of_A_Nested_File(identifier => identifier.Replace('\\', '/'));

    // sorted by identifier, as the file system service lists: each folder before what lies in it
    [Test]
    public async Task List_Is_Sorted_By_Identifier()
    {
        var listed = await StorageTestContext.FileService.List(new FileSearchObject { Recursive = true });

        Assert.That(listed, Is.EqualTo(new[]
        {
            "dir1", "dir1/file1.1.txt", "dir1/file1.2.txt",
            "dir2", "dir2/dir2.1", "dir2/dir2.1/dir2.1.1", "dir2/dir2.1/dir2.1.1/file2.1.1.1.txt",
            "dir2/dir2.1/file2.1.1.log", "dir2/dir2.1/file2.1.2.txt",
            "dir2/dir2.2", "dir2/dir2.2/file2.2.1.log", "dir2/dir2.2/file2.2.2.text",
            "dir3", "dir3/file3.1.txt",
            "file1.txt", "file2.txt"
        }));
    }

    // either separator reaches the nested folder; Recursive takes its subfolder's file along (Filter_Non_Recursive
    // covers a folder's own files)
    [TestCase("dir2/dir2.1")]
    [TestCase(@"dir2\dir2.1")]
    public async Task List_Nested_Folder(string folderUri)
    {
        var files = await StorageTestContext.FileService.List(new FileSearchObject { FolderUri = folderUri, Recursive = true, Type = FileEntryTypes.Files });

        var expected = StorageTestContext.SourceFiles.Count(x => x.Identifier!.StartsWith(@"dir2\dir2.1\"));
        Assert.That(files.Count(), Is.EqualTo(expected));
    }

    // The archive is open for update, where opening an entry inflates it into memory: GetInfo reads the size the
    // archive records. An entry opened before (read, or written by Save) answers from its stream, with the same size.
    [Test]
    public async Task GetInfo_Reads_The_Size_Without_Inflating_The_Entry()
    {
        const int size = 16 * 1024 * 1024;
        using var zip = new[] { new BinaryFileItem { FileName = "big.bin", Bytes = new byte[size] } }.Zip();
        using var service = new ZipFileService(new ZipFileCommunicator { SourceFile = zip });
        Assert.That(await service.Exists("big.bin"), Is.True);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var info = await service.GetInfo("big.bin");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(info!.Length, Is.EqualTo(size));
        Assert.That(allocated, Is.LessThan(size / 16));
        Assert.That((await service.GetBytes("big.bin"))!.Length, Is.EqualTo(size));
        Assert.That((await service.GetInfo("big.bin"))!.Length, Is.EqualTo(size));
        await service.Save("big.bin", new byte[7]);
        Assert.That((await service.GetInfo("big.bin"))!.Length, Is.EqualTo(7));
    }

    // an archive another tool wrote may hold an entry for a folder, which is no file
    [Test]
    public async Task GetInfo_Of_A_Folder_Entry_Is_Null()
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("dir/");
            await using var entry = archive.CreateEntry("dir/file.txt").Open();
            entry.Write("text"u8);
        }
        using var service = new ZipFileService(new ZipFileCommunicator { SourceFile = zip.ToMemoryFile() });

        Assert.That(await service.GetInfo("dir/"), Is.Null);
        Assert.That(await service.GetInfo("dir"), Is.Null);
        Assert.That((await service.GetInfo("dir/file.txt"))!.Length, Is.EqualTo(4));
    }

    [Test]
    public Task GetStream_With_Backward_Slashes() => GetStream_Of_A_Nested_File(identifier => identifier.Replace('/', '\\'));

    // a file in a folder, so the separator is part of the identifier
    private async Task GetStream_Of_A_Nested_File(Func<string, string> spell)
    {
        var source = StorageTestContext.SourceFiles.First(x => x.Identifier!.Contains('\\'));
        var identifier = spell(source.Identifier!);

        Assert.That(await StorageTestContext.FileService.Exists(identifier), Is.True);
        await using var stream = await StorageTestContext.FileService.GetStream(identifier);
        using var read = new MemoryStream();
        await stream!.CopyToAsync(read);
        Assert.That(read.ToArray(), Is.EqualTo(source.GetBytes()));
    }
}