using System.Text;
using Regira.Collections;
using Regira.IO.Models;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.Helpers;

namespace IO.Testing.Helpers;

public static class StorageTestHelper
{
    public interface IStorageTestContext : IAsyncDisposable
    {
        IList<BinaryFileItem> SourceFiles { get; }
        IFileService FileService { get; }
    }
    public class StorageTestContext<T>(T service, string folder, BinaryFileItem[] sourceFiles) : IStorageTestContext
        where T : class, IFileService
    {
        public T FileService { get; init; } = service;
        IFileService IStorageTestContext.FileService => FileService;
        public string SourceFolder { get; init; } = folder;
        public IList<BinaryFileItem> SourceFiles { get; } = sourceFiles;

        public async ValueTask DisposeAsync()
        {
            Directory.Delete(SourceFolder, true);
            SourceFiles.Dispose();

            var allFiles = await FileService.List(new FileSearchObject { Recursive = true });
            var fileCollection = allFiles.ToArray();
            foreach (var file in fileCollection)
            {
                await FileService.Delete(file);
            }

            if (FileService is IDisposable disposableService)
            {
                disposableService.Dispose();
            }
        }
    }

    public static StorageTestContext<T> CreateDecoratedFileService<T>(Func<IList<BinaryFileItem>, string, T> factory)
        where T : class, IFileService
    {
        var sourceFolder = new DirectoryInfo(Path.Combine("Assets", "Input", Guid.NewGuid().ToString("D"))).FullName;
        var sourceFiles = TestFilesCreator.Create(sourceFolder).ToArray();
        return new StorageTestContext<T>(factory(sourceFiles, sourceFolder), sourceFolder, sourceFiles);
    }

    /// <summary>
    /// Polls <paramref name="condition"/> until it returns <c>true</c> or the timeout elapses.
    /// Remote stores such as the GitHub contents API are eventually consistent, so a read taken
    /// immediately after a write or delete can briefly observe the previous state.
    /// </summary>
    public static async Task<bool> Eventually(Func<Task<bool>> condition, int timeoutMs = 5000, int pollMs = 250)
    {
        var start = Environment.TickCount64;
        while (true)
        {
            if (await condition())
                return true;
            if (Environment.TickCount64 - start >= timeoutMs)
                return false;
            await Task.Delay(pollMs);
        }
    }

