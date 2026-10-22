using System.Collections.Generic;
using System.Linq;
using ArsStudio.Core;
using Avalonia;
using Avalonia.Media;

namespace ArsStudio.Controls;

public class MachineScene : OrbitView
{
    public static readonly StyledProperty<MachineFrame?> FrameProperty =
        AvaloniaProperty.Register<MachineScene, MachineFrame?>(nameof(Frame));

    private const double ClosedDistance = 1.52;
    private const double OpenDistance = 2.45;
    private const double ProngReach = 0.7;
    private const double ClawDepthBias = 0.35;
    private const double FloorY = -2.5;

    private static readonly IBrush CubeBodyBrush = new SolidColorBrush(Color.Parse("#0B0D10"));
    private static readonly IBrush BaseBrush = new SolidColorBrush(Color.Parse("#20252C"));
    private static readonly IBrush RailBrush = new SolidColorBrush(Color.Parse("#3A4049"));
    private static readonly IBrush SupportBrush = new SolidColorBrush(Color.Parse("#353B44"));
    private static readonly IBrush MotorBrush = new SolidColorBrush(Color.Parse("#2B3038"));
    private static readonly IBrush MotorCapBrush = new SolidColorBrush(Color.Parse("#4A515C"));
    private static readonly IBrush ShaftBrush = new SolidColorBrush(Color.Parse("#8A9099"));
    private static readonly IBrush ClawBrush = new SolidColorBrush(Color.Parse("#E040FB"), 0.88);
    private static readonly IBrush ClawStripeBrush = new SolidColorBrush(Color.Parse("#FFFFFF"), 0.9);

    static MachineScene()
    {
        AffectsRender<MachineScene>(FrameProperty);
    }

    public MachineScene()
        : base(-35, 28)
    {
    }

    public MachineFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override double SceneRadius => 4.6;

    protected override double MaxExtent => 760;

