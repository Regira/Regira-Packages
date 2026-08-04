using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Models.DTO;

namespace Regira.Office.Clients.Services;

public class WordClient(HttpClient client) : OfficeClientBase(client),
    IWordCreator, IWordConverter, IWordMerger, IWordTextExtractor
{
    private const string CreatePath = "word/create";
    private const string ConvertPath = "word/convert";
    private const string MergePath = "word/merge";
    private const string TextPath = "word/txt";

    public async Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var dto = input.ToWordDocumentInputDto();
        return await PostJsonForFileAsync(CreatePath, dto, cancellationToken);
    }

    public Task<IMemoryFile> Convert(WordTemplateInput input, FileFormat format, CancellationToken cancellationToken = default)
        => Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);

    public async Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        var file = await Create(input, cancellationToken);
        var url = BuildConvertUrl(options);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(file.GetBytes()!), "file", "template.docx");
        return await PostMultipartForFileAsync(url, content, cancellationToken);
    }

    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var index = 0;
        foreach (var input in inputs)
        {
            var file = await Create(input, cancellationToken);
            content.Add(new ByteArrayContent(file.GetBytes()!), "files", $"document-{index + 1}.docx");
            index++;
        }
        return await PostMultipartForFileAsync(MergePath, content, cancellationToken);
    }

    public async Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var file = await Create(input, cancellationToken);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(file.GetBytes()!), "file", "document.docx");
        return await PostMultipartForTextAsync(TextPath, content, cancellationToken) ?? string.Empty;
    }

    private static string BuildConvertUrl(ConversionOptions options)
    {
        var parts = new List<string> { $"OutputFormat={options.OutputFormat}" };
        if (options.Settings?.PageSize != null) parts.Add($"PageSize={options.Settings.PageSize}");
        if (options.AutoScaleTables) parts.Add("AutoScaleTables=true");
        if (options.AutoScalePictures) parts.Add("AutoScalePictures=true");
        return $"{ConvertPath}?{string.Join("&", parts)}";
    }
}
