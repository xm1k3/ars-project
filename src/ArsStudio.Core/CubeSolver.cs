using System;
using System.Linq;
using System.Threading;
using TwoPhaseSolver;

namespace ArsStudio.Core;

public sealed record CubeSolution(string Moves, string MachineCode);

public static class CubeSolver
{
    private const int MaxDepth = 30;
    private const int TimeoutMilliseconds = 6000;

    private static readonly Lock SearchLock = new();

    public static CubeSolution Solve(CubeState state)
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

        Move solution;
        try
        {
            lock (SearchLock)
            {
                solution = Search.fullSolve(cube, MaxDepth, TimeoutMilliseconds);
            }
        }
        catch (Exception exception) when (exception is TimeoutException || exception.GetType() == typeof(Exception))
        {
            throw new InvalidCubeException("Non è stato possibile trovare una soluzione.");
        }

        var moves = solution.ToString();
        return new CubeSolution(moves, MachineEncoder.Encode(orientation, moves));
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

        var solvable =
            cornerPositions.Distinct().Count() == cornerPositions.Length &&
            edgePositions.Distinct().Count() == edgePositions.Length &&
            cube.corners.Sum(cubie => cubie.orient) % 3 == 0 &&
            cube.edges.Sum(cubie => cubie.orient) % 2 == 0 &&
            Parity(cornerPositions) == Parity(edgePositions);

        if (!solvable)
        {
            throw new InvalidCubeException("Il cubo non è risolvibile, controlla i colori inseriti.");
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
