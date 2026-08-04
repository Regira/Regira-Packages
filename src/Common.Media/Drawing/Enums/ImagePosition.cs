namespace Regira.Media.Drawing.Enums;

[Flags]
public enum ImagePosition
{
    Absolute = 1 << 0,
    Left = 1 << 1,
    Top = 1 << 2,
    Right = 1 << 3,
    Bottom = 1 << 4,
    HCenter = 1 << 5,
    VCenter = 1 << 6
}