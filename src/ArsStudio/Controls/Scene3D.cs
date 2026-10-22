using System;
using System.Collections.Generic;
using ArsStudio.Core;
using Avalonia;
using Avalonia.Media;

namespace ArsStudio.Controls;

public readonly record struct Vector3(double X, double Y, double Z)
{
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(double k, Vector3 v) => new(k * v.X, k * v.Y, k * v.Z);

    public double Dot(Vector3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vector3 Cross(Vector3 other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    public static Vector3 From(GridVector v) => new(v.X, v.Y, v.Z);
}

public readonly record struct Rotation(Vector3 Axis, double Angle)
{
    public static Rotation None { get; } = new(new Vector3(0, 1, 0), 0);

    public static Rotation Clockwise(GridVector outwardAxis, double quarterTurns) =>
        new(Vector3.From(outwardAxis), -Math.PI / 2 * quarterTurns);

    public Vector3 Apply(Vector3 v)
    {
        if (Angle == 0)
        {
            return v;
        }

        var cos = Math.Cos(Angle);
        var sin = Math.Sin(Angle);
        return cos * v + sin * Axis.Cross(v) + (Axis.Dot(v) * (1 - cos)) * Axis;
    }
}

public sealed record SceneQuad(
    Vector3[] Corners,
    Vector3 Normal,
    IBrush Brush,
    bool IsBackground = false,
    double DepthBias = 0,
    double Shading = 0,
    bool HasOutline = false);

public readonly struct SceneCamera
{
    public const double Distance = 10;

    private readonly double _yawCos;
    private readonly double _yawSin;
    private readonly double _pitchCos;
    private readonly double _pitchSin;
    private readonly Point _center;
    private readonly double _scale;

    public SceneCamera(double yawDegrees, double pitchDegrees, Size size, double sceneRadius, double maxExtent)
    {
        var yaw = yawDegrees * Math.PI / 180;
        var pitch = pitchDegrees * Math.PI / 180;
        _yawCos = Math.Cos(yaw);
        _yawSin = Math.Sin(yaw);
        _pitchCos = Math.Cos(pitch);
        _pitchSin = Math.Sin(pitch);
        _center = new Point(size.Width / 2, size.Height / 2);
        var distance = Distance * Math.Max(1, sceneRadius / 2.7);
        CameraDistance = distance;
        _scale = Math.Min(Math.Min(size.Width, size.Height), maxExtent) * 0.42 * distance / sceneRadius;
    }

    public double CameraDistance { get; }

    public Vector3 Rotate(Vector3 v)
    {
        var x = v.X * _yawCos + v.Z * _yawSin;
        var z = -v.X * _yawSin + v.Z * _yawCos;
        var y = v.Y * _pitchCos - z * _pitchSin;
        return new Vector3(x, y, v.Y * _pitchSin + z * _pitchCos);
    }

    public Point Project(Vector3 v)
    {
        var factor = _scale / (CameraDistance - v.Z);
        return new Point(_center.X + v.X * factor, _center.Y - v.Y * factor);
    }
}

public static class SceneBuilder
{
    private const double BodyHalfSize = 0.5;
    private const double StickerHalfSize = 0.42;
    private const double StickerDepthBias = 0.001;
    private const double BodyShading = 0.5;
    private const double StickerShading = 0.18;
    private const double BoxShading = 0.45;

    public static void AddCube(
        List<SceneQuad> quads,
        CubeState state,
        Func<CubeColor, IBrush> brushOf,
        IBrush bodyBrush,
        Func<StickerPlacement, Rotation> rotationOf)
    {
        for (var i = 0; i < CubeGeometry.Stickers.Count; i++)
        {
            var sticker = CubeGeometry.Stickers[i];
            var rotation = rotationOf(sticker);
            var normal = rotation.Apply(Vector3.From(sticker.Normal));
            var center = rotation.Apply(Vector3.From(sticker.Position) + 0.5 * Vector3.From(sticker.Normal));
            var right = rotation.Apply(Vector3.From(sticker.Right));
            var down = rotation.Apply(Vector3.From(sticker.Down));

            quads.Add(new SceneQuad(Square(center, right, down, BodyHalfSize), normal, bodyBrush, Shading: BodyShading));
            quads.Add(new SceneQuad(
                Square(center, right, down, StickerHalfSize),
                normal,
                brushOf(state.Stickers[i]),
                DepthBias: StickerDepthBias,
                Shading: StickerShading));
        }
    }

    public static void AddBox(
        List<SceneQuad> quads,
        Vector3 center,
        Vector3 halfX,
        Vector3 halfY,
        Vector3 halfZ,
        IBrush brush,
        bool isBackground = false,
        double depthBias = 0,
        bool hasOutline = true)
    {
        foreach (var (normal, u, v) in new[]
                 {
                     (halfX, halfY, halfZ),
                     (-1 * halfX, halfZ, halfY),
                     (halfY, halfZ, halfX),
                     (-1 * halfY, halfX, halfZ),
                     (halfZ, halfX, halfY),
                     (-1 * halfZ, halfY, halfX),
                 })
        {
            var faceCenter = center + normal;
            quads.Add(new SceneQuad(
                [faceCenter - u - v, faceCenter + u - v, faceCenter + u + v, faceCenter - u + v],
                normal,
                brush,
                isBackground,
                depthBias,
                BoxShading,
                hasOutline));
        }
    }

    private static Vector3[] Square(Vector3 center, Vector3 right, Vector3 down, double halfSize) =>
    [
        center - halfSize * right - halfSize * down,
        center + halfSize * right - halfSize * down,
        center + halfSize * right + halfSize * down,
        center - halfSize * right + halfSize * down,
    ];
}
