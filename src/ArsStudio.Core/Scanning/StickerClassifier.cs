using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public static class StickerClassifier
{
    private const int CenterIndex = 4;
    private const int StickersPerColor = CubeState.StickersPerFace - 1;
    private const int RefineIterations = 3;
    private const double DoubtRatio = 0.7;

    private static readonly CubeColor[] Colors =
    [
        CubeColor.White,
        CubeColor.Yellow,
        CubeColor.Orange,
        CubeColor.Red,
        CubeColor.Green,
        CubeColor.Blue,
    ];

    public static IReadOnlyDictionary<CubeColor, LabColor> DefaultReferences { get; } = new Dictionary<CubeColor, LabColor>
    {
        [CubeColor.White] = LabColor.FromRgb(new Rgb(200, 205, 210)),
        [CubeColor.Yellow] = LabColor.FromRgb(new Rgb(215, 205, 50)),
        [CubeColor.Orange] = LabColor.FromRgb(new Rgb(215, 95, 30)),
        [CubeColor.Red] = LabColor.FromRgb(new Rgb(175, 25, 35)),
        [CubeColor.Green] = LabColor.FromRgb(new Rgb(15, 120, 60)),
        [CubeColor.Blue] = LabColor.FromRgb(new Rgb(10, 60, 145)),
    };

    public static CubeColor Classify(Rgb sample, IReadOnlyDictionary<CubeColor, LabColor> references)
    {
        var lab = LabColor.FromRgb(sample);
        return references.MinBy(reference => reference.Value.DistanceTo(lab)).Key;
    }

    public static CubeColor SlotColor(CubeFace face) => CubeState.Solved.Face(face)[CenterIndex];

    public static CubeFace SlotOf(CubeColor color) =>
        Enum.GetValues<CubeFace>().First(face => SlotColor(face) == color);

    public static IReadOnlyDictionary<CubeColor, LabColor> References(IReadOnlyList<Rgb?> samples) =>
        Refine(samples).References;

    public static ScanReading Read(IReadOnlyList<Rgb?> samples)
    {
        var (references, colors, labs) = Refine(samples);
        var doubtful = new HashSet<int>();
        for (var sticker = 0; sticker < colors.Length; sticker++)
        {
            if (labs[sticker] is not { } lab || sticker == Center(sticker / CubeState.StickersPerFace))
            {
                continue;
            }

            var own = references[colors[sticker]].DistanceTo(lab);
            var other = references.Where(reference => reference.Key != colors[sticker]).Min(reference => reference.Value.DistanceTo(lab));
            if (own > DoubtRatio * other)
            {
                doubtful.Add(sticker);
            }
        }

        return new ScanReading(new CubeState(colors), doubtful);
    }

    private static (IReadOnlyDictionary<CubeColor, LabColor> References, CubeColor[] Colors, LabColor?[] Labs) Refine(IReadOnlyList<Rgb?> samples)
    {
        if (samples.Count != CubeState.StickerCount)
        {
            throw new ArgumentException($"Servono {CubeState.StickerCount} campioni.", nameof(samples));
        }

        var labs = samples.Select(sample => sample is { } value ? LabColor.FromRgb(value) : (LabColor?)null).ToArray();
        IReadOnlyDictionary<CubeColor, LabColor> references = DefaultReferences;
        var colors = Assign(labs, references);

        for (var iteration = 0; iteration < RefineIterations; iteration++)
        {
            var refined = new Dictionary<CubeColor, LabColor>(DefaultReferences);
            foreach (var group in Enumerable.Range(0, colors.Length)
                .Where(sticker => labs[sticker] is not null)
                .GroupBy(sticker => colors[sticker]))
            {
                var members = group.Select(sticker => labs[sticker]!.Value).ToArray();
                refined[group.Key] = new LabColor(
                    members.Average(lab => lab.L),
                    members.Average(lab => lab.A),
                    members.Average(lab => lab.B));
            }

            references = refined;
            colors = Assign(labs, references);
        }

        return (references, colors, labs);
    }

    private static CubeColor[] Assign(LabColor?[] labs, IReadOnlyDictionary<CubeColor, LabColor> references)
    {
        var result = new CubeColor[CubeState.StickerCount];
        var complete = labs.All(lab => lab is not null);
        var candidates = new List<(int Sticker, CubeColor Color, double Distance)>();

        for (var sticker = 0; sticker < result.Length; sticker++)
        {
            var face = sticker / CubeState.StickersPerFace;
            if (labs[sticker] is not { } lab)
            {
                continue;
            }

            if (sticker == Center(face))
            {
                result[sticker] = SlotColor((CubeFace)face);
            }
            else if (complete)
            {
                candidates.AddRange(references.Select(reference => (sticker, reference.Key, reference.Value.DistanceTo(lab))));
            }
            else
            {
                result[sticker] = references.MinBy(reference => reference.Value.DistanceTo(lab)).Key;
            }
        }

        if (complete)
        {
            AssignWithLimit(candidates, result);
        }

        return result;
    }

    private static void AssignWithLimit(List<(int Sticker, CubeColor Color, double Distance)> candidates, CubeColor[] result)
    {
        var used = Colors.ToDictionary(color => color, _ => 0);
        var assigned = new HashSet<int>();
        foreach (var (sticker, color, _) in candidates.OrderBy(candidate => candidate.Distance))
        {
            if (assigned.Contains(sticker) || used[color] >= StickersPerColor)
            {
                continue;
            }

            result[sticker] = color;
            used[color]++;
            assigned.Add(sticker);
        }
    }

    private static int Center(int face) => face * CubeState.StickersPerFace + CenterIndex;
}
