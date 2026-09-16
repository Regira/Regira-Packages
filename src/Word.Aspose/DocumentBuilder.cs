using Aspose.Words;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Word.Models;
using Paragraph = Regira.Office.Word.Models.Paragraph;

namespace Regira.Office.Word.Aspose;

public class DocumentBuilder(WordService service)
{
    private WordDocumentSettings? _settings;
    private WordTemplateInput[]? _inputs;
    private ConversionOptions? _conversionOptions;
    private IEnumerable<Paragraph>? _paragraphs;
    private ICollection<WordHeaderFooterInput>? _headers;
    private ICollection<WordHeaderFooterInput>? _footers;


    public DocumentBuilder Load(params WordTemplateInput[] inputs)
    {
        _inputs = inputs;
        return this;
    }
    public DocumentBuilder WithSettings(WordDocumentSettings settings)
    {
        _settings = settings;
        return this;
    }
    public DocumentBuilder WithParagraphs(IEnumerable<Paragraph> paragraphs)
    {
        _paragraphs = paragraphs;
        return this;
    }
    public DocumentBuilder AddHeader(WordHeaderFooterInput header)
    {
        _headers ??= new List<WordHeaderFooterInput>();
        _headers.Add(header);
        return this;
    }
    public DocumentBuilder AddFooter(WordHeaderFooterInput footer)
    {
        _footers ??= new List<WordHeaderFooterInput>();
        _footers.Add(footer);
        return this;
    }
    public DocumentBuilder WithConversion(ConversionOptions options)
    {
        _conversionOptions = options;
        return this;
    }


    public async Task<IMemoryFile> Build()
    {
        var doc = _inputs != null
            ? await service.MergeDocuments(_inputs)
            : new Document();

        // PageSettings
        if (_settings != null)
        {
            if (doc.Sections.Count == 0)
            {
                doc.EnsureMinimum();
            }
            foreach (var section in doc.Sections.OfType<Section>())
            {
                service.SetPageSetup(section.PageSetup, _settings.PageSize, _settings.PageOrientation);
            }
        }

        // Paragraphs
        if (_paragraphs?.Any() ?? false)
        {
            service.AddParagraphs(doc, _paragraphs);
        }

        // Headers
        if (_headers?.Any() ?? false)
        {
            foreach (var headerInput in _headers)
            {
                service.AddHeader(doc, service.CreateDocument(headerInput.Template), headerInput.Type);
            }
        }
        // Footers
        if (_footers?.Any() ?? false)
        {
            foreach (var footerInput in _footers)
            {
                service.AddFooter(doc, service.CreateDocument(footerInput.Template), footerInput.Type);
            }
        }

        _conversionOptions ??= new ConversionOptions();
        var stream = service.ConvertDocument(doc, _conversionOptions);
        return stream.ToMemoryFile(WordService.GetContentType(_conversionOptions.OutputFormat));
    }
}
