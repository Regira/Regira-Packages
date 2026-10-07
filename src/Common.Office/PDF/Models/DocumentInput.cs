using Regira.IO.Abstractions;
using Regira.Office.Models;

namespace Regira.Office.PDF.Models;

/// <summary>
/// An office document to convert to PDF. The page settings override the document's own page setup; each one left
/// empty keeps what the document says.
/// </summary>
public class DocumentInput
{
    /// <summary>
    /// The document to convert.
    /// </summary>
    public IMemoryFile Document { get; set; } = null!;
    /// <summary>
    /// Paper size of every page.
    /// </summary>
    public PageSize? Format { get; set; }
    /// <summary>
    /// Orientation of every page.
    /// </summary>
    public PageOrientation? Orientation { get; set; }
    /// <summary>
    /// Page margins in points.
    /// </summary>
    public Margins? Margins { get; set; }
}
