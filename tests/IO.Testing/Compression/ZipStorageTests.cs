using IO.Testing.Helpers;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Storage;
using Regira.IO.Storage.Compression;

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
    [Ignore("Library defect S6: ZipFileService.List ignores FileSearchObject.Recursive and Type, which BinaryFileService honours")]
    public async Task Filter_By_EntryType() => await StorageTestContext.Test_Filter_By_EntryType();
    [Test]
    [Ignore("Library defect S6: ZipFileService.List ignores FileSearchObject.Recursive and Type, which BinaryFileService honours")]
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

    // either separator reaches the nested folder; Recursive takes its subfolder's file along (Filter_Non_Recursive
    // covers a folder's own files)
    [TestCase("dir2/dir2.1")]
    [TestCase(@"dir2\dir2.1")]
    public async Task List_Nested_Folder(string folderUri)
    {
        var files = await StorageTestContext.FileService.List(new FileSearchObject { FolderUri = folderUri, Recursive = true });

        var expected = StorageTestContext.SourceFiles.Count(x => x.Identifier!.StartsWith(@"dir2\dir2.1\"));
        Assert.That(files.Count(), Is.EqualTo(expected));
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