    // ReSharper disable PossibleMultipleEnumeration
    public static async Task Test_List(this IStorageTestContext ctx)
    {
        var files = await ctx.FileService.List();
        Assert.That(files, Is.Not.Empty);
    }
    public static async Task Test_GetBytes(this IStorageTestContext ctx)
    {
        var fso = new FileSearchObject
        {
            Type = FileEntryTypes.Files,
            FolderUri = "dir2",
            Recursive = true
        };
        var files = await ctx.FileService.List(fso);
        Assert.That(files, Is.Not.Empty);
        foreach (var file in files.Take(1))
        {
            var bytes = await ctx.FileService.GetBytes(file);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes!.Length > 0, Is.True);
            var sourceFileName = Path.GetFileName(file.Split('?').First());
            var sourceFile = ctx.SourceFiles.Single(x => sourceFileName.Equals(x.FileName, StringComparison.InvariantCultureIgnoreCase));
            Assert.That(bytes.Length, Is.EqualTo(sourceFile.Length));
        }
    }
    public static async Task Test_GetInfo(this IStorageTestContext ctx, bool hasLastModified = true)
    {
        var fso = new FileSearchObject
        {
            Type = FileEntryTypes.Files,
            FolderUri = "dir2",
            Recursive = true
        };
        var file = (await ctx.FileService.List(fso)).First();
        var info = await ctx.FileService.GetInfo(file);

        var sourceFileName = Path.GetFileName(file.Split('?').First());
        var sourceFile = ctx.SourceFiles.Single(x => sourceFileName.Equals(x.FileName, StringComparison.InvariantCultureIgnoreCase));
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Length, Is.EqualTo(sourceFile.Length));
        if (hasLastModified)
        {
            Assert.That(info.LastModified, Is.Not.Null.And.LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5)));
            // a local time passes the bound above on a server at UTC or east of it
            Assert.That(info.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
        else
        {
            Assert.That(info.LastModified, Is.Null);
        }
        Assert.That(await ctx.FileService.GetInfo($"dir2/{Guid.NewGuid():N}.txt"), Is.Null);
        Assert.That(await ctx.FileService.GetInfo("dir2"), Is.Null);
    }
    public static async Task Test_Filter_By_Folder(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        var so = new FileSearchObject
        {
            FolderUri = folder,
            Type = FileEntryTypes.Files,
            Recursive = true
        };
        var files = await ctx.FileService.List(so);
        Assert.That(files, Is.Not.Empty);
        var filteredSourceFiles = ctx.SourceFiles.Where(x => x.Identifier!.Contains($"{folder}\\"));
        Assert.That(files.Count(), Is.EqualTo(filteredSourceFiles.Count()));
    }
    public static async Task Test_Filter_By_Extension(this IStorageTestContext ctx)
    {
        var so = new FileSearchObject
        {
            Recursive = true,
            Extensions = [".png", ".txt", ".sql"],
            Type = FileEntryTypes.Files
        };
        var files = await ctx.FileService.List(so);
        Assert.That(files, Is.Not.Empty);
        foreach (var file in files)
        {
            var pathWithoutQuery = file.Split('?').First();
            Assert.That(so.Extensions.Any(e => pathWithoutQuery.EndsWith(e, StringComparison.CurrentCultureIgnoreCase)), Is.True);
        }
    }
    public static async Task Test_Filter_Recursive(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        var so = new FileSearchObject
        {
            FolderUri = folder,
            Recursive = true,
            Type = FileEntryTypes.Files
        };
        var files = await ctx.FileService.List(so);
        Assert.That(files, Is.Not.Empty);
        var filteredSourceFiles = ctx.SourceFiles.Where(x => x.Identifier!.Contains($"{folder}\\"));
        Assert.That(files.Count, Is.EqualTo(filteredSourceFiles.Count()));
    }
    /// <summary>Without <c>Recursive</c> a folder lists its own files only: the file in dir2.1's subfolder stays out.</summary>
    public static async Task Test_Filter_Non_Recursive(this IStorageTestContext ctx)
    {
        var so = new FileSearchObject { FolderUri = "dir2/dir2.1", Recursive = false, Type = FileEntryTypes.Files };

        var files = await ctx.FileService.List(so);

        Assert.That(files.Select(file => file.Split('/', '\\').Last()), Is.EquivalentTo(new[] { "file2.1.1.log", "file2.1.2.txt" }));
    }
    public static async Task Test_Filter_By_EntryType(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        // files
        var so = new FileSearchObject
        {
            FolderUri = folder,
            Recursive = true,
            Type = FileEntryTypes.Files
        };
        var files = await ctx.FileService.List(so);
        Assert.That(files, Is.Not.Empty);
        // folders
        so = new FileSearchObject
        {
            FolderUri = folder,
            Recursive = true,
            Type = FileEntryTypes.Directories
        };
        var folders = await ctx.FileService.List(so);
        Assert.That(folders, Is.Not.Empty);
        // both
        so = new FileSearchObject
        {
            FolderUri = folder,
            Recursive = true,
            Type = FileEntryTypes.All
        };
        var both = await ctx.FileService.List(so);
        Assert.That(both, Is.Not.Empty);
        Assert.That(both, Is.EquivalentTo(folders.Concat(files)));
    }

#if NET10_0_OR_GREATER
    /// <summary><c>ListAsync</c> yields what <c>List</c> returns, in its order, for every kind of filter.</summary>
    public static async Task Test_ListAsync_Yields_What_List_Returns(this IStorageTestContext ctx)
    {
        FileSearchObject?[] searches =
        [
            null,
            new() { FolderUri = "dir2", Type = FileEntryTypes.Files, Recursive = true },
            new() { Recursive = true, Extensions = [".png", ".txt", ".sql"], Type = FileEntryTypes.Files },
            new() { FolderUri = "dir2", Recursive = true, Type = FileEntryTypes.Directories },
            new() { FolderUri = "dir2", Recursive = true, Extensions = [".txt"], Type = FileEntryTypes.All }
        ];
        foreach (var so in searches)
        {
            var listed = (await ctx.FileService.List(so)).ToList();
            var yielded = new List<string>();
            await foreach (var identifier in ctx.FileService.ListAsync(so))
            {
                yielded.Add(identifier);
            }
            Assert.That(yielded, Is.EqualTo(listed), $"{so?.FolderUri} {so?.Type} {string.Join(",", so?.Extensions ?? [])}");
        }
    }
