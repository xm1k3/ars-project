using System;
using System.Collections.Generic;
using System.Linq;
using ArsStudio.Core;

namespace ArsStudio.ViewModels;

public class FaceViewModel : ViewModelBase
{
    public FaceViewModel(Func<CubeColor> selectedColor, Action painted)
    {
        Stickers = Enumerable.Range(0, CubeState.StickersPerFace)
            .Select(_ => new StickerViewModel(CubeColor.Empty, selectedColor, painted))
            .ToArray();
    }

    public IReadOnlyList<StickerViewModel> Stickers { get; }

    public IEnumerable<CubeColor> Colors => Stickers.Select(sticker => sticker.Color);

    public void SetColors(IEnumerable<CubeColor> colors)
    {
        foreach (var (sticker, color) in Stickers.Zip(colors))
        {
            sticker.Color = color;
        }
    }
}
