using System;
using System.Collections.Generic;

namespace ArsStudio.Core.Scanning;

public sealed class FrameImage
{
    public const int BytesPerPixel = 4;

    public FrameImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height * BytesPerPixel)
        {
            throw new ArgumentException("Dimensioni del fotogramma non valide.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public static FrameImage FromBgra(ReadOnlySpan<byte> data, int width, int height, int stride, int maxWidth)
    {
        var step = Math.Max(1, (width + maxWidth - 1) / maxWidth);
        var outputWidth = width / step;
        var outputHeight = height / step;
        var pixels = new byte[outputWidth * outputHeight * BytesPerPixel];

        for (var y = 0; y < outputHeight; y++)
        {
            var source = data.Slice(y * step * stride, width * BytesPerPixel);
            var target = y * outputWidth * BytesPerPixel;
            for (var x = 0; x < outputWidth; x++)
            {
                var pixel = x * step * BytesPerPixel;
                pixels[target] = source[pixel];
                pixels[target + 1] = source[pixel + 1];
                pixels[target + 2] = source[pixel + 2];
                pixels[target + 3] = 255;
                target += BytesPerPixel;
            }
        }

        return new FrameImage(outputWidth, outputHeight, pixels);
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public Rgb Median(PixelRect area)
    {
        var left = Math.Clamp(area.X, 0, Width - 1);
        var top = Math.Clamp(area.Y, 0, Height - 1);
        var right = Math.Clamp(area.X + area.Width, left + 1, Width);
        var bottom = Math.Clamp(area.Y + area.Height, top + 1, Height);
        var step = Math.Max(1, Math.Min(right - left, bottom - top) / 16);

        var reds = new List<byte>();
        var greens = new List<byte>();
        var blues = new List<byte>();
        for (var y = top; y < bottom; y += step)
        {
            for (var x = left; x < right; x += step)
            {
                var offset = (y * Width + x) * BytesPerPixel;
                blues.Add(Pixels[offset]);
                greens.Add(Pixels[offset + 1]);
                reds.Add(Pixels[offset + 2]);
            }
        }

        return new Rgb(MedianOf(reds), MedianOf(greens), MedianOf(blues));
    }

    private static double MedianOf(List<byte> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    public void Render(PixelRect crop, int width, int height, ColorAdjustment adjustment, byte[] target)
    {
        if (target.Length < width * height * BytesPerPixel)
        {
            throw new ArgumentException("Buffer di destinazione troppo piccolo.", nameof(target));
        }

        var levels = adjustment.BuildLevels();
        var saturation = adjustment.SaturationFactor;
        var adjustSaturation = adjustment.Saturation != 0;

        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Clamp(crop.Y + y * crop.Height / height, 0, Height - 1);
            var row = y * width * BytesPerPixel;
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Clamp(crop.X + x * crop.Width / width, 0, Width - 1);
                var source = (sourceY * Width + sourceX) * BytesPerPixel;
                int b = levels[Pixels[source]];
                int g = levels[Pixels[source + 1]];
                int r = levels[Pixels[source + 2]];

                if (adjustSaturation)
                {
                    var gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    r = (int)Math.Clamp(gray + (r - gray) * saturation, 0, 255);
                    g = (int)Math.Clamp(gray + (g - gray) * saturation, 0, 255);
                    b = (int)Math.Clamp(gray + (b - gray) * saturation, 0, 255);
                }

                var destination = row + x * BytesPerPixel;
                target[destination] = (byte)b;
                target[destination + 1] = (byte)g;
                target[destination + 2] = (byte)r;
                target[destination + 3] = 255;
            }
        }
    }
}
