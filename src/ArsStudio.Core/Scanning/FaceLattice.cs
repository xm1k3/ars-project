using System;
using System.Collections.Generic;
using System.Linq;

namespace ArsStudio.Core.Scanning;

internal static class FaceLattice
{
    private const int Size = 3;
    private const int Reach = Size - 1;
    private const int Span = 2 * Reach + 1;
    private const int MinFound = 7;
    private const double CellTolerance = 0.3;
    private const double MaxAreaRatio = 2.5;
    private const double MinSpacing = 1.02;
    private const double MaxSpacing = 1.9;
    private const double MinAxisRatio = 0.78;
    private const double MaxAxisCosine = 0.21;
    private const double MaxResidual = 0.12;
    private const double MinSideToSpacing = 0.45;

    private readonly record struct Member(StickerBlob Blob, int Column, int Row);

    public static DetectedFace? Fit(IReadOnlyList<StickerBlob> blobs)
    {
        List<Member>? best = null;
        var bestError = double.MaxValue;

        foreach (var origin in blobs)
        {
            foreach (var neighbour in blobs)
            {
                var distance = Distance(origin, neighbour);
                if (!SimilarSize(origin, neighbour)
                    || distance < origin.Side * MinSpacing
                    || distance > origin.Side * MaxSpacing)
                {
                    continue;
                }

                var (members, error) = Collect(blobs, origin, neighbour);
                if (members.Count >= MinFound
                    && (best is null || members.Count > best.Count || (members.Count == best.Count && error < bestError)))
                {
                    best = members;
                    bestError = error;
                }
            }
        }

        return best is null ? null : Refine(best);
    }

    private static (List<Member> Members, double Error) Collect(IReadOnlyList<StickerBlob> blobs, StickerBlob origin, StickerBlob neighbour)
    {
        var ux = neighbour.X - origin.X;
        var uy = neighbour.Y - origin.Y;
        var squared = ux * ux + uy * uy;
        var slots = new StickerBlob?[Span, Span];
        var errors = new double[Span, Span];

        foreach (var blob in blobs)
        {
            if (!SimilarSize(origin, blob))
            {
                continue;
            }

            var dx = blob.X - origin.X;
            var dy = blob.Y - origin.Y;
            var along = (dx * ux + dy * uy) / squared;
            var across = (dy * ux - dx * uy) / squared;
            var column = (int)Math.Round(along);
            var row = (int)Math.Round(across);
            var error = Math.Max(Math.Abs(along - column), Math.Abs(across - row));
            if (error > CellTolerance || Math.Abs(column) > Reach || Math.Abs(row) > Reach)
            {
                continue;
            }

            if (slots[column + Reach, row + Reach] is null || error < errors[column + Reach, row + Reach])
            {
                slots[column + Reach, row + Reach] = blob;
                errors[column + Reach, row + Reach] = error;
            }
        }

        var best = new List<Member>();
        var bestError = double.MaxValue;
        for (var left = 0; left <= Reach; left++)
        {
            for (var top = 0; top <= Reach; top++)
            {
                var members = new List<Member>();
                double error = 0;
                for (var column = 0; column < Size; column++)
                {
                    for (var row = 0; row < Size; row++)
                    {
                        if (slots[left + column, top + row] is { } blob)
                        {
                            members.Add(new Member(blob, column, row));
                            error += errors[left + column, top + row];
                        }
                    }
                }

                if (members.Count > best.Count || (members.Count == best.Count && error < bestError))
                {
                    best = members;
                    bestError = error;
                }
            }
        }

        return (best, bestError);
    }

    private static DetectedFace? Refine(List<Member> members)
    {
        if (Solve(members, member => member.Blob.X) is not { } x || Solve(members, member => member.Blob.Y) is not { } y)
        {
            return null;
        }

        var u = new ScanPoint(x.Column, y.Column);
        var v = new ScanPoint(x.Row, y.Row);
        var lengthU = Length(u);
        var lengthV = Length(v);
        var spacing = (lengthU + lengthV) / 2;
        if (Math.Min(lengthU, lengthV) / Math.Max(lengthU, lengthV) < MinAxisRatio
            || Math.Abs(u.X * v.X + u.Y * v.Y) / (lengthU * lengthV) > MaxAxisCosine)
        {
            return null;
        }

        var residual = Math.Sqrt(members.Average(member =>
            Math.Pow(x.Origin + x.Column * member.Column + x.Row * member.Row - member.Blob.X, 2)
            + Math.Pow(y.Origin + y.Column * member.Column + y.Row * member.Row - member.Blob.Y, 2)));
        if (residual > MaxResidual * spacing || members.Average(member => member.Blob.Side) < MinSideToSpacing * spacing)
        {
            return null;
        }

        var right = new[] { u, Negate(u), v, Negate(v) }.MaxBy(axis => axis.X);
        var down = new[] { u, Negate(u), v, Negate(v) }.MaxBy(axis => right.X * axis.Y - right.Y * axis.X);
        var centerX = x.Origin + x.Column + x.Row;
        var centerY = y.Origin + y.Column + y.Row;

        var centers = new ScanPoint[CubeState.StickersPerFace];
        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                centers[row * Size + column] = new ScanPoint(
                    centerX + (column - 1) * right.X + (row - 1) * down.X,
                    centerY + (column - 1) * right.Y + (row - 1) * down.Y);
            }
        }

        var found = new bool[centers.Length];
        foreach (var member in members)
        {
            var nearest = Enumerable.Range(0, centers.Length)
                .MinBy(index => Math.Pow(centers[index].X - member.Blob.X, 2) + Math.Pow(centers[index].Y - member.Blob.Y, 2));
            found[nearest] = true;
        }

        return new DetectedFace(centers, found, spacing);
    }

    private static (double Origin, double Column, double Row)? Solve(List<Member> members, Func<Member, double> value)
    {
        double n = members.Count, sc = 0, sr = 0, scc = 0, srr = 0, scr = 0, sv = 0, scv = 0, srv = 0;
        foreach (var member in members)
        {
            var target = value(member);
            sc += member.Column;
            sr += member.Row;
            scc += member.Column * member.Column;
            srr += member.Row * member.Row;
            scr += member.Column * member.Row;
            sv += target;
            scv += member.Column * target;
            srv += member.Row * target;
        }

        var determinant = Determinant(n, sc, sr, sc, scc, scr, sr, scr, srr);
        if (Math.Abs(determinant) < 1e-9)
        {
            return null;
        }

        return (
            Determinant(sv, sc, sr, scv, scc, scr, srv, scr, srr) / determinant,
            Determinant(n, sv, sr, sc, scv, scr, sr, srv, srr) / determinant,
            Determinant(n, sc, sv, sc, scc, scv, sr, scr, srv) / determinant);
    }

    private static double Determinant(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
        a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

    private static bool SimilarSize(StickerBlob a, StickerBlob b) =>
        Math.Max(a.Area, b.Area) <= MaxAreaRatio * Math.Min(a.Area, b.Area);

    private static double Distance(StickerBlob a, StickerBlob b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static double Length(ScanPoint vector) => Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);

    private static ScanPoint Negate(ScanPoint vector) => new(-vector.X, -vector.Y);
}
