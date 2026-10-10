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

    /// <exception cref="NotSupportedException">
    /// <see cref="ConversionOptions.Settings"/> asks for a landscape orientation or for margins, which the API's
    /// conversion does not take
    /// </exception>
    public async Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Settings is { } settings && (settings.PageOrientation != PageOrientation.Portrait || settings.Margins != null))
        {
            throw new NotSupportedException(
                $"The Office API's conversion takes no {nameof(DocumentSettings.PageOrientation)} or {nameof(DocumentSettings.Margins)}: " +
                "it lays every section out in portrait and keeps the document's margins. Convert with an in-process Word backend instead.");
        }

        var file = await Create(input, cancellationToken);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(file.GetBytes()!), "file", "template.docx");
        AddConversionFields(content, options);
        return await PostMultipartForFileAsync(ConvertPath, content, cancellationToken);
    }

    /// <exception cref="NotSupportedException">
    /// One of several inputs has <see cref="InputOptions.EnforceEvenAmountOfPages"/>: the API merges the finished
    /// documents without their options, so neither that input nor the next starts on an odd page as an in-process
    /// merge starts them, and the input's padding page is lost before the next one's new page
    /// </exception>
    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
    {
        var inputList = inputs.ToList();
        if (inputList.Count > 1 && inputList.Any(input => input.Options?.EnforceEvenAmountOfPages == true))
        {
            throw new NotSupportedException(
                $"The Office API merges the finished documents without their options, so an input with {nameof(InputOptions.EnforceEvenAmountOfPages)} " +
                "and the one after it cannot start on an odd page: the input starts on a new page, which throws its page count off, " +
                "and its padding page is lost before the next one's new page. Merge with an in-process Word backend instead.");
        }

        using var content = new MultipartFormDataContent();
        var index = 0;
        foreach (var input in inputList)
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

    // The API binds a WordConversionModel from the form and ignores the query string. It takes no orientation or margins.
    private static void AddConversionFields(MultipartFormDataContent content, ConversionOptions options)
    {
        content.Add(new StringContent(options.OutputFormat.ToString()), nameof(WordConversionModel.OutputFormat));
        if (options.Settings != null) content.Add(new StringContent(options.Settings.PageSize.ToString()), nameof(WordConversionModel.PageSize));
        content.Add(new StringContent(options.AutoScaleTables ? "true" : "false"), nameof(WordConversionModel.AutoScaleTables));
        content.Add(new StringContent(options.AutoScalePictures ? "true" : "false"), nameof(WordConversionModel.AutoScalePictures));
    }
}
