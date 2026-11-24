using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public static class BitmapFrameReader
{
    private const int HeaderSize = 54;

    private static readonly (int Width, int Height)[] CommonSizes =
    [
        (640, 480), (1280, 720), (1920, 1080), (320, 240), (800, 600),
        (1024, 768), (1280, 960), (1280, 1024), (1600, 1200), (2560, 1440), (3840, 2160),
    ];

    public static FrameImage? Read(ReadOnlySpan<byte> bitmap, IEnumerable<(int Width, int Height)> knownSizes, int maxWidth)
    {
        if (bitmap.Length < HeaderSize || bitmap[0] != 'B' || bitmap[1] != 'M')
        {
            return null;
        }

        var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(bitmap[10..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(bitmap[18..]);
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(bitmap[22..]);
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(bitmap[28..]);
        if (bitsPerPixel is not (24 or 32) || dataOffset < HeaderSize || dataOffset >= bitmap.Length)
        {
            return null;
        }

        var bytesPerPixel = bitsPerPixel / 8;
        var bottomUp = signedHeight > 0;
        var height = Math.Abs(signedHeight);
        var dataLength = bitmap.Length - dataOffset;

        if (width <= 0 || height <= 0 || Stride(width, bytesPerPixel) * (long)height != dataLength)
        {
            var size = knownSizes.Concat(CommonSizes)
                .FirstOrDefault(candidate => Stride(candidate.Width, bytesPerPixel) * (long)candidate.Height == dataLength);
            if (size == default)
            {
                return null;
            }

            (width, height) = size;
        }

        var stride = Stride(width, bytesPerPixel);
        var step = Math.Max(1, (width + maxWidth - 1) / maxWidth);
        var outputWidth = width / step;
        var outputHeight = height / step;
        var pixels = new byte[outputWidth * outputHeight * FrameImage.BytesPerPixel];
        var data = bitmap[dataOffset..];

        for (var y = 0; y < outputHeight; y++)
        {
            var sourceRow = bottomUp ? height - 1 - y * step : y * step;
            var source = data.Slice(sourceRow * stride, stride);
            var target = y * outputWidth * FrameImage.BytesPerPixel;
            for (var x = 0; x < outputWidth; x++)
            {
                var pixel = x * step * bytesPerPixel;
                pixels[target] = source[pixel];
                pixels[target + 1] = source[pixel + 1];
                pixels[target + 2] = source[pixel + 2];
                pixels[target + 3] = 255;
                target += FrameImage.BytesPerPixel;
            }
        }

        return new FrameImage(outputWidth, outputHeight, pixels);
    }

    private static int Stride(int width, int bytesPerPixel) => (width * bytesPerPixel + 3) & ~3;
}
