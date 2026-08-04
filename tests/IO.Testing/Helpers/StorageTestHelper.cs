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
    public static async Task Test_ListAsync(this IStorageTestContext ctx)
    {
        var files = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync())
            files.Add(f);
        Assert.That(files, Is.Not.Empty);
    }
    public static async Task Test_Filter_By_Folder_Async(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        var so = new FileSearchObject
        {
            FolderUri = folder,
            Type = FileEntryTypes.Files,
            Recursive = true
        };
        var files = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            files.Add(f);
        Assert.That(files, Is.Not.Empty);
        var filteredSourceFiles = ctx.SourceFiles.Where(x => x.Identifier!.Contains($"{folder}\\"));
        Assert.That(files.Count, Is.EqualTo(filteredSourceFiles.Count()));
    }
    public static async Task Test_Filter_By_Extension_Async(this IStorageTestContext ctx)
    {
        var so = new FileSearchObject
        {
            Recursive = true,
            Extensions = [".png", ".txt", ".sql"],
            Type = FileEntryTypes.Files
        };
        var files = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            files.Add(f);
        Assert.That(files, Is.Not.Empty);
        foreach (var file in files)
        {
            var pathWithoutQuery = file.Split('?').First();
            Assert.That(so.Extensions.Any(e => pathWithoutQuery.EndsWith(e, StringComparison.CurrentCultureIgnoreCase)), Is.True);
        }
    }
    public static async Task Test_Filter_Recursive_Async(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        var so = new FileSearchObject
        {
            FolderUri = folder,
            Recursive = true,
            Type = FileEntryTypes.Files
        };
        var files = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            files.Add(f);
        Assert.That(files, Is.Not.Empty);
        var filteredSourceFiles = ctx.SourceFiles.Where(x => x.Identifier!.Contains($"{folder}\\"));
        Assert.That(files.Count, Is.EqualTo(filteredSourceFiles.Count()));
    }
    public static async Task Test_Filter_By_EntryType_Async(this IStorageTestContext ctx)
    {
        var folder = "dir2";
        var so = new FileSearchObject { FolderUri = folder, Recursive = true, Type = FileEntryTypes.Files };
        var files = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            files.Add(f);
        Assert.That(files, Is.Not.Empty);

        so = new FileSearchObject { FolderUri = folder, Recursive = true, Type = FileEntryTypes.Directories };
        var folders = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            folders.Add(f);
        Assert.That(folders, Is.Not.Empty);

        so = new FileSearchObject { FolderUri = folder, Recursive = true, Type = FileEntryTypes.All };
        var both = new List<string>();
        await foreach (var f in ctx.FileService.ListAsync(so))
            both.Add(f);
        Assert.That(both, Is.Not.Empty);
        Assert.That(both, Is.EquivalentTo(folders.Concat(files)));
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
    public static async Task Test_Remove_File(this IStorageTestContext ctx)
    {
        var identifier = ctx.SourceFiles.Skip(5).First().Identifier!;
        var exists = await ctx.FileService.Exists(identifier);
        Assert.That(exists, Is.True);

        await ctx.FileService.Delete(identifier);
        var stillExists = await ctx.FileService.Exists(identifier);
        Assert.That(stillExists, Is.False);
    }


    public static async Task Test_Recursive_Directories(this IStorageTestContext ctx, string? folder = null)
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
    }
    public static async Task Test_Recursive_DirectoriesAsync(this IStorageTestContext ctx, string? folder = null)
    {
        var fileProcessor = new FileProcessor(ctx.FileService);
        var filesWithRecursiveSearchObject = new List<string>();
        await fileProcessor.ProcessFiles(
            new FileSearchObject
            {
                FolderUri = folder,
                Recursive = true
            },
            (f, _) => { filesWithRecursiveSearchObject.Add(f); return Task.CompletedTask; },
            false
        );

        var filesFromRecursiveDirectories = new List<string>();
        await fileProcessor.ProcessFiles(
            new FileSearchObject
            {
                FolderUri = folder
            },
            (f, _) => { filesFromRecursiveDirectories.Add(f); return Task.CompletedTask; },
            true
        );
        Assert.That(filesFromRecursiveDirectories, Is.EquivalentTo(filesWithRecursiveSearchObject));
    }
    // ReSharper restore PossibleMultipleEnumeration
}