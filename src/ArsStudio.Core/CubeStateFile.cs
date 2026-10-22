using System;
using System.Globalization;
using System.Linq;

namespace ArsStudio.Core;

public static class CubeStateFile
{
    public const string Extension = "cst";

    public static string Serialize(CubeState state) =>
        string.Concat(state.Stickers.Select(color => ((int)color).ToString(CultureInfo.InvariantCulture) + "\n"));

    public static CubeState Parse(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length < CubeState.StickerCount)
        {
            throw new FormatException("Il file non contiene tutte le caselle del cubo.");
        }

        return new CubeState(lines.Take(CubeState.StickerCount).Select(ParseColor));
    }

    private static CubeColor ParseColor(string line)
    {
        var value = short.Parse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        return Enum.IsDefined((CubeColor)value) ? (CubeColor)value : CubeColor.Empty;
    }
}
