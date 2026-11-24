using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

internal static class LayerSolver
{
    private const string Failure = "Non è stato possibile trovare una soluzione a strati.";
    private const string Faces = "ULFRBD";
    private const string Twist = "R' D' R D R' D' R D";

    private sealed record Macro(string Moves, int[] Sources, char Face);

    private sealed record Stage(Func<byte[], int> Progress, int Target, Macro[] Macros, int MaxDepth);

    private static readonly byte[] Solved = CubeState.Solved.Stickers.Select(color => (byte)color).ToArray();

    private static readonly int[][] Pieces = CubeGeometry.Stickers
        .Select((sticker, index) => (sticker.Position, Index: index))
        .GroupBy(sticker => sticker.Position)
        .Where(group => group.Count() > 1)
        .Select(group => group.Select(sticker => sticker.Index).ToArray())
        .ToArray();

    private static readonly int[][] TopEdges = Layer(1, 2);
    private static readonly int[][] TopCorners = Layer(1, 3);
    private static readonly int[][] MiddleEdges = Layer(0, 2);
    private static readonly int[][] BottomEdges = Layer(-1, 2);
    private static readonly int[][] BottomCorners = Layer(-1, 3);

    private static readonly int[] BottomEdgeFaces = BottomEdges
        .Select(piece => piece.Single(index => CubeGeometry.Stickers[index].Normal.Y == -1))
        .ToArray();

    private static readonly Macro[] Singles = Faces
        .SelectMany(face => new[] { $"{face}", $"{face}'", $"{face}2" })
        .Select(moves => new Macro(moves, CubeMoves.Sources(moves), moves[0]))
        .ToArray();

    private static readonly Macro[] LayerTurns = Build(false, "U", "U'", "U2");

    private static readonly Stage[] Stages =
    [
        new(state => Count(state, TopEdges), 4, Singles, 7),
        new(state => Kept(state, TopEdges) ? Count(state, TopCorners) : -1, 4,
            [.. LayerTurns, .. Build(true, "R U R'", "R U' R'", "F' U' F", "F' U F", "R U2 R'")], 6),
        new(state => Kept(state, TopEdges, TopCorners) ? Count(state, MiddleEdges) : -1, 4,
            [.. LayerTurns, .. Build(true, "U R U' R' U' F' U F", "U' F' U F U R U' R'")], 6),
        new(state => Kept(state, TopEdges, TopCorners, MiddleEdges) && BottomEdgeFaces.All(index => state[index] == Solved[index]) ? 1 : 0, 1,
            [.. LayerTurns, .. Build(true, "F R U R' U' F'")], 8),
        new(state => Kept(state, TopEdges, TopCorners, MiddleEdges, BottomEdges) ? 1 : 0, 1,
            [.. LayerTurns, .. Build(true, "R U R' U R U2 R' U")], 8),
        new(state => Kept(state, TopEdges, TopCorners, MiddleEdges, BottomEdges) && BottomCorners.All(piece => InPlace(state, piece)) ? 1 : 0, 1,
            Build(true, "U R U' L' U R' U' L", "L' U R U' L U R' U'"), 4),
        new(state => state.AsSpan().SequenceEqual(Solved) ? 1 : 0, 1,
            Build(true,
                $"{Twist} U {Twist} {Twist} U'",
                $"{Twist} {Twist} U {Twist} U'",
                $"{Twist} U {Twist} U {Twist} U2",
                $"{Twist} {Twist} U {Twist} {Twist} U {Twist} {Twist} U2"), 4),
    ];

