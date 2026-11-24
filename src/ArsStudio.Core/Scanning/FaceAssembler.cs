using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public static class FaceAssembler
{
    private const int Rotations = 4;

    public static IReadOnlyList<T> Rotate<T>(IReadOnlyList<T> face, int quarterTurns)
    {
        var current = face.ToArray();
        for (var turn = 0; turn < quarterTurns % Rotations; turn++)
        {
            var next = new T[current.Length];
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    next[row * 3 + column] = current[(2 - column) * 3 + row];
                }
            }

            current = next;
        }

        return current;
    }

    public static AssembledCube? Assemble(CubeState state)
    {
        if (Search(state) is { } direct)
        {
            return new AssembledCube(direct, false);
        }

        return Search(Mirror(state)) is { } mirrored ? new AssembledCube(mirrored, true) : null;
    }

    private static CubeState Mirror(CubeState state)
    {
        var order = new[] { CubeFace.Up, CubeFace.Right, CubeFace.Front, CubeFace.Left, CubeFace.Back, CubeFace.Down };
        return new CubeState(order.SelectMany(face => state.Face(face)).Select(color => color switch
        {
            CubeColor.Orange => CubeColor.Red,
            CubeColor.Red => CubeColor.Orange,
            _ => color,
        }));
    }

    private static CubeState? Search(CubeState state)
    {
        var faces = Enumerable.Range(0, CubeState.FaceCount)
            .Select(face => Enumerable.Range(0, Rotations).Select(turns => Rotate(state.Face((CubeFace)face), turns)).ToArray())
            .ToArray();

        var combinations = 1;
        for (var face = 0; face < CubeState.FaceCount; face++)
        {
            combinations *= Rotations;
        }

        for (var combination = 0; combination < combinations; combination++)
        {
            var stickers = new List<CubeColor>(CubeState.StickerCount);
            var code = combination;
            for (var face = 0; face < CubeState.FaceCount; face++)
            {
                stickers.AddRange(faces[face][code % Rotations]);
                code /= Rotations;
            }

            var candidate = new CubeState(stickers);
            if (CubeSolver.IsSolvable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
