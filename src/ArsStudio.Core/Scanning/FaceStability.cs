using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

public sealed class FaceStability
{
    private CubeColor[] _current = [];
    private CubeColor[]? _ignored;
    private DateTime _since;

    public void Reset() => _current = [];

    public void Ignore(IReadOnlyList<CubeColor> colors)
    {
        _ignored = Canonical(colors);
        _current = [];
    }

    public bool IsStable(IReadOnlyList<CubeColor> colors, DateTime now, TimeSpan duration)
    {
        var canonical = Canonical(colors);
        if (_ignored is not null)
        {
            if (canonical.SequenceEqual(_ignored))
            {
                return false;
            }

            _ignored = null;
        }

        if (!canonical.SequenceEqual(_current))
        {
            _current = canonical;
            _since = now;
            return false;
        }

        return now - _since >= duration;
    }

    private static CubeColor[] Canonical(IReadOnlyList<CubeColor> colors) =>
        Enumerable.Range(0, 4)
            .Select(turns => FaceAssembler.Rotate(colors, turns).ToArray())
            .MinBy(rotated => string.Concat(rotated.Select(color => (char)('0' + (int)color))))!;
}
