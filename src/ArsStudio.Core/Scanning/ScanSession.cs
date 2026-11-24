using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public sealed class ScanSession
{
    private const double SameFaceDistance = 12;
    private const int CenterIndex = 4;

    private static readonly CubeFace[] Faces = Enum.GetValues<CubeFace>();

    private readonly Rgb?[] _samples = new Rgb?[CubeState.StickerCount];

    public IReadOnlyDictionary<CubeColor, LabColor> References { get; private set; } = StickerClassifier.DefaultReferences;

    public int ScannedCount => Faces.Count(IsScanned);

    public bool IsComplete => ScannedCount == CubeState.FaceCount;

    public bool IsScanned(CubeFace face) => _samples[Offset(face)] is not null;

    public ScanReading Read() => StickerClassifier.Read(_samples);

    public void Store(CubeFace face, IReadOnlyList<Rgb> stickers)
    {
        for (var index = 0; index < CubeState.StickersPerFace; index++)
        {
            _samples[Offset(face) + index] = stickers[index];
        }

        Refresh();
    }

    public void Clear(CubeFace face)
    {
        Array.Clear(_samples, Offset(face), CubeState.StickersPerFace);
        Refresh();
    }

    public void Reset()
    {
        Array.Clear(_samples);
        Refresh();
    }

    public CubeFace? ChooseSlot(IReadOnlyList<Rgb> stickers, bool overwrite)
    {
        if (MatchingFace(stickers) is { } same)
        {
            return overwrite ? same : null;
        }

        var center = LabColor.FromRgb(stickers[CenterIndex]);
        var ranked = References
            .OrderBy(reference => reference.Value.DistanceTo(center))
            .Select(reference => StickerClassifier.SlotOf(reference.Key))
            .ToArray();

        if (overwrite || !IsScanned(ranked[0]))
        {
            return ranked[0];
        }

        return ranked.Where(face => !IsScanned(face)).Select(face => (CubeFace?)face).FirstOrDefault();
    }

    private CubeFace? MatchingFace(IReadOnlyList<Rgb> stickers)
    {
        var live = stickers.Select(LabColor.FromRgb).ToArray();
        CubeFace? match = null;
        var bestDistance = SameFaceDistance;

        foreach (var face in Faces.Where(IsScanned))
        {
            var stored = _samples
                .Skip(Offset(face))
                .Take(CubeState.StickersPerFace)
                .Select(sample => LabColor.FromRgb(sample!.Value))
                .ToArray();

            for (var turns = 0; turns < 4; turns++)
            {
                var distance = live.Zip(FaceAssembler.Rotate(stored, turns), (a, b) => a.DistanceTo(b)).Average();
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    match = face;
                }
            }
        }

        return match;
    }

    private void Refresh() => References = StickerClassifier.References(_samples);

    private static int Offset(CubeFace face) => (int)face * CubeState.StickersPerFace;
}
