using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage.Helpers;

public class FileProcessor(IFileService fileService) : IFileProcessor
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="so"></param>
    /// <param name="handleFile"></param>
    /// <param name="processRecursively">
    ///     Set true when all subfolders should be processed, but not at once.
    ///     When true, overwrites the Recursive property to false in the FileSearchObject, but calls this function recursively for each subfolder.
    /// </param>
    /// <returns></returns>
    public async Task ProcessFiles(FileSearchObject so, Func<string, IFileService, Task> handleFile, bool? processRecursively = null)
    {
        var recursive = processRecursively ?? false ? !processRecursively.Value : so.Recursive;
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
        {
            var absoluteUri = fileService.GetAbsoluteUri(uri);
            await handleFile(absoluteUri, fileService);
        }
#else
        var fileUris = await fileService.List(filesSo);
        // handle file
        var handleFilesFuncs = fileUris
            .Select(fileUri =>
            {
                var absoluteUri = fileService.GetAbsoluteUri(fileUri);
                return handleFile(absoluteUri, fileService);
            })
            .ToArray();
        await Task.WhenAll(handleFilesFuncs);
#endif

        // recursive
        if (processRecursively ?? so.Recursive)
        {
            // directories
            var dirSo = new FileSearchObject
            {
                FolderUri = so.FolderUri,
                Type = FileEntryTypes.Directories,
                Recursive = recursive
            };
#if NET10_0_OR_GREATER
            await foreach (var uri in fileService.ListAsync(dirSo))
            {
                var absoluteUri = fileService.GetAbsoluteUri(uri);
                await ProcessFiles(new FileSearchObject { FolderUri = absoluteUri, Extensions = so.Extensions, Recursive = recursive }, handleFile, processRecursively);
            }
#else
            var directoryUris = await fileService.List(dirSo);
            var dirTasks = directoryUris
                .Select(dirUri => ProcessFiles(new FileSearchObject { FolderUri = dirUri, Extensions = so.Extensions, Recursive = so.Recursive }, handleFile, processRecursively))
                .ToArray();
            await Task.WhenAll(dirTasks);
#endif
        }
    }
}