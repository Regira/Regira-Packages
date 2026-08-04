using Regira.Office.Models;

namespace Regira.Office.Word.Spire;

public class WordDocumentSettings
{
    public PageSize PageSize { get; set; } = PageSize.A4;
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Portrait;
}