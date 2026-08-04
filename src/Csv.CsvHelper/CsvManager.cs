using CsvHelper;
using CsvHelper.Configuration;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Csv.Abstractions;
using Regira.Office.Csv.Models;
using Regira.Office.MimeTypes;
using Regira.Utilities;

namespace Regira.Office.Csv.CsvHelper;

public class CsvManager(CsvHelperOptions? options = null) : CsvManager<IDictionary<string, object>>(options), ICsvService
{
    protected internal override async Task<List<IDictionary<string, object>>> Read(TextReader tr, CsvOptions? options = null)
    {
        using var csvReader = GetReader(tr, options);

        // headers
        var keys = new List<string>();
        await csvReader.ReadAsync();
        int numberOfKeys = 0;
        while (csvReader.TryGetField<string>(numberOfKeys++, out var header))
        {
            keys.Add(header!);
        }

        var items = new List<IDictionary<string, object>>();

        while (await csvReader.ReadAsync())
        {
            var dic = new Dictionary<string, object>();
            for (var i = 0; i < keys.Count; i++)
            {
                if (csvReader.TryGetField<string>(i, out var value))
                {
                    var key = keys[i];
                    dic[key] = value!;
                }
            }
            items.Add(dic);
        }

        return items;
    }
    protected internal override async Task Write(TextWriter tw, IEnumerable<IDictionary<string, object>> items, CsvOptions? options = null)
    {
        await using var csvWriter = GetWriter(tw, options);

        var itemList = items.AsList();
        var keys = itemList.SelectMany(x => x.Keys).Distinct().ToArray();

        foreach (var key in keys)
        {
            csvWriter.WriteField(key);
        }
        foreach (var item in itemList)
        {
            await csvWriter.NextRecordAsync();
            foreach (var key in keys)
            {
                csvWriter.WriteField(item.TryGetValue(key, out var value) ? value : string.Empty);
            }
        }
    }
}
public class CsvManager<T>(CsvHelperOptions? defaultOptions = null) : ICsvService<T>
{
    private CsvHelperOptions? _defaultOptions = defaultOptions;

    public async Task<List<T>> Read(string input, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var sr = new StringReader(input);
        return await Read(sr, options);
    }
    public async Task<List<T>> Read(IBinaryFile input, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var ms = input.GetStream() ?? throw new Exception("Could not get contents of file");
        using var sr = new StreamReader(ms);
        return await Read(sr, options);
    }
    protected internal virtual async Task<List<T>> Read(TextReader tr, CsvOptions? options = null)
    {
        using var csvReader = GetReader(tr, options);

        var records = csvReader.GetRecordsAsync<T>();
        return await records.ToListAsync();
    }


    public async Task<string> Write(IEnumerable<T> items, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        await using var sw = new StringWriter();
        await Write(sw, items, options);
        return sw.ToString();
    }
    public async Task<IMemoryFile> WriteFile(IEnumerable<T> items, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        // create a MemoryStream to avoid error "Cannot access a closed Stream"
        var ms = new MemoryStream();
        var sw = new StreamWriter(ms);

        await Write(sw, items, options);

        ms.Seek(0, SeekOrigin.Begin);
        return ms.ToMemoryFile(ContentTypes.CSV);
    }
    protected internal virtual async Task Write(TextWriter tw, IEnumerable<T> items, CsvOptions? options = null)
    {
        await using var csvWriter = GetWriter(tw, options);
        await csvWriter.WriteRecordsAsync(items);
    }

    protected CsvReader GetReader(TextReader tr, CsvOptions? options) => new(tr, GetConfiguration(options), true);
    protected CsvWriter GetWriter(TextWriter tw, CsvOptions? options) => new(tw, GetConfiguration(options), true);
    protected CsvConfiguration GetConfiguration(CsvOptions? options)
    {
        _defaultOptions ??= new CsvHelperOptions();
        var config = new CsvConfiguration(options?.Culture ?? _defaultOptions.Culture)
        {
            TrimOptions = _defaultOptions.PreserveWhitespace ? TrimOptions.None : TrimOptions.Trim,
            Delimiter = options?.Delimiter ?? _defaultOptions.Delimiter,
        };

        if (_defaultOptions.IgnoreBadData)
        {
            config.BadDataFound = null;
        }

        return config;
    }
}