#endif

    public static async Task Test_Add_File(this IStorageTestContext ctx)
    {
        var newIdentifier = "dir2/dir2.2/file2.2.3.txt";
        var newBytes = Encoding.UTF8.GetBytes(newIdentifier);// File Identifier used as it's content
        var savedUri = await ctx.FileService.Save(newIdentifier, newBytes);
        Assert.That(savedUri, Is.Not.Empty);
        var savedBytes = await ctx.FileService.GetBytes(newIdentifier);
        Assert.That(savedBytes, Is.EquivalentTo(newBytes));
    }
    public static async Task Test_Update_File(this IStorageTestContext ctx)
    {
        var identifier = ctx.SourceFiles.Skip(5).First().Identifier!;
        var sourceBytes = await ctx.FileService.GetBytes(identifier);
        var updatedBytes = Encoding.UTF8.GetBytes($"{identifier} updated");
        await ctx.FileService.Save(identifier, updatedBytes);
        var savedBytes = await ctx.FileService.GetBytes(identifier);
        Assert.That(savedBytes, Is.Not.EquivalentTo(sourceBytes!));
        Assert.That(savedBytes, Is.EquivalentTo(updatedBytes));
    }
    public static async Task Test_Update_File_With_Shorter_Content(this IStorageTestContext ctx)
    {
        var identifier = ctx.SourceFiles.Skip(5).First().Identifier!;
        await ctx.FileService.Save(identifier, Encoding.UTF8.GetBytes("long content, written first"));
        var shorterBytes = Encoding.UTF8.GetBytes("short");
        await ctx.FileService.Save(identifier, shorterBytes);
        var savedBytes = await ctx.FileService.GetBytes(identifier);
        Assert.That(savedBytes, Is.EqualTo(shorterBytes), "nothing of the longer content stays behind");
    }
    public static async Task Test_Remove_File(this IStorageTestContext ctx)
    {
        var identifier = ctx.SourceFiles.Skip(5).First().Identifier!;
        var exists = await ctx.FileService.Exists(identifier);
        Assert.That(exists, Is.True);

        await ctx.FileService.Delete(identifier);
        var stillExists = await ctx.FileService.Exists(identifier);
        Assert.That(stillExists, Is.False);
    }


    /// <summary>
    /// Processing folder by folder finds the files one recursive listing does, and, given <paramref name="expected"/>,
    /// exactly those: two empty listings agree with each other as well.
    /// </summary>
    public static async Task Test_Recursive_Directories(this IStorageTestContext ctx, string? folder = null, IEnumerable<string>? expected = null)
    {
        var fileProcessor = new FileProcessor(ctx.FileService);
        var filesWithRecursiveSearchObject = new List<string>();
        await fileProcessor.ProcessFiles(
            new FileSearchObject
            {
                FolderUri = folder,
                Recursive = true
            }, (f, _) => { filesWithRecursiveSearchObject.Add(f); return Task.CompletedTask; },
            false
        );

        var filesFromRecursiveDirectories = new List<string>();
        await fileProcessor.ProcessFiles(
            new FileSearchObject
            {
                FolderUri = folder
            }, (f, _) => { filesFromRecursiveDirectories.Add(f); return Task.CompletedTask; },
            true
        );
        Assert.That(filesFromRecursiveDirectories, Is.EquivalentTo(filesWithRecursiveSearchObject));
        if (expected != null)
        {
            static string Normalize(string uri) => uri.Replace('\\', '/');
            Assert.That(filesWithRecursiveSearchObject.Select(Normalize), Is.EquivalentTo(expected.Select(Normalize)));
        }
    }
    // ReSharper restore PossibleMultipleEnumeration
}