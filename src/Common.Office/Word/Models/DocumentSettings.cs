using Regira.Office.Models;

namespace Regira.Office.Word.Models;

public record DocumentSettings
{
    public PageSize PageSize { get; set; } = PageSize.A4;
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Portrait;
    public Margins? Margins { get; set; }
}