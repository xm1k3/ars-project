using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwoPhaseSolver;

namespace ArsStudio.Core;

public sealed record CubeSolution(string Moves, string MachineCode);

public static class CubeSolver
{
    private const int MaxDepth = 30;
    private const int TimeoutMilliseconds = 6000;
    private const int OptimizeMilliseconds = 1500;

    private static readonly Lock SearchLock = new();

    public static CubeSolution Solve(CubeState state, SolveMethod method = SolveMethod.Kociemba)
    {
        CubeValidator.Validate(state);

        var facelets = ToFacelets(state);
        var orientation = CubeOrientation.Normalize(facelets);
        var cube = CubieMapper.ToCube(facelets);
        EnsureSolvable(cube);

        if (cube.isSolved())
        {
            return new CubeSolution("", "");
        }

        var moves = method switch
        {
            SolveMethod.Layers => LayerSolver.Solve(CubeMoves.Normalize(state)),
            SolveMethod.Optimized => Shorten(cube, TwoPhase(cube, MaxDepth, TimeoutMilliseconds)),
            _ => TwoPhase(cube, MaxDepth, TimeoutMilliseconds),
        };

        return new CubeSolution(moves, MachineEncoder.Encode(orientation, moves));
    }

    private static string TwoPhase(Cube cube, int maxDepth, int timeout)
    {
        try
        {
            lock (SearchLock)
            {
                return Search.fullSolve(cube, maxDepth, timeout).ToString();
            }
        }
        catch (Exception exception) when (exception is TimeoutException || exception.GetType() == typeof(Exception))
        {
            throw new InvalidCubeException("Non è stato possibile trovare una soluzione.");
        }
    }

    private static string Shorten(Cube cube, string moves)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < OptimizeMilliseconds)
        {
            var length = CubeMoves.Split(moves).Count;
            var remaining = (int)(OptimizeMilliseconds - watch.ElapsedMilliseconds);
            try
            {
                moves = TwoPhase(cube, length - 1, remaining);
            }
            catch (InvalidCubeException)
            {
                break;
            }
        }

        return moves;
    }

    public static bool IsSolvable(CubeState state)
    {
        if (state.Stickers.Contains(CubeColor.Empty) ||
            state.Stickers.GroupBy(color => color).Any(group => group.Count() != CubeState.StickersPerFace) ||
            Enumerable.Range(0, CubeState.FaceCount).Select(face => state.Face((CubeFace)face)[4]).Distinct().Count() != CubeState.FaceCount)
        {
            return false;
        }

        if (CubeDiagnostics.HasMirroredCorner(state))
        {
            return false;
        }

        try
        {
            var facelets = ToFacelets(state);
            CubeOrientation.Normalize(facelets);
            EnsureSolvable(CubieMapper.ToCube(facelets));
            return true;
        }
        catch (InvalidCubeException)
        {
            return false;
        }
    }

    private static int[] ToFacelets(CubeState state)
    {
        var facelets = new int[CubeState.StickerCount];
        for (var face = 0; face < CubeState.FaceCount; face++)
        {
            var stickers = state.Face((CubeFace)face);
            var outer = stickers.Where((_, index) => index != 4).ToArray();
            for (var i = 0; i < outer.Length; i++)
            {
                facelets[face * 8 + i] = (int)outer[i];
            }

            facelets[48 + face] = (int)stickers[4];
        }

        return facelets;
    }

    private static void EnsureSolvable(Cube cube)
    {
        var cornerPositions = cube.corners.Select(cubie => (int)cubie.pos).ToArray();
        var edgePositions = cube.edges.Select(cubie => (int)cubie.pos).ToArray();

        var problems = new List<string>();
        if (cornerPositions.Distinct().Count() != cornerPositions.Length || edgePositions.Distinct().Count() != edgePositions.Length)
        {
            problems.Add("Alcuni pezzi compaiono due volte.");
        }

        if (cube.corners.Sum(cubie => cubie.orient) % 3 != 0)
        {
            problems.Add("Un angolo risulta ruotato su se stesso: controlla l'ordine dei colori sugli angoli.");
        }

        if (cube.edges.Sum(cubie => cubie.orient) % 2 != 0)
        {
            problems.Add("Uno spigolo risulta capovolto: controlla i due colori di ogni spigolo.");
        }

        if (problems.Count == 0 && Parity(cornerPositions) != Parity(edgePositions))
        {
            problems.Add("Due pezzi risultano scambiati tra loro: controlla gli spigoli o gli angoli con colori simili.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidCubeException("Il cubo non è risolvibile." + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }
    }

    private static int Parity(int[] permutation)
    {
        var inversions = 0;
        for (var i = 0; i < permutation.Length; i++)
        {
            for (var j = i + 1; j < permutation.Length; j++)
            {
                if (permutation[i] > permutation[j])
                {
                    inversions++;
                }
            }
        }

        return inversions % 2;
    }
}
