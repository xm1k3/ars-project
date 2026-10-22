using System;
using ArsStudio.Core;

namespace ArsStudio.Controls;

public sealed record MachineFrame(MachineState State, MachineStep? Step = null, TimeSpan Duration = default)
{
    public bool IsAnimated => Step is not null && Duration > TimeSpan.Zero;
}
