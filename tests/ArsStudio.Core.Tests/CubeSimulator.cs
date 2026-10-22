namespace ArsStudio.Core.Tests;

internal static class CubeSimulator
{
    public static readonly string[] FaceTurns =
        "U U2 U' R R2 R' F F2 F' D D2 D' L L2 L' B B2 B'".Split(' ');

    public static CubeState Scramble(int seed, int length = 25)
    {
        var random = new Random(seed);
        var moves = Enumerable.Range(0, length).Select(_ => FaceTurns[random.Next(FaceTurns.Length)]);
        return CubeMoves.Apply(CubeState.Solved, string.Join(' ', moves));
    }
}
