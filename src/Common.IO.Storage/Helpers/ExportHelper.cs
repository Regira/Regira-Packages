using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage.Helpers;

public interface IExportHelper
{
    Task Export(FileSearchObject so);
}

public class ExportHelper(IFileService sourceService, IFileService targetService) : IExportHelper
{
    public async Task Export(FileSearchObject so)
    {
        var fileUris = await sourceService.List(so);
        foreach (var fileUri in fileUris)
        {
            await using var stream = await sourceService.GetStream(fileUri);
            if (stream != null)
            {
                await targetService.Save(fileUri, stream);
            }
        }
    }
}