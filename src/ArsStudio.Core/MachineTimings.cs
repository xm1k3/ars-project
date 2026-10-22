using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

public sealed record MachineTimings(TimeSpan QuarterTurn, TimeSpan CarriageMove)
{
    public static MachineTimings Default { get; } = new(TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(500));

    public TimeSpan DurationOf(MachineStep step) =>
        step.Motion == MachineMotion.RotateGrippers ? QuarterTurn : CarriageMove;

    public TimeSpan TotalDuration(IEnumerable<MachineStep> steps) =>
        steps.Aggregate(TimeSpan.Zero, (total, step) => total + DurationOf(step));
}
