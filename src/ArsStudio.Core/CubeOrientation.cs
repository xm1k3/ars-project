namespace ArsStudio.Core;

internal static class CubeOrientation
{
    private const int White = (int)CubeColor.White;
    private const int Green = (int)CubeColor.Green;

    private static readonly int[] RotationAroundUp =
    [
        2, 4, 7, 1, 6, 0, 3, 5,
        32, 33, 34, 35, 36, 37, 38, 39,
        8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 18, 19, 20, 21, 22, 23,
        24, 25, 26, 27, 28, 29, 30, 31,
        45, 43, 40, 46, 41, 47, 44, 42,
        48, 52, 49, 50, 51, 53,
    ];

    private static readonly int[] RotationAroundLeft =
    [
        16, 17, 18, 19, 20, 21, 22, 23,
        10, 12, 15, 9, 14, 8, 11, 13,
        40, 41, 42, 43, 44, 45, 46, 47,
        29, 27, 24, 30, 25, 31, 28, 26,
        7, 6, 5, 4, 3, 2, 1, 0,
        39, 38, 37, 36, 35, 34, 33, 32,
        50, 49, 53, 51, 48, 52,
    ];

    private static readonly int[] RotationAroundFront =
    [
        13, 11, 8, 14, 9, 15, 12, 10,
        45, 43, 40, 46, 41, 47, 44, 42,
        21, 19, 16, 22, 17, 23, 20, 18,
        5, 3, 0, 6, 1, 7, 4, 2,
        34, 36, 39, 33, 38, 32, 35, 37,
        29, 27, 24, 30, 25, 31, 28, 26,
        49, 53, 50, 48, 52, 51,
    ];

    public static string Normalize(int[] facelets)
    {
        var code = "";

        if (facelets[49] == White)
        {
            Rotate(facelets, RotationAroundFront, 1);
            code = "4 ";
        }
        else if (facelets[50] == White)
        {
            Rotate(facelets, RotationAroundLeft, 1);
            code = "1 ";
        }
        else if (facelets[51] == White)
        {
            Rotate(facelets, RotationAroundFront, 3);
            code = "3 ";
        }
        else if (facelets[52] == White)
        {
            Rotate(facelets, RotationAroundLeft, 3);
            code = "2 ";
        }
        else if (facelets[53] == White)
        {
            Rotate(facelets, RotationAroundLeft, 2);
            code = "7 ";
        }

        if (facelets[49] == Green)
        {
            Rotate(facelets, RotationAroundUp, 1);
            code += "6 ";
        }
        else if (facelets[51] == Green)
        {
            Rotate(facelets, RotationAroundUp, 3);
            code += "5 ";
        }
        else if (facelets[52] == Green)
        {
            Rotate(facelets, RotationAroundUp, 2);
            code += "( ";
        }

        return code;
    }

    private static void Rotate(int[] facelets, int[] map, int times)
    {
        for (var turn = 0; turn < times; turn++)
        {
            var source = (int[])facelets.Clone();
            for (var i = 0; i < facelets.Length; i++)
            {
                facelets[i] = source[map[i]];
            }
        }
    }
}