    protected override bool IsSceneAnimating => Frame is { IsAnimated: true } frame && AnimationElapsed < frame.Duration;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty && Frame is { IsAnimated: true })
        {
            StartAnimation();
        }
    }

    protected override void BuildScene(List<SceneQuad> quads)
    {
        if (Frame is not { } frame)
        {
            return;
        }

        AddFrame(quads);

        if (!IsSceneAnimating)
        {
            AddMachineState(quads, frame.State, null, 0);
            return;
        }

        var progress = Ease(AnimationElapsed.TotalMilliseconds / frame.Duration.TotalMilliseconds);
        AddMachineState(quads, frame.Step!.Before, frame.Step, progress);
    }

    private void AddMachineState(List<SceneQuad> quads, MachineState state, MachineStep? step, double progress)
    {
        var cubeRotation = CubeRotation(step, progress);
        SceneBuilder.AddCube(quads, state.Cube, CubeBrush, CubeBodyBrush, cubeRotation);

        for (var gripper = 0; gripper < 4; gripper++)
        {
            var pair = MachineSimulator.PairOf(gripper);
            var distance = Distance(state.IsClosed(pair), step, pair, progress);
            var turns = state.GripperTurns[gripper] + RotationDelta(step, gripper) * progress;
            AddGripper(quads, gripper, distance, turns);
        }
    }

    private static System.Func<StickerPlacement, Rotation> CubeRotation(MachineStep? step, double progress)
    {
        if (step is not { CubeTurn: { } turn } || step.Effect == CubeEffect.None)
        {
            return _ => Rotation.None;
        }

        var axis = CubeGeometry.AxisOf(turn.Face);
        var rotation = Rotation.Clockwise(axis, (turn.QuarterTurns == 3 ? -1 : 1) * progress);
        return step.Effect == CubeEffect.WholeCubeTurn
            ? _ => rotation
            : sticker => sticker.Position.Dot(axis) == 1 ? rotation : Rotation.None;
    }

    private static double Distance(bool closed, MachineStep? step, GripperPair pair, double progress)
    {
        var current = closed ? ClosedDistance : OpenDistance;
        if (step is null || step.Motion == MachineMotion.RotateGrippers || step.Pair != pair)
        {
            return current;
        }

        var target = step.Motion == MachineMotion.ClosePair ? ClosedDistance : OpenDistance;
        return current + (target - current) * progress;
    }

    private static double RotationDelta(MachineStep? step, int gripper)
    {
        if (step is not { Motion: MachineMotion.RotateGrippers } || !step.Grippers.Contains(gripper))
        {
            return 0;
        }

        return gripper == step.Grippers[0] ? step.ClockwiseQuarterTurns : -step.ClockwiseQuarterTurns;
    }

    private static void AddFrame(List<SceneQuad> quads)
    {
        var up = new Vector3(0, 1, 0);
        SceneBuilder.AddBox(quads, new Vector3(0, FloorY - 0.12, 0),
            new Vector3(4.7, 0, 0), 0.12 * up, new Vector3(0, 0, 4.7), BaseBrush, isBackground: true);
        SceneBuilder.AddBox(quads, new Vector3(0, FloorY + 0.05, 0),
            new Vector3(4.5, 0, 0), 0.05 * up, new Vector3(0, 0, 0.3), RailBrush, isBackground: true);
        SceneBuilder.AddBox(quads, new Vector3(0, FloorY + 0.15, 0),
            new Vector3(0.3, 0, 0), 0.05 * up, new Vector3(0, 0, 4.5), RailBrush, isBackground: true);
    }

    private static void AddGripper(List<SceneQuad> quads, int gripper, double distance, double clockwiseTurns)
    {
        var axis = CubeGeometry.AxisOf(MachineSimulator.GripperFaces[gripper]);
        var n = Vector3.From(axis);
        var up = new Vector3(0, 1, 0);
        var side = n.Cross(up);
        var spin = Rotation.Clockwise(axis, clockwiseTurns);
        var spunUp = spin.Apply(up);
        var spunSide = spin.Apply(side);

        SceneBuilder.AddBox(quads, (distance + 0.1) * n, 0.1 * n, 1.62 * spunUp, 0.45 * spunSide, ClawBrush, depthBias: ClawDepthBias);
        SceneBuilder.AddBox(quads, (distance + 0.21) * n, 0.01 * n, 1.4 * spunUp, 0.08 * spunSide, ClawStripeBrush, depthBias: ClawDepthBias, hasOutline: false);
        foreach (var direction in new[] { 1.0, -1.0 })
        {
            var prongCenter = (distance + 0.2 - (ProngReach + 0.2) / 2) * n + direction * 1.56 * spunUp;
            SceneBuilder.AddBox(quads, prongCenter, ((ProngReach + 0.2) / 2) * n, 0.06 * spunUp, 0.45 * spunSide, ClawBrush, depthBias: ClawDepthBias);
        }

        SceneBuilder.AddBox(quads, (distance + 0.4) * n, 0.2 * n, 0.25 * spunUp, 0.25 * spunSide, ShaftBrush);

        var motorCenter = (distance + 1.05) * n;
        SceneBuilder.AddBox(quads, motorCenter, 0.45 * n, 0.55 * up, 0.55 * side, MotorBrush);
        SceneBuilder.AddBox(quads, motorCenter + 0.5 * n, 0.05 * n, 0.45 * up, 0.45 * side, MotorCapBrush);

        var supportTop = -0.55;
        var supportBottom = FloorY + (MachineSimulator.PairOf(gripper) == GripperPair.RightLeft ? 0.1 : 0.2);
        var supportCenter = motorCenter + new Vector3(0, (supportTop + supportBottom) / 2, 0);
        SceneBuilder.AddBox(quads, supportCenter, 0.3 * n, ((supportTop - supportBottom) / 2) * up, 0.3 * side, SupportBrush);
    }
}
