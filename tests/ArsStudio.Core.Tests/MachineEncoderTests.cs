namespace ArsStudio.Core.Tests;

public class MachineEncoderTests
{
    [Theory]
    [InlineData("", "R", "blak")]
    [InlineData("", "R2", "bb")]
    [InlineData("", "F F'", "djcicjdi")]
    [InlineData("2 ", "D", "djcmilnk")]
    [InlineData("4 ", "F", "lpkjoidjci")]
    [InlineData("", "None", "")]
    public void Encode_TranslatesMovesToMachineCommands(string orientation, string moves, string expected)
    {
        Assert.Equal(expected, MachineEncoder.Encode(orientation, moves));
    }
}
