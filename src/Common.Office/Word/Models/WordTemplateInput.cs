using Regira.IO.Abstractions;

namespace Regira.Office.Word.Models;

public class WordTemplateInput
{
    public IMemoryFile Template { get; set; } = null!;
    //public string Filename { get; set; }
    //public Stream TemplateStream { get; set; }
    //public byte[] TemplateBytes { get; set; }

    public IDictionary<string, object>? GlobalParameters { get; set; } = new Dictionary<string, object>();
    public IDictionary<string, ICollection<IDictionary<string, object>>>? CollectionParameters { get; set; } = new Dictionary<string, ICollection<IDictionary<string, object>>>();
    public ICollection<WordImage>? Images { get; set; } = new List<WordImage>();
    public IDictionary<string, WordTemplateInput>? DocumentParameters { get; set; } = new Dictionary<string, WordTemplateInput>();

    public ICollection<WordHeaderFooterInput>? Headers { get; set; } = new List<WordHeaderFooterInput>();
    public ICollection<WordHeaderFooterInput>? Footers { get; set; } = new List<WordHeaderFooterInput>();

    public InputOptions? Options { get; set; } = new();
}