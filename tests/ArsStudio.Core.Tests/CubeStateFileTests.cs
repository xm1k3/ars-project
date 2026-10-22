namespace ArsStudio.Core.Tests;

public class CubeStateFileTests
{
    [Fact]
    public void Serialize_ThenParse_ReturnsSameState()
    {
        var state = CubeSimulator.Scramble(7);

        var parsed = CubeStateFile.Parse(CubeStateFile.Serialize(state));

        Assert.Equal(state.Stickers, parsed.Stickers);
    }

    [Fact]
    public void Serialize_WritesOneNumberPerLine()
    {
        var content = CubeStateFile.Serialize(CubeState.Solved);

        Assert.StartsWith("1\n1\n1\n1\n1\n1\n1\n1\n1\n3\n", content);
        Assert.Equal(CubeState.StickerCount, content.Count(c => c == '\n'));
    }

    [Fact]
    public void Parse_WindowsLineEndings_IsAccepted()
    {
        var content = CubeStateFile.Serialize(CubeState.Solved).Replace("\n", "\r\n");

        Assert.Equal(CubeState.Solved.Stickers, CubeStateFile.Parse(content).Stickers);
    }

    [Fact]
    public void Parse_UnknownNumber_BecomesEmpty()
    {
        var content = "9\n" + string.Concat(Enumerable.Repeat("1\n", CubeState.StickerCount - 1));

        Assert.Equal(CubeColor.Empty, CubeStateFile.Parse(content).Stickers[0]);
    }

    [Fact]
    public void Parse_TooFewLines_Throws()
    {
        Assert.Throws<FormatException>(() => CubeStateFile.Parse("1\n2\n3\n"));
    }

    [Fact]
    public void Parse_NotANumber_Throws()
    {
        var content = "x\n" + string.Concat(Enumerable.Repeat("1\n", CubeState.StickerCount - 1));

        Assert.Throws<FormatException>(() => CubeStateFile.Parse(content));
    }
}
