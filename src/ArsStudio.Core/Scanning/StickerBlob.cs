using System;

namespace ArsStudio.Core.Scanning;

internal readonly record struct StickerBlob(double X, double Y, double Area)
{
    public double Side => Math.Sqrt(Area);
}
