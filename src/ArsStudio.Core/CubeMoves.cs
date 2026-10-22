using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

public readonly record struct CubeMove(char Face, int QuarterTurns)
{
    public static CubeMove Parse(string move)
    {
        if (move.Length is < 1 or > 2 || "ULFRBD".IndexOf(move[0]) < 0)
        {
            throw new FormatException($"Mossa non valida: {move}");
        }

        var quarterTurns = move.Length == 1 ? 1 : move[1] switch
        {
            '2' => 2,
            '\'' => 3,
            _ => throw new FormatException($"Mossa non valida: {move}"),
        };

        return new CubeMove(move[0], quarterTurns);
    }
}

public static class CubeMoves
{
    private static readonly (char Axis, int Turns)[] FaceUpRotations =
    [
        ('U', 0),
        ('F', 1),
        ('F', 2),
        ('F', 3),
        ('R', 1),
        ('R', 3),
    ];

    private static readonly Dictionary<StickerPlacementKey, int> IndexByPlacement = CubeGeometry.Stickers
        .Select((sticker, index) => (Key: new StickerPlacementKey(sticker.Position, sticker.Normal), index))
        .ToDictionary(entry => entry.Key, entry => entry.index);

    public static IReadOnlyList<string> Split(string moves) =>
        moves.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static CubeState Apply(CubeState state, string moves) =>
        Split(moves).Aggregate(state, (current, move) => Apply(current, CubeMove.Parse(move)));

    public static CubeState Apply(CubeState state, CubeMove move)
    {
        var axis = CubeGeometry.AxisOf(move.Face);
        for (var turn = 0; turn < move.QuarterTurns; turn++)
        {
            state = Turn(state, axis, wholeCube: false);
        }

        return state;
    }

    public static CubeState RotateWholeCube(CubeState state, char axisFace, int quarterTurns)
    {
        var axis = CubeGeometry.AxisOf(axisFace);
        for (var turn = 0; turn < quarterTurns; turn++)
        {
            state = Turn(state, axis, wholeCube: true);
        }

        return state;
    }

    public static CubeState Normalize(CubeState state)
    {
        foreach (var (axis, turns) in FaceUpRotations)
        {
            var faceUp = RotateWholeCube(state, axis, turns);
            for (var spin = 0; spin < 4; spin++)
            {
                var candidate = RotateWholeCube(faceUp, 'U', spin);
                if (candidate.Face(CubeFace.Up)[4] == CubeColor.White && candidate.Face(CubeFace.Front)[4] == CubeColor.Green)
                {
                    return candidate;
                }
            }
        }

        return state;
    }

    public static bool IsSolved(CubeState state) =>
        Enumerable.Range(0, CubeState.FaceCount).All(face => state.Face((CubeFace)face).Distinct().Count() == 1);

    private static CubeState Turn(CubeState state, GridVector axis, bool wholeCube)
    {
        var result = state.Stickers.ToArray();
        for (var i = 0; i < CubeGeometry.Stickers.Count; i++)
        {
            var sticker = CubeGeometry.Stickers[i];
            if (!wholeCube && sticker.Position.Dot(axis) != 1)
            {
                continue;
            }

            var target = new StickerPlacementKey(sticker.Position.RotateClockwise(axis), sticker.Normal.RotateClockwise(axis));
            result[IndexByPlacement[target]] = state.Stickers[i];
        }

        return new CubeState(result);
    }

    private readonly record struct StickerPlacementKey(GridVector Position, GridVector Normal);
}
