using Regira.Dimensions;

namespace Regira.Media.Drawing.Utilities;

public static class RotateUtility
{
    public static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
    public static double ToDegrees(double radians)
    {
        return radians * 180 / Math.PI;
    }

    public static Size2D CalculateSize(Size2D sourceSize, double angleDegrees)
    {
        var radians = ToRadians(angleDegrees);
        var sine = (float)Math.Abs(Math.Sin(radians));
        var cosine = (float)Math.Abs(Math.Cos(radians));
        var originalWidth = sourceSize.Width;
        var originalHeight = sourceSize.Height;
        var rotatedWidth = (int)(cosine * originalWidth + sine * originalHeight);
        var rotatedHeight = (int)(cosine * originalHeight + sine * originalWidth);
        return new[] { rotatedWidth, rotatedHeight };
    }
}