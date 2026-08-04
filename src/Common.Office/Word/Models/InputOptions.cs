namespace Regira.Office.Word.Models;

public record InputOptions
{
    /// <summary>
    /// Uses the font size and font family of the main template's built-in style 'Normal'
    /// Only applies to paragraphs using a style with a name starting with normal ('Normal', 'Normal(Web)', ...)
    /// </summary>
    public bool InheritFont { get; set; }
    /// <summary>
    /// Only applies to paragraphs using a style with a name starting with normal ('Normal', 'Normal(Web)', ...)
    /// </summary>
    public HorizontalAlignment? HorizontalAlignment { get; set; }
    /// <summary>
    /// Adds an empty page to ensure an even amount of pages
    /// </summary>
    public bool EnforceEvenAmountOfPages { get; set; }
    public bool RemoveEmptyParagraphs { get; set; }
}