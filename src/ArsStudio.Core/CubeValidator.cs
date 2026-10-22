using System.Linq;

namespace ArsStudio.Core;

public static class CubeValidator
{
    public static void Validate(CubeState state)
    {
        if (state.Stickers.Contains(CubeColor.Empty))
        {
            throw new InvalidCubeException("Ci sono caselle senza colore.");
        }

        if (state.Stickers.GroupBy(color => color).Any(group => group.Count() != CubeState.StickersPerFace))
        {
            throw new InvalidCubeException("Ogni colore deve comparire esattamente 9 volte.");
        }

        var centers = Enumerable.Range(0, CubeState.FaceCount).Select(face => state.Face((CubeFace)face)[4]);
        if (centers.Distinct().Count() != CubeState.FaceCount)
        {
            throw new InvalidCubeException("I centri delle facce devono avere colori diversi.");
        }
    }
}
