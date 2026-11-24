using System;
using System.Runtime.InteropServices;
using ArsStudio.Core.Scanning;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelRect = ArsStudio.Core.Scanning.PixelRect;

namespace ArsStudio.ViewModels;

public sealed class PreviewSurface
{
    private const int MaxWidth = 640;

    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private byte[] _pixels = [];

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Bitmap Draw(FrameImage frame, PixelRect crop, ColorAdjustment adjustment)
    {
        var scale = Math.Min(1.0, (double)MaxWidth / crop.Width);
        Width = Math.Max(1, (int)Math.Round(crop.Width * scale));
        Height = Math.Max(1, (int)Math.Round(crop.Height * scale));

        var length = Width * Height * FrameImage.BytesPerPixel;
        if (_pixels.Length != length)
        {
            _pixels = new byte[length];
        }

        frame.Render(crop, Width, Height, adjustment, _pixels);

        var target = _back;
        if (target is null || target.PixelSize.Width != Width || target.PixelSize.Height != Height)
        {
            target = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);
        }

        using (var buffer = target.Lock())
        {
            var rowLength = Width * FrameImage.BytesPerPixel;
            for (var row = 0; row < Height; row++)
            {
                Marshal.Copy(_pixels, row * rowLength, buffer.Address + row * buffer.RowBytes, rowLength);
            }
        }

        _back = _front;
        _front = target;
        return target;
    }
}
