namespace ArsStudio.Core.Tests;

public class CubeMovesTests
{
    [Theory]
    [InlineData("R R R R")]
    [InlineData("U U'")]
    [InlineData("F2 F2")]
    [InlineData("R U R' U' R U R' U' R U R' U' R U R' U' R U R' U' R U R' U'")]
    public void Apply_IdentitySequence_ReturnsSameState(string moves)
    {
        var scrambled = CubeSimulator.Scramble(11);

        Assert.Equal(scrambled.Stickers, CubeMoves.Apply(scrambled, moves).Stickers);
    }

    [Fact]
    public void Apply_SingleMove_ChangesState()
    {
        Assert.False(CubeMoves.IsSolved(CubeMoves.Apply(CubeState.Solved, "R")));
    }

    [Fact]
    public void Apply_U_MovesFrontRowToLeft()
    {
        var turned = CubeMoves.Apply(CubeState.Solved, "U");

        Assert.All(turned.Face(CubeFace.Left).Take(3), color => Assert.Equal(CubeColor.Green, color));
    }

    [Theory]
    [InlineData('F', 1, 'U', 0)]
    [InlineData('R', 3, 'U', 2)]
    [InlineData('F', 2, 'U', 3)]
    public void Normalize_RotatedCube_PutsWhiteUpAndGreenFront(char axis, int turns, char spinAxis, int spins)
    {
        var scrambled = CubeSimulator.Scramble(5);
        var rotated = CubeMoves.RotateWholeCube(CubeMoves.RotateWholeCube(scrambled, spinAxis, spins), axis, turns);

        Assert.Equal(scrambled.Stickers, CubeMoves.Normalize(rotated).Stickers);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("R3")]
    [InlineData("")]
    public void Parse_InvalidMove_Throws(string move)
    {
        Assert.Throws<FormatException>(() => CubeMove.Parse(move));
    }
}
