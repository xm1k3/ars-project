using System;
using System.Collections.Generic;

namespace ArsStudio.Core.Scanning;

internal sealed class StickerBlobFinder
{
    private const int WorkWidth = 320;
    private const int Channels = 3;
    private const int EdgeThreshold = 22;
    private const int DarkThreshold = 45;
    private const double MinSideRatio = 0.025;
    private const double MaxSideRatio = 0.3;
    private const double MaxElongation = 2.0;
    private const double MinFill = 0.8;
    private const double MaxFill = 1.15;
    private const byte Edge = 0;
    private const byte Flat = 1;
    private const byte Visited = 2;

    private byte[] _work = [];
    private byte[] _mask = [];
    private int[] _stack = [];

    public List<StickerBlob> Find(FrameImage frame, PixelRect crop)
    {
        var step = Math.Max(1, (crop.Width + WorkWidth - 1) / WorkWidth);
        var width = crop.Width / step;
        var height = crop.Height / step;
        var blobs = new List<StickerBlob>();
        if (width < 3 || height < 3)
        {
            return blobs;
        }

        Reduce(frame, crop, step, width, height);
        MarkFlat(width, height);

        var shortSide = Math.Min(width, height);
        var minArea = Math.Pow(MinSideRatio * shortSide, 2);
        var maxArea = Math.Pow(MaxSideRatio * shortSide, 2);

        for (var start = 0; start < width * height; start++)
        {
            if (_mask[start] == Flat && Measure(start, width, minArea, maxArea) is { } blob)
            {
                blobs.Add(new StickerBlob(
                    crop.X + (blob.X + 0.5) * step,
                    crop.Y + (blob.Y + 0.5) * step,
                    blob.Area * step * step));
            }
        }

        return blobs;
    }

    private void Reduce(FrameImage frame, PixelRect crop, int step, int width, int height)
    {
        if (_work.Length != width * height * Channels)
        {
            _work = new byte[width * height * Channels];
            _mask = new byte[width * height];
            _stack = new int[width * height];
        }

        var pixels = frame.Pixels;
        var samples = step * step;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0;
                for (var dy = 0; dy < step; dy++)
                {
                    var source = ((crop.Y + y * step + dy) * frame.Width + crop.X + x * step) * FrameImage.BytesPerPixel;
                    for (var dx = 0; dx < step; dx++)
                    {
                        b += pixels[source];
                        g += pixels[source + 1];
                        r += pixels[source + 2];
                        source += FrameImage.BytesPerPixel;
                    }
                }

                var target = (y * width + x) * Channels;
                _work[target] = (byte)(b / samples);
                _work[target + 1] = (byte)(g / samples);
                _work[target + 2] = (byte)(r / samples);
            }
        }
    }

    private void MarkFlat(int width, int height)
    {
        Array.Clear(_mask);
        var row = width * Channels;
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var offset = (y * width + x) * Channels;
                var brightest = 0;
                var gradient = 0;
                for (var channel = 0; channel < Channels; channel++)
                {
                    var index = offset + channel;
                    brightest = Math.Max(brightest, _work[index]);
                    gradient = Math.Max(gradient, Math.Abs(_work[index + Channels] - _work[index - Channels]));
                    gradient = Math.Max(gradient, Math.Abs(_work[index + row] - _work[index - row]));
                }

                if (gradient < EdgeThreshold && brightest >= DarkThreshold)
                {
                    _mask[y * width + x] = Flat;
                }
            }
        }
    }

    private StickerBlob? Measure(int start, int width, double minArea, double maxArea)
    {
        double count = 0, sumX = 0, sumY = 0, sumXX = 0, sumYY = 0, sumXY = 0;
        var top = 0;
        _stack[top++] = start;
        _mask[start] = Visited;

        while (top > 0)
        {
            var index = _stack[--top];
            var x = index % width;
            var y = index / width;
            count++;
            sumX += x;
            sumY += y;
            sumXX += x * x;
            sumYY += y * y;
            sumXY += x * y;

            Push(index - 1, ref top);
            Push(index + 1, ref top);
            Push(index - width, ref top);
            Push(index + width, ref top);
        }

        if (count < minArea || count > maxArea)
        {
            return null;
        }

        var meanX = sumX / count;
        var meanY = sumY / count;
        var varianceX = sumXX / count - meanX * meanX;
        var varianceY = sumYY / count - meanY * meanY;
        var covariance = sumXY / count - meanX * meanY;
        var mean = (varianceX + varianceY) / 2;
        var spread = Math.Sqrt(Math.Pow((varianceX - varianceY) / 2, 2) + covariance * covariance);
        var major = mean + spread;
        var minor = mean - spread;
        if (minor <= 0 || major / minor > MaxElongation)
        {
            return null;
        }

        var fill = count / (12 * Math.Sqrt(major * minor));
        return fill is >= MinFill and <= MaxFill ? new StickerBlob(meanX, meanY, count) : null;
    }

    private void Push(int index, ref int top)
    {
        if (_mask[index] == Flat)
        {
            _mask[index] = Visited;
            _stack[top++] = index;
        }
    }
}
