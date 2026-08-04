namespace Regira.Office.Word.Models.DTO;

public class WordHeaderFooterModel
{
    public WordDocumentInputDto Template { get; set; } = null!;
    public HeaderFooterType Type { get; set; }
}
public class WordDocumentInputDto
{
    public byte[] TemplateBytes { get; set; } = null!;
    public IDictionary<string, object>? GlobalParameters { get; set; }
    public IDictionary<string, ICollection<IDictionary<string, object>>>? CollectionParameters { get; set; }
    public ICollection<WordImageModel>? Images { get; set; }
    public IDictionary<string, WordDocumentInputDto>? DocumentParameters { get; set; }
    public ICollection<WordHeaderFooterModel>? Headers { get; set; }
    public ICollection<WordHeaderFooterModel>? Footers { get; set; }
}