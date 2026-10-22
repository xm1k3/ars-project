using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core;

public enum GripperPair
{
    RightLeft,
    FrontBack,
}

public enum MachineMotion
{
    RotateGrippers,
    OpenPair,
    ClosePair,
}

public enum CubeEffect
{
    None,
    FaceTurn,
    WholeCubeTurn,
}

public sealed record MachineState(CubeState Cube, IReadOnlyList<int> GripperTurns, bool RightLeftClosed, bool FrontBackClosed)
{
    public bool IsClosed(GripperPair pair) => pair == GripperPair.RightLeft ? RightLeftClosed : FrontBackClosed;
}

public sealed record MachineStep(
    char Command,
    string Description,
    MachineMotion Motion,
    IReadOnlyList<int> Grippers,
    int ClockwiseQuarterTurns,
    GripperPair Pair,
    CubeEffect Effect,
    CubeMove? CubeTurn,
    MachineState Before,
    MachineState After);

public static class MachineSimulator
{
    public const string GripperFaces = "RFLB";

    private static readonly Dictionary<char, (int[] Grippers, int ClockwiseQuarterTurns)> Rotations = new()
    {
        ['a'] = ([0], -1),
        ['b'] = ([0], 1),
        ['c'] = ([1], -1),
        ['d'] = ([1], 1),
        ['e'] = ([2], -1),
        ['f'] = ([2], 1),
        ['g'] = ([3], -1),
        ['h'] = ([3], 1),
        ['m'] = ([0, 2], -1),
        ['n'] = ([0, 2], 1),
        ['o'] = ([1, 3], -1),
        ['p'] = ([1, 3], 1),
    };

    private static readonly Dictionary<char, (GripperPair Pair, MachineMotion Motion)> CarriageMoves = new()
    {
        ['i'] = (GripperPair.FrontBack, MachineMotion.ClosePair),
        ['j'] = (GripperPair.FrontBack, MachineMotion.OpenPair),
        ['k'] = (GripperPair.RightLeft, MachineMotion.ClosePair),
        ['l'] = (GripperPair.RightLeft, MachineMotion.OpenPair),
    };

    public static MachineState Initial(CubeState cube) => new(cube, [0, 0, 0, 0], true, true);

    public static GripperPair PairOf(int gripper) => gripper % 2 == 0 ? GripperPair.RightLeft : GripperPair.FrontBack;

    public static IReadOnlyList<MachineStep> Simulate(CubeState start, string machineCode)
    {
        var steps = new List<MachineStep>();
        var state = Initial(start);
        foreach (var command in machineCode)
        {
            var step = Apply(state, command);
            steps.Add(step);
            state = step.After;
        }

        return steps;
    }

    private static MachineStep Apply(MachineState state, char command)
    {
        if (CarriageMoves.TryGetValue(command, out var carriage))
        {
            var close = carriage.Motion == MachineMotion.ClosePair;
            var after = carriage.Pair == GripperPair.RightLeft
                ? state with { RightLeftClosed = close }
                : state with { FrontBackClosed = close };
            var description = $"{(close ? "Chiude" : "Apre")} le pinze {PairName(carriage.Pair)}";
            return new MachineStep(command, description, carriage.Motion, [], 0, carriage.Pair, CubeEffect.None, null, state, after);
        }

        if (!Rotations.TryGetValue(command, out var rotation))
        {
            throw new FormatException($"Comando macchina non valido: {command}");
        }

        var pair = PairOf(rotation.Grippers[0]);
        var face = GripperFaces[rotation.Grippers[0]];
        var quarterTurns = rotation.ClockwiseQuarterTurns == 1 ? 1 : 3;
        var effect = CubeEffect.None;
        CubeMove? cubeTurn = null;
        var cube = state.Cube;

        if (state.IsClosed(pair))
        {
            var otherPair = pair == GripperPair.RightLeft ? GripperPair.FrontBack : GripperPair.RightLeft;
            if (rotation.Grippers.Length == 1)
            {
                effect = CubeEffect.FaceTurn;
                cubeTurn = new CubeMove(face, quarterTurns);
                cube = CubeMoves.Apply(cube, cubeTurn.Value);
            }
            else if (!state.IsClosed(otherPair))
            {
                effect = CubeEffect.WholeCubeTurn;
                cubeTurn = new CubeMove(face, quarterTurns);
                cube = CubeMoves.RotateWholeCube(cube, face, quarterTurns);
            }
        }

        var turns = state.GripperTurns.ToArray();
        foreach (var gripper in rotation.Grippers)
        {
            var sameDirection = gripper == rotation.Grippers[0];
            turns[gripper] = Mod4(turns[gripper] + (sameDirection ? rotation.ClockwiseQuarterTurns : -rotation.ClockwiseQuarterTurns));
        }

        var degrees = rotation.ClockwiseQuarterTurns == 1 ? "-90°" : "+90°";
        var target = rotation.Grippers.Length == 1 ? $"La pinza {face}" : $"Le pinze {PairName(pair)}";
        var verb = rotation.Grippers.Length == 1 ? "ruota" : "ruotano";
        var suffix = effect switch
        {
            CubeEffect.FaceTurn => $": gira la faccia {face}",
            CubeEffect.WholeCubeTurn => ": gira tutto il cubo",
            _ => "",
        };

        return new MachineStep(
            command,
            $"{target} {verb} di {degrees}{suffix}",
            MachineMotion.RotateGrippers,
            rotation.Grippers,
            rotation.ClockwiseQuarterTurns,
            pair,
            effect,
            cubeTurn,
            state,
            state with { Cube = cube, GripperTurns = turns });
    }

    private static string PairName(GripperPair pair) => pair == GripperPair.RightLeft ? "R/L" : "F/B";

    private static int Mod4(int value) => ((value % 4) + 4) % 4;
}
