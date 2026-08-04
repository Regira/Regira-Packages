using Regira.IO.Abstractions;

namespace Regira.IO.Models;

public class BinaryFileCollection(IEnumerable<IBinaryFile> items) : List<IBinaryFile>(items), IDisposable
{
    public void Dispose()
    {
        foreach (var fileItem in this)
        {
            fileItem.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}