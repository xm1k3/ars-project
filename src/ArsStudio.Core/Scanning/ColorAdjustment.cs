using System;

namespace ArsStudio.Core.Scanning;

public readonly record struct ColorAdjustment(double Brightness, double Contrast, double Saturation)
{
    public bool IsIdentity => Brightness == 0 && Contrast == 0 && Saturation == 0;

    public Rgb Apply(Rgb color)
    {
        var r = Level(color.R);
        var g = Level(color.G);
        var b = Level(color.B);
        var gray = 0.299 * r + 0.587 * g + 0.114 * b;
        var factor = SaturationFactor;
        return new Rgb(
            Math.Clamp(gray + (r - gray) * factor, 0, 255),
            Math.Clamp(gray + (g - gray) * factor, 0, 255),
            Math.Clamp(gray + (b - gray) * factor, 0, 255));
    }

    public byte[] BuildLevels()
    {
        var levels = new byte[256];
        for (var value = 0; value < levels.Length; value++)
        {
            levels[value] = (byte)Math.Round(Math.Clamp(Level(value), 0, 255));
        }

        return levels;
    }

    public double SaturationFactor => 1 + Math.Clamp(Saturation, -100, 100) / 100;

    private double Level(double value)
    {
        var contrast = 1 + Math.Clamp(Contrast, -100, 100) / 100;
        return (value - 128) * contrast + 128 + Math.Clamp(Brightness, -100, 100) * 1.28;
    }
}