    public static string Solve(CubeState normalized)
    {
        var state = normalized.Stickers.Select(color => (byte)color).ToArray();
        var moves = new List<string>();

        foreach (var stage in Stages)
        {
            var buffers = Enumerable.Range(0, stage.MaxDepth + 1).Select(_ => new byte[state.Length]).ToArray();
            while (stage.Progress(state) < stage.Target)
            {
                var path = Advance(state, stage, buffers) ?? throw new InvalidCubeException(Failure);
                foreach (var macro in path)
                {
                    state = Apply(state, macro.Sources, new byte[state.Length]);
                    moves.AddRange(CubeMoves.Split(macro.Moves));
                }
            }
        }

        return string.Join(' ', Simplify(moves));
    }

    private static List<Macro>? Advance(byte[] state, Stage stage, byte[][] buffers)
    {
        var goal = stage.Progress(state) + 1;
        state.CopyTo(buffers[0], 0);
        for (var depth = 1; depth <= stage.MaxDepth; depth++)
        {
            var path = new List<Macro>();
            if (Search(stage, buffers, 0, depth, goal, '\0', path))
            {
                path.Reverse();
                return path;
            }
        }

        return null;
    }

    private static bool Search(Stage stage, byte[][] buffers, int level, int depth, int goal, char lastFace, List<Macro> path)
    {
        if (level == depth)
        {
            return stage.Progress(buffers[level]) >= goal;
        }

        foreach (var macro in stage.Macros)
        {
            if (macro.Face != '\0' && macro.Face == lastFace)
            {
                continue;
            }

            Apply(buffers[level], macro.Sources, buffers[level + 1]);
            if (Search(stage, buffers, level + 1, depth, goal, macro.Face, path))
            {
                path.Add(macro);
                return true;
            }
        }

        return false;
    }

    private static byte[] Apply(byte[] state, int[] sources, byte[] target)
    {
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = state[sources[index]];
        }

        return target;
    }

    private static Macro[] Build(bool allSides, params string[] algorithms) =>
        algorithms
            .SelectMany(algorithm => Enumerable.Range(0, allSides ? 4 : 1).Select(spins => Convert(algorithm, spins)))
            .Select(moves => new Macro(moves, CubeMoves.Sources(moves), CubeMoves.Split(moves).Count == 1 ? moves[0] : '\0'))
            .ToArray();

    private static string Convert(string algorithm, int spins) =>
        string.Concat(algorithm.Select(symbol =>
        {
            var face = symbol switch
            {
                'U' => 'D',
                'D' => 'U',
                'F' => 'B',
                'B' => 'F',
                _ => symbol,
            };

            for (var spin = 0; spin < spins; spin++)
            {
                face = face switch
                {
                    'F' => 'R',
                    'R' => 'B',
                    'B' => 'L',
                    'L' => 'F',
                    _ => face,
                };
            }

            return face;
        }));

    private static List<string> Simplify(List<string> moves)
    {
        var result = new List<CubeMove>();
        foreach (var move in moves.Select(CubeMove.Parse))
        {
            if (result.Count > 0 && result[^1].Face == move.Face)
            {
                var turns = (result[^1].QuarterTurns + move.QuarterTurns) % 4;
                result.RemoveAt(result.Count - 1);
                if (turns != 0)
                {
                    result.Add(new CubeMove(move.Face, turns));
                }
            }
            else
            {
                result.Add(move);
            }
        }

        return result.Select(move => move.Face + move.QuarterTurns switch { 2 => "2", 3 => "'", _ => "" }).ToList();
    }

    private static int[][] Layer(int height, int stickers) =>
        Pieces.Where(piece => piece.Length == stickers && CubeGeometry.Stickers[piece[0]].Position.Y == height).ToArray();

    private static int Count(byte[] state, int[][] pieces) => pieces.Count(piece => IsSolved(state, piece));

    private static bool Kept(byte[] state, params int[][][] groups) =>
        groups.All(pieces => pieces.All(piece => IsSolved(state, piece)));

    private static bool IsSolved(byte[] state, int[] piece) => piece.All(index => state[index] == Solved[index]);

    private static bool InPlace(byte[] state, int[] piece) =>
        piece.All(index => piece.Any(other => state[other] == Solved[index]));
}
