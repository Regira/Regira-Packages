using Regira.Media.Drawing.Constants;
using System.Diagnostics;

namespace Regira.Media.Drawing.Models;

[DebuggerDisplay("Color = [{Red},{Green},{Blue},{Alpha}]")]
public readonly struct Color(byte red = byte.MaxValue, byte green = byte.MaxValue, byte blue = byte.MaxValue, byte? alpha = null) : IEquatable<Color>
{
    public byte Red { get; } = red;
    public byte Green { get; } = green;
    public byte Blue { get; } = blue;
    public byte Alpha { get; } = alpha ?? ImageDefaults.Alpha;

    /// <summary>
    /// Gets the hexadecimal color code representation of the RGB color (without alpha).
    /// </summary>
    public string Hex => $"#{Red:X2}{Green:X2}{Blue:X2}";
    /// <summary>
    /// Gets the hexadecimal color code representation of the RGBA color.
    /// </summary>
    public string HexA => $"#{Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}";

    // ReSharper disable RedundantArgumentDefaultValue
    public static Color Transparent => new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MinValue);
    public static Color White => new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    public static Color Black => new(byte.MinValue, byte.MinValue, byte.MinValue, byte.MaxValue);
    // ReSharper restore RedundantArgumentDefaultValue

    public override string ToString() => HexA;

    public static implicit operator Color(byte[] array)
    {
        if (array == null || (array.Length != 3 && array.Length != 4))
        {
            throw new ArgumentException("Array must have 3 (RGB) or 4 (RGBA) elements.");
        }

        var r = array[0];
        var g = array[1];
        var b = array[2];
        var a = array.Length == 4 ? array[3] : ImageDefaults.Alpha;

        return new Color(r, g, b, a);
    }
    public static implicit operator Color(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return ImageDefaults.BackgroundColor;
        }

        hex = hex.TrimStart('#');

        if (hex.Length is 3 or 4)
        {
            var expanded = "";
            foreach (var c in hex)
            {
                expanded += new string(c, 2);
            }
            hex = expanded;
        }

        if (hex.Length != 6 && hex.Length != 8)
        {
            throw new ArgumentException("Hex color must be RGB, RGBA, RRGGBB, or RRGGBBAA format.", nameof(hex));
        }

        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        var a = hex.Length == 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : ImageDefaults.Alpha;

        return new Color(r, g, b, a);
    }
    public static implicit operator byte[](Color? rgba)
        => [rgba?.Red ?? byte.MaxValue, rgba?.Green ?? byte.MaxValue, rgba?.Blue ?? byte.MaxValue, rgba?.Alpha ?? ImageDefaults.Alpha];
    public static implicit operator string(Color? rgba)
        => (rgba ?? new Color()).ToString();

    // IEquatable
    public bool Equals(Color other)
        => Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha;
    public override bool Equals(object? obj)
        => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue, Alpha);
}