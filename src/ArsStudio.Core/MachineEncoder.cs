using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArsStudio.Core;

public static class MachineEncoder
{
    private const string Commands = "abcdefghijklmnopqr";
    private const string Inverses = "badcfehgjilknmporq";

    private static readonly Dictionary<string, string> Sequences = new()
    {
        ["1"] = "jnilmk",
        ["2"] = "jmilnk",
        ["3"] = "lokjpi",
        ["4"] = "lpkjoi",
        ["5"] = "lokjpmilnpkjoi",
        ["6"] = "lokjpnilmpkjoi",
        ["7"] = "jmmi",
        ["8"] = "jmmi",
        ["9"] = "lppk",
        ["/"] = "lppk",
        ["("] = "lokjommpilpk",
        [")"] = "lokjommpilpk",
        ["L"] = "flek",
        ["L'"] = "elfk",
        ["R"] = "blak",
        ["R'"] = "albk",
        ["U"] = "lmkjnihjgilnkjmi",
        ["U'"] = "lmkjnigjhilnkjmi",
        ["D"] = "lmkjnidjcmilnk",
        ["D'"] = "lmkjnicjdmilnk",
        ["F"] = "djci",
        ["F'"] = "cjdi",
        ["B"] = "hjgi",
        ["B'"] = "gjhi",
        ["M"] = "lokp",
        ["M'"] = "lpko",
        ["E"] = "lmkjnilpkolnkjmi",
        ["E'"] = "lmkjnilokplnkjmi",
        ["S"] = "jmin",
        ["S'"] = "jnim",
        ["L2"] = "ff",
        ["R2"] = "bb",
        ["U2"] = "jnilmkggjmilnk",
        ["D2"] = "jnilmkddjmilnk",
        ["F2"] = "cc",
        ["B2"] = "gg",
        ["M2"] = "oolppk",
        ["E2"] = "jnilmkoolookjmilnk",
        ["S2"] = "nnjmmi",
    };

    public static string Encode(string orientation, string moves)
    {
        var sequence = (orientation + moves)
            .Split(' ')
            .Select(token => Sequences.GetValueOrDefault(token, ""));

        return Simplify(string.Concat(sequence));
    }

    private static string Simplify(string sequence)
    {
        var result = new StringBuilder();
        foreach (var command in sequence)
        {
            if (result.Length > 0 && IsInverse(result[^1], command))
            {
                result.Length--;
            }
            else
            {
                result.Append(command);
            }
        }

        return result.ToString();
    }

    private static bool IsInverse(char first, char second)
    {
        var index = Commands.IndexOf(first);
        return index >= 0 && Inverses[index] == second;
    }
}
