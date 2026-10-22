using System;
using ArsStudio.Core;

namespace ArsStudio.Controls;

public sealed record CubeFrame(CubeState State, CubeState? Previous = null, CubeMove? Move = null, TimeSpan Duration = default)
{
    public bool IsAnimated => Previous is not null && Move is not null && Duration > TimeSpan.Zero;
}
