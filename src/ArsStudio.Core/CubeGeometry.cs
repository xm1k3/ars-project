using System;
using System.Collections.Generic;

namespace ArsStudio.Core;

public readonly record struct GridVector(int X, int Y, int Z)
{
    public static GridVector operator +(GridVector a, GridVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static GridVector operator *(int k, GridVector v) => new(k * v.X, k * v.Y, k * v.Z);

    public int Dot(GridVector other) => X * other.X + Y * other.Y + Z * other.Z;

    public GridVector Cross(GridVector other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    public GridVector RotateClockwise(GridVector axis) => axis.Dot(this) * axis + -1 * axis.Cross(this);
}

public readonly record struct StickerPlacement(GridVector Position, GridVector Normal, GridVector Right, GridVector Down);

public static class CubeGeometry
{
    private static readonly (GridVector Normal, GridVector Right, GridVector Down)[] Frames =
    [
        (new(0, 1, 0), new(1, 0, 0), new(0, 0, 1)),
        (new(-1, 0, 0), new(0, 0, 1), new(0, -1, 0)),
        (new(0, 0, 1), new(1, 0, 0), new(0, -1, 0)),
        (new(1, 0, 0), new(0, 0, -1), new(0, -1, 0)),
        (new(0, 0, -1), new(-1, 0, 0), new(0, -1, 0)),
        (new(0, -1, 0), new(1, 0, 0), new(0, 0, -1)),
    ];

    public static IReadOnlyList<StickerPlacement> Stickers { get; } = BuildStickers();

    public static GridVector AxisOf(char face) => face switch
    {
        'U' => Frames[(int)CubeFace.Up].Normal,
        'L' => Frames[(int)CubeFace.Left].Normal,
        'F' => Frames[(int)CubeFace.Front].Normal,
        'R' => Frames[(int)CubeFace.Right].Normal,
        'B' => Frames[(int)CubeFace.Back].Normal,
        'D' => Frames[(int)CubeFace.Down].Normal,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    private static StickerPlacement[] BuildStickers()
    {
        var stickers = new List<StickerPlacement>();
        foreach (var (normal, right, down) in Frames)
        {
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var position = normal + (column - 1) * right + (row - 1) * down;
                    stickers.Add(new StickerPlacement(position, normal, right, down));
                }
            }
        }

        return stickers.ToArray();
    }
}
