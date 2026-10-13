using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage.Helpers;

public class FileProcessor(IFileService fileService) : IFileProcessor
{
    /// <summary>
    /// Calls <paramref name="handleFile"/> for every file the search object lists, one file at a time, with the file's absolute URI.
    /// </summary>
    /// <param name="so">The folder, extensions and <see cref="FileSearchObject.Recursive"/> flag to list files with.</param>
    /// <param name="handleFile">Handles one file. A caller that wants files handled in parallel adds that inside it.</param>
    /// <param name="processRecursively">
    ///     Set true when all subfolders should be processed, but not at once.
    ///     When true, overrides the Recursive property to false in the FileSearchObject, and calls this function recursively for each subfolder.
    ///     Otherwise, the search object's <see cref="FileSearchObject.Recursive"/> flag decides whether files in subfolders are listed, in one listing.
    /// </param>
    public async Task ProcessFiles(FileSearchObject so, Func<string, IFileService, Task> handleFile, bool? processRecursively = null)
    {
        var processFolders = processRecursively == true;
        var recursive = !processFolders && so.Recursive;
        // files
        var filesSo = new FileSearchObject
        {
            FolderUri = so.FolderUri,
            Extensions = so.Extensions,
            Recursive = recursive,
            Type = FileEntryTypes.Files
        };
#if NET10_0_OR_GREATER
        await foreach (var uri in fileService.ListAsync(filesSo))
#else
        foreach (var uri in await fileService.List(filesSo))
#endif
        {
            var absoluteUri = fileService.GetAbsoluteUri(uri);
            await handleFile(absoluteUri, fileService);
        }

        // recursive
        if (processFolders)
        {
            // directories
            var dirSo = new FileSearchObject
            {
                FolderUri = so.FolderUri,
                Type = FileEntryTypes.Directories,
                Recursive = false
            };
#if NET10_0_OR_GREATER
            await foreach (var uri in fileService.ListAsync(dirSo))
#else
            foreach (var uri in await fileService.List(dirSo))
#endif
            {
                var absoluteUri = fileService.GetAbsoluteUri(uri);
                await ProcessFiles(new FileSearchObject { FolderUri = absoluteUri, Extensions = so.Extensions, Recursive = false }, handleFile, processRecursively);
            }
        }
    }
}
