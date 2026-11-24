using System;

namespace ArsStudio.Core;

public static class CubeValidator
{
    public static void Validate(CubeState state)
    {
        var problems = CubeDiagnostics.Problems(state);
        if (problems.Count > 0)
        {
            throw new InvalidCubeException(string.Join(Environment.NewLine, problems));
        }
    }
}
