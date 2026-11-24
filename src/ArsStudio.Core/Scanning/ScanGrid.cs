using System;

namespace ArsStudio.Core.Scanning;

public static class ScanGrid
{
    public const double GridRatio = 0.45;
    public const double SampleRatio = 0.4;
    public const double MaxZoom = 4;

    public static PixelRect Crop(int width, int height, double zoom)
    {
        zoom = Math.Clamp(zoom, 1, MaxZoom);
        var cropWidth = Math.Max(1, (int)Math.Round(width / zoom));
        var cropHeight = Math.Max(1, (int)Math.Round(height / zoom));
        return new PixelRect((width - cropWidth) / 2, (height - cropHeight) / 2, cropWidth, cropHeight);
    }

    public static double CellSize(PixelRect crop) => GridRatio * Math.Min(crop.Width, crop.Height) / 3;

    public static PixelRect[] Cells(PixelRect crop)
    {
        var cell = CellSize(crop);
        var side = cell * 3;
        var left = crop.X + (crop.Width - side) / 2;
        var top = crop.Y + (crop.Height - side) / 2;
        var sample = Math.Max(1, (int)Math.Round(cell * SampleRatio));
        var inset = (cell - sample) / 2;

        var cells = new PixelRect[CubeState.StickersPerFace];
        for (var index = 0; index < cells.Length; index++)
        {
            var row = index / 3;
            var column = index % 3;
            cells[index] = new PixelRect(
                (int)Math.Round(left + column * cell + inset),
                (int)Math.Round(top + row * cell + inset),
                sample,
                sample);
        }

        return cells;
    }
}
