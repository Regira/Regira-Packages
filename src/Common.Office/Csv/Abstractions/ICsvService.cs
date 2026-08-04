using Regira.IO.Abstractions;
using Regira.Office.Csv.Models;

namespace Regira.Office.Csv.Abstractions;

public interface ICsvService : ICsvService<IDictionary<string, object>>;
public interface ICsvService<T>
{
    Task<List<T>> Read(string input, CsvOptions? options = null, CancellationToken cancellationToken = default);
    Task<List<T>> Read(IBinaryFile input, CsvOptions? options = null, CancellationToken cancellationToken = default);

    Task<string> Write(IEnumerable<T> items, CsvOptions? options = null, CancellationToken cancellationToken = default);
    Task<IMemoryFile> WriteFile(IEnumerable<T> items, CsvOptions? options = null, CancellationToken cancellationToken = default);
}