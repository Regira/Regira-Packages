namespace Regira.Office.VCards.Abstractions;

[Flags]
public enum VCardPropertyType
{
    Work = 1,
    Home = 1 << 1
}