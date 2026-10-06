namespace Regira.Office.Models;

/// <summary>
/// The paper format. A0, A1 and A2 are larger than the 22 inches (1584 pt) Word holds a page to: a PDF or page images
/// take them, but Word misreads a Word document written at them.
/// </summary>
public enum PageSize
{
    A4, // first (-> default)
    A0,
    A1,
    A2,
    A3,
    A5,
    A6,
    A7,
    A8,
    A9,
    A10
}