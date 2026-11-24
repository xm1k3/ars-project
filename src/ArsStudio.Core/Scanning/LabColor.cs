using System;

namespace ArsStudio.Core.Scanning;

public readonly record struct LabColor(double L, double A, double B)
{
    private const double LightnessWeight = 0.6;

    public static LabColor FromRgb(Rgb color)
    {
        var r = Linear(color.R);
        var g = Linear(color.G);
        var b = Linear(color.B);

        var x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
        var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        var z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;

        var fx = Pivot(x);
        var fy = Pivot(y);
        var fz = Pivot(z);
        return new LabColor(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    public double DistanceTo(LabColor other)
    {
        var l = (L - other.L) * LightnessWeight;
        var a = A - other.A;
        var b = B - other.B;
        return Math.Sqrt(l * l + a * a + b * b);
    }

    private static double Linear(double channel)
    {
        var value = Math.Clamp(channel, 0, 255) / 255;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Pivot(double value) =>
        value > 216.0 / 24389 ? Math.Cbrt(value) : (24389.0 / 27 * value + 16) / 116;
}
