using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

public static class CubeDiagnostics
{
    private const int MaxProblems = 8;

    private static readonly int[][] Pieces = CubeGeometry.Stickers
        .Select((sticker, index) => (sticker.Position, Index: index))
        .GroupBy(sticker => sticker.Position)
        .Where(group => group.Count() > 1)
        .Select(group => group.Select(sticker => sticker.Index).ToArray())
        .ToArray();

    private static readonly HashSet<string> ValidPieces = Pieces
        .Select(piece => Key(piece.Select(index => CubeState.Solved.Stickers[index])))
        .ToHashSet();

    private static readonly Dictionary<CubeColor, GridVector> HomeNormals = Enum.GetValues<CubeFace>()
        .ToDictionary(face => CubeState.Solved.Face(face)[4], face => CubeGeometry.Stickers[(int)face * CubeState.StickersPerFace + 4].Normal);

    private static readonly (CubeColor First, CubeColor Second)[] OppositeColors =
    [
        (CubeColor.White, CubeColor.Yellow),
        (CubeColor.Orange, CubeColor.Red),
        (CubeColor.Green, CubeColor.Blue),
    ];

    private static readonly (CubeFace First, CubeFace Second)[] OppositeFaces =
    [
        (CubeFace.Up, CubeFace.Down),
        (CubeFace.Left, CubeFace.Right),
        (CubeFace.Front, CubeFace.Back),
    ];

    public static IReadOnlyList<string> Problems(CubeState state)
    {
        var problems = new List<string>();

        var empty = state.Stickers.Count(color => color == CubeColor.Empty);
        if (empty > 0)
        {
            problems.Add(empty == 1 ? "C'è 1 casella senza colore." : $"Ci sono {empty} caselle senza colore.");
        }

        foreach (var color in Enum.GetValues<CubeColor>().Where(color => color != CubeColor.Empty))
        {
            var count = state.Stickers.Count(sticker => sticker == color);
            if (count != CubeState.StickersPerFace)
            {
                problems.Add($"Il {Name(color)} compare {count} volte invece di {CubeState.StickersPerFace}.");
            }
        }

        problems.AddRange(CenterProblems(state));

        if (problems.Count == 0)
        {
            problems.AddRange(PieceProblems(state));
        }

        return problems.Count > MaxProblems
            ? [.. problems.Take(MaxProblems), $"...e altri {problems.Count - MaxProblems} problemi."]
            : problems;
    }

    internal static bool HasMirroredCorner(CubeState state) =>
        Pieces.Any(piece => IsMirrored(piece, piece.Select(index => state.Stickers[index]).ToArray()));

    public static string Name(CubeColor color) => color switch
    {
        CubeColor.White => "bianco",
        CubeColor.Yellow => "giallo",
        CubeColor.Orange => "arancione",
        CubeColor.Red => "rosso",
        CubeColor.Green => "verde",
        CubeColor.Blue => "blu",
        _ => "vuoto",
    };

    public static string Name(CubeFace face) => face switch
    {
        CubeFace.Up => "sopra",
        CubeFace.Left => "sinistra",
        CubeFace.Front => "davanti",
        CubeFace.Right => "destra",
        CubeFace.Back => "dietro",
        _ => "sotto",
    };

    private static IEnumerable<string> CenterProblems(CubeState state)
    {
        var centers = Enum.GetValues<CubeFace>().ToDictionary(face => face, face => state.Face(face)[4]);
        if (centers.Values.Contains(CubeColor.Empty))
        {
            yield break;
        }

        foreach (var group in centers.GroupBy(center => center.Value).Where(group => group.Count() > 1))
        {
            yield return $"I centri delle facce {string.Join(" e ", group.Select(center => Name(center.Key)))} sono entrambi {Name(group.Key)}.";
        }

        if (centers.Values.Distinct().Count() != CubeState.FaceCount)
        {
            yield break;
        }

        var wrongPairs = false;
        foreach (var (first, second) in OppositeFaces)
        {
            if (!AreOpposite(centers[first], centers[second]))
            {
                wrongPairs = true;
                yield return $"I centri {Name(centers[first])} ({Name(first)}) e {Name(centers[second])} ({Name(second)}) dovrebbero stare su facce adiacenti, non opposte.";
            }
        }

        if (!wrongPairs && !CentersMatchRealCube(state))
        {
            yield return "I centri sono disposti a specchio rispetto a un cubo reale: scambia due centri opposti.";
        }
    }

    private static IEnumerable<string> PieceProblems(CubeState state)
    {
        var problems = new List<string>();
        var seen = new Dictionary<string, int>();

        foreach (var piece in Pieces)
        {
            var colors = piece.Select(index => state.Stickers[index]).ToArray();
            var key = Key(colors);
            var description = Describe(piece, colors);

            if (!ValidPieces.Contains(key))
            {
                var reason = colors.Distinct().Count() != colors.Length
                    ? "ha due caselle dello stesso colore"
                    : "ha due colori opposti, che non possono stare sullo stesso pezzo";
                problems.Add($"{description} {reason}.");
                continue;
            }

            if (IsMirrored(piece, colors))
            {
                problems.Add($"{description} ha i colori in ordine speculare: due delle sue caselle sono scambiate tra loro.");
                continue;
            }

            seen[key] = seen.GetValueOrDefault(key) + 1;
            if (seen[key] == 2)
            {
                problems.Add($"Il pezzo {string.Join("-", colors.Select(Name))} compare due volte (anche in {description.ToLowerInvariant()}).");
            }
        }

        return problems;
    }

    private static bool IsMirrored(int[] piece, CubeColor[] colors)
    {
        if (piece.Length != 3 || colors.Any(color => !HomeNormals.ContainsKey(color)))
        {
            return false;
        }

        var placed = Handedness(piece.Select(index => CubeGeometry.Stickers[index].Normal).ToArray());
        var home = Handedness(colors.Select(color => HomeNormals[color]).ToArray());
        return placed != home;
    }

    private static int Handedness(GridVector[] normals) => normals[0].Dot(normals[1].Cross(normals[2]));

    private static string Describe(int[] piece, CubeColor[] colors)
    {
        var kind = piece.Length == 3 ? "L'angolo" : "Lo spigolo";
        var faces = string.Join("-", piece.Select(index => Name((CubeFace)(index / CubeState.StickersPerFace))));
        return $"{kind} {faces} ({string.Join(", ", colors.Select(Name))})";
    }

    private static bool CentersMatchRealCube(CubeState state)
    {
        var normalized = CubeMoves.Normalize(state);
        return Enum.GetValues<CubeFace>().All(face => normalized.Face(face)[4] == CubeState.Solved.Face(face)[4]);
    }

    private static bool AreOpposite(CubeColor first, CubeColor second) =>
        OppositeColors.Any(pair => (pair.First == first && pair.Second == second) || (pair.First == second && pair.Second == first));

    private static string Key(IEnumerable<CubeColor> colors) => string.Join(",", colors.Order());
}
