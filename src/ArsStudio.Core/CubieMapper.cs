using System.Collections.Generic;
using System.Linq;
using TwoPhaseSolver;

namespace ArsStudio.Core;

internal static class CubieMapper
{
    private const string MappingError = "Il cubo non è stato colorato correttamente.";

    private static readonly int[][] CornerColors =
    [
        [1, 5, 4],
        [1, 5, 3],
        [1, 6, 3],
        [1, 6, 4],
        [2, 5, 4],
        [2, 5, 3],
        [2, 3, 6],
        [2, 4, 6],
    ];

    private static readonly int[][] EdgeColors =
    [
        [1, 4],
        [1, 5],
        [1, 3],
        [1, 6],
        [4, 2],
        [5, 2],
        [3, 2],
        [6, 2],
        [4, 5],
        [3, 5],
        [3, 6],
        [4, 6],
    ];

    private static readonly (int[] Facelets, byte[] Orientations)[] Corners =
    [
        ([7, 18, 24], [0, 2, 1]),
        ([5, 16, 10], [0, 1, 2]),
        ([0, 8, 34], [0, 1, 2]),
        ([2, 26, 32], [0, 2, 1]),
        ([23, 29, 42], [1, 2, 0]),
        ([15, 21, 40], [1, 2, 0]),
        ([13, 39, 45], [2, 1, 0]),
        ([47, 31, 37], [0, 1, 2]),
    ];

    private static readonly int[][] Edges =
    [
        [4, 25],
        [6, 17],
        [3, 9],
        [1, 33],
        [44, 30],
        [41, 22],
        [43, 14],
        [46, 38],
        [20, 27],
        [19, 12],
        [36, 11],
        [35, 28],
    ];

    public static Cube ToCube(int[] facelets)
    {
        var corners = Corners.Select(corner => MapCorner(facelets, corner.Facelets, corner.Orientations)).ToArray();
        var edges = Edges.Select(edge => MapEdge(facelets, edge[0], edge[1])).ToArray();
        return new Cube(corners, edges);
    }

    private static Cubie MapCorner(int[] facelets, int[] positions, byte[] orientations)
    {
        var colors = positions.Select(position => facelets[position]).ToArray();
        var block = FindBlock(CornerColors, colors);
        var whiteOrYellow = colors.ToList().FindIndex(IsWhiteOrYellow);
        var orientation = whiteOrYellow >= 0 ? orientations[whiteOrYellow] : (byte)0;
        return new Cubie(block, orientation);
    }

    private static Cubie MapEdge(int[] facelets, int first, int second)
    {
        var a = facelets[first];
        var b = facelets[second];
        var block = FindBlock(EdgeColors, [a, b]);

        byte orientation = 0;
        if (IsWhiteOrYellow(a) || IsOrangeOrRed(b))
        {
            orientation = 0;
        }
        else if (IsWhiteOrYellow(b) || IsOrangeOrRed(a))
        {
            orientation = 1;
        }

        return new Cubie(block, orientation);
    }

    private static byte FindBlock(IReadOnlyList<int[]> blocks, int[] colors)
    {
        if (colors.Distinct().Count() == colors.Length)
        {
            for (var block = 0; block < blocks.Count; block++)
            {
                if (blocks[block].All(colors.Contains))
                {
                    return (byte)block;
                }
            }
        }

        throw new InvalidCubeException(MappingError);
    }

    private static bool IsWhiteOrYellow(int color) => color is (int)CubeColor.White or (int)CubeColor.Yellow;

    private static bool IsOrangeOrRed(int color) => color is (int)CubeColor.Orange or (int)CubeColor.Red;
}
