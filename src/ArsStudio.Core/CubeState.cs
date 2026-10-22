using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

public sealed class CubeState
{
    public const int FaceCount = 6;
    public const int StickersPerFace = 9;
    public const int StickerCount = FaceCount * StickersPerFace;

    private static readonly CubeColor[] SolvedFaceColors =
    [
        CubeColor.White,
        CubeColor.Orange,
        CubeColor.Green,
        CubeColor.Red,
        CubeColor.Blue,
        CubeColor.Yellow,
    ];

    private readonly CubeColor[] _stickers;

    public CubeState(IEnumerable<CubeColor> stickers)
    {
        _stickers = stickers.ToArray();
        if (_stickers.Length != StickerCount)
        {
            throw new ArgumentException($"Il cubo deve avere {StickerCount} caselle.", nameof(stickers));
        }
    }

    public static CubeState Empty { get; } = new(Enumerable.Repeat(CubeColor.Empty, StickerCount));

    public static CubeState Solved { get; } =
        new(SolvedFaceColors.SelectMany(color => Enumerable.Repeat(color, StickersPerFace)));

    public IReadOnlyList<CubeColor> Stickers => _stickers;

    public IReadOnlyList<CubeColor> Face(CubeFace face) =>
        new ArraySegment<CubeColor>(_stickers, (int)face * StickersPerFace, StickersPerFace);
}
