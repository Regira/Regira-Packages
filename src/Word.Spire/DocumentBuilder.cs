using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Models;
using Spire.Doc;

namespace Regira.Office.Word.Spire;

public class DocumentBuilder(WordService manager)
{
    /// <summary>
    /// Keeps code compiled against the <see cref="WordManager"/> signature running.
    /// </summary>
    [Obsolete("Use the WordService constructor instead", false)]
    public DocumentBuilder(WordManager manager) : this((WordService)manager)
    {
    }

    private WordDocumentSettings? _settings;
    private WordTemplateInput[]? _inputs;
    private ConversionOptions? _conversionOptions;
    private MergeOptions? _mergeOptions;
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
    /// <summary>
    /// How the inputs <see cref="Load"/> takes are joined, as <see cref="WordService.Merge(IEnumerable{WordTemplateInput}, MergeOptions?, CancellationToken)"/>
    /// joins them; by default each starts on a new page.
    /// </summary>
    public DocumentBuilder WithMerge(MergeOptions options)
    {
        _mergeOptions = options;
        return this;
    }


    public async Task<IMemoryFile> Build()
    {
        // Create Document
        using var doc = _inputs != null
            ? await manager.MergeDocuments(_inputs, _mergeOptions)
            : new Document();

        // PageSettings
        if (_settings != null)
        {
            if (doc.Sections.Count == 0)
            {
                doc.AddSection();
            }
            foreach (Section section in doc.Sections)
            {
                manager.SetPageSetup(section.PageSetup, _settings.PageSize, _settings.PageOrientation);
            }
        }

        // Paragraphs
        if (_paragraphs?.Any() ?? false)
        {
            manager.AddParagraphs(doc, _paragraphs);
        }

        // Headers
        if (_headers?.Any() ?? false)
        {
            foreach (var headerInput in _headers)
            {
                using var headerDoc = manager.CreateDocument(headerInput.Template);
                manager.AddHeader(doc, headerDoc, headerInput.Type);
            }
        }
        // Footers
        if (_footers?.Any() ?? false)
        {
            foreach (var footerInput in _footers)
            {
                using var footerDoc = manager.CreateDocument(footerInput.Template);
                manager.AddFooter(doc, footerDoc, footerInput.Type);
            }
        }

        // ConversionOptions
        _conversionOptions ??= new ConversionOptions();
        var stream = manager.ConvertDocument(doc, _conversionOptions);
        return stream.ToMemoryFile(WordContentTypes.Of(_conversionOptions.OutputFormat));
    }
}