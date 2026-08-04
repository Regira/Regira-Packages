namespace Regira.Office.VCards.Abstractions;

[Flags]
public enum VCardTelType
{
    Voice = 1,
    Text = 1 << 1,
    Cell = 1 << 2,
    Video = 1 << 3,
    Work = 1 << 4,
    Home = 1 << 5
}