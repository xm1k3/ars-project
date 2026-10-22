using System;
using System.Collections.Generic;
using ArsStudio.Core;
using Avalonia;
using Avalonia.Media;

namespace ArsStudio.Controls;

public class Cube3DView : OrbitView
{
    public static readonly StyledProperty<CubeFrame?> FrameProperty =
        AvaloniaProperty.Register<Cube3DView, CubeFrame?>(nameof(Frame));

    private static readonly IBrush BodyBrush = new SolidColorBrush(Color.Parse("#0B0D10"));

    static Cube3DView()
    {
        AffectsRender<Cube3DView>(FrameProperty);
    }

    public Cube3DView()
        : base(-35, 25)
    {
    }

    public CubeFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override double SceneRadius => 2.7;

    protected override double MaxExtent => 520;

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

        if (!IsSceneAnimating)
        {
            SceneBuilder.AddCube(quads, frame.State, CubeBrush, BodyBrush, _ => Rotation.None);
            return;
        }

        var move = frame.Move!.Value;
        var axis = CubeGeometry.AxisOf(move.Face);
        var progress = Ease(AnimationElapsed.TotalMilliseconds / frame.Duration.TotalMilliseconds);
        var layer = Rotation.Clockwise(axis, (move.QuarterTurns == 3 ? -1 : move.QuarterTurns) * progress);
        SceneBuilder.AddCube(quads, frame.Previous!, CubeBrush, BodyBrush,
            sticker => sticker.Position.Dot(axis) == 1 ? layer : Rotation.None);
    }
}
