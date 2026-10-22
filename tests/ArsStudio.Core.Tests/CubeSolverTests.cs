namespace ArsStudio.Core.Tests;

public class CubeSolverTests
{
    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 10));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Solve_ScrambledCube_ReturnsMovesThatSolveIt(int seed)
    {
        var scrambled = CubeSimulator.Scramble(seed);

        var solution = CubeSolver.Solve(scrambled);

        Assert.True(CubeMoves.IsSolved(CubeMoves.Apply(scrambled, solution.Moves)));
        Assert.NotEmpty(solution.MachineCode);
    }

    [Fact]
    public void Solve_SolvedCube_ReturnsEmptySolution()
    {
        var solution = CubeSolver.Solve(CubeState.Solved);

        Assert.Equal(new CubeSolution("", ""), solution);
    }

    public static TheoryData<char, int, char, int> Orientations
    {
        get
        {
            var data = new TheoryData<char, int, char, int>();
            foreach (var (axis, turns) in new[] { ('U', 0), ('F', 1), ('F', 2), ('F', 3), ('R', 1), ('R', 3) })
            {
                for (var spin = 0; spin < 4; spin++)
                {
                    data.Add(axis, turns, 'U', spin);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void Solve_RotatedCube_ReturnsSameMovesAsStandardOrientation(char axis, int turns, char spinAxis, int spins)
    {
        var scrambled = CubeSimulator.Scramble(42);
        var rotated = CubeMoves.RotateWholeCube(CubeMoves.RotateWholeCube(scrambled, spinAxis, spins), axis, turns);

        var expected = CubeSolver.Solve(scrambled);
        var actual = CubeSolver.Solve(rotated);

        Assert.Equal(expected.Moves, actual.Moves);
    }

    [Fact]
    public void Solve_EmptySticker_Throws()
    {
        var stickers = CubeState.Solved.Stickers.ToArray();
        stickers[0] = CubeColor.Empty;

        var exception = Assert.Throws<InvalidCubeException>(() => CubeSolver.Solve(new CubeState(stickers)));

        Assert.Equal("Ci sono caselle senza colore.", exception.Message);
    }

    [Fact]
    public void Solve_WrongColorCount_Throws()
    {
        var stickers = CubeState.Solved.Stickers.ToArray();
        stickers[0] = CubeColor.Red;

        var exception = Assert.Throws<InvalidCubeException>(() => CubeSolver.Solve(new CubeState(stickers)));

        Assert.Equal("Ogni colore deve comparire esattamente 9 volte.", exception.Message);
    }

    [Fact]
    public void Solve_TwistedCorner_Throws()
    {
        var stickers = CubeState.Solved.Stickers.ToArray();
        (stickers[8], stickers[20], stickers[27]) = (stickers[20], stickers[27], stickers[8]);

        Assert.Throws<InvalidCubeException>(() => CubeSolver.Solve(new CubeState(stickers)));
    }

    [Fact]
    public void Solve_FlippedEdge_Throws()
    {
        var stickers = CubeState.Solved.Stickers.ToArray();
        (stickers[7], stickers[19]) = (stickers[19], stickers[7]);

        Assert.Throws<InvalidCubeException>(() => CubeSolver.Solve(new CubeState(stickers)));
    }
}
