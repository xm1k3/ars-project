using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public sealed record DetectedFace(IReadOnlyList<ScanPoint> Centers, IReadOnlyList<bool> Found, double Spacing)
{
    private const double SampleRatio = 0.35;

    public int FoundCount => Found.Count(found => found);

    public PixelRect SampleArea(int index)
    {
        var size = Math.Max(1, (int)Math.Round(Spacing * SampleRatio));
        var center = Centers[index];
        return new PixelRect(
            (int)Math.Round(center.X - size / 2.0),
            (int)Math.Round(center.Y - size / 2.0),
            size,
            size);
    }
}
