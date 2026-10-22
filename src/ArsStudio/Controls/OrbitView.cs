using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ArsStudio.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ArsStudio.Controls;

public abstract class OrbitView : Control
{
    private const double MinPitch = -85;
    private const double MaxPitch = 85;

    private static readonly Vector3 LightDirection = Normalize(new Vector3(-0.35, 0.85, 0.4));
    private static readonly IPen OutlinePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 1);

    private readonly Stopwatch _animationClock = new();
    private double _yaw;
    private double _pitch;
    private Point? _dragOrigin;

    static OrbitView()
    {
        ClipToBoundsProperty.OverrideDefaultValue<OrbitView>(true);
    }

    protected OrbitView(double yaw, double pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    protected abstract double SceneRadius { get; }

    protected abstract double MaxExtent { get; }

    protected TimeSpan AnimationElapsed => _animationClock.Elapsed;

    protected abstract bool IsSceneAnimating { get; }

    protected abstract void BuildScene(List<SceneQuad> quads);

    protected void StartAnimation()
    {
        _animationClock.Restart();
        RequestNextFrame();
    }

    protected IBrush CubeBrush(CubeColor color) => ResourceBrush($"Cube{color}Brush");

    protected IBrush ResourceBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var brush) && brush is IBrush resolved
            ? resolved
            : Brushes.Gray;

    protected static double Ease(double progress) => progress * progress * (3 - 2 * progress);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragOrigin = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragOrigin is not { } origin)
        {
            return;
        }

        var position = e.GetPosition(this);
        _yaw += (position.X - origin.X) * 0.5;
        _pitch = Math.Clamp(_pitch + (position.Y - origin.Y) * 0.5, MinPitch, MaxPitch);
        _dragOrigin = position;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragOrigin = null;
        e.Pointer.Capture(null);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var quads = new List<SceneQuad>();
        BuildScene(quads);

        var camera = new SceneCamera(_yaw, _pitch, Bounds.Size, SceneRadius, MaxExtent);
        var eye = new Vector3(0, 0, camera.CameraDistance);
        var visible = new List<(bool IsBackground, double Depth, Point[] Points, IBrush Brush, IPen? Pen)>();

        foreach (var quad in quads)
        {
            var corners = quad.Corners.Select(camera.Rotate).ToArray();
            var center = 0.25 * (corners[0] + corners[1] + corners[2] + corners[3]);
            if (camera.Rotate(quad.Normal).Dot(eye - center) <= 0)
            {
                continue;
            }

            visible.Add((
                quad.IsBackground,
                center.Z + quad.DepthBias,
                corners.Select(camera.Project).ToArray(),
                Shade(quad.Brush, quad.Normal, quad.Shading),
                quad.HasOutline ? OutlinePen : null));
        }

        foreach (var (_, _, points, brush, pen) in visible.OrderByDescending(face => face.IsBackground).ThenBy(face => face.Depth))
        {
            var geometry = new StreamGeometry();
            using (var builder = geometry.Open())
            {
                builder.BeginFigure(points[0], true);
                foreach (var point in points.Skip(1))
                {
                    builder.LineTo(point);
                }

                builder.EndFigure(true);
            }

            context.DrawGeometry(brush, pen, geometry);
        }

        if (IsSceneAnimating)
        {
            RequestNextFrame();
        }
    }

    private static IBrush Shade(IBrush brush, Vector3 normal, double strength)
    {
        if (strength <= 0 || brush is not ISolidColorBrush solid)
        {
            return brush;
        }

        var light = Math.Max(0, Normalize(normal).Dot(LightDirection));
        var factor = 1 - strength + strength * light;
        var color = solid.Color;
        return new SolidColorBrush(
            Color.FromArgb(color.A, Scale(color.R, factor), Scale(color.G, factor), Scale(color.B, factor)),
            solid.Opacity);
    }

    private static byte Scale(byte channel, double factor) => (byte)Math.Clamp(channel * factor, 0, 255);

    private static Vector3 Normalize(Vector3 v)
    {
        var length = Math.Sqrt(v.Dot(v));
        return length == 0 ? v : 1 / length * v;
    }

    private void RequestNextFrame() =>
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ => InvalidateVisual());
}
