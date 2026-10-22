using System;
using ArsStudio.Core;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class PaletteItemViewModel : ViewModelBase
{
    private readonly Action<CubeColor> _select;

    public PaletteItemViewModel(CubeColor color, Action<CubeColor> select)
    {
        Color = color;
        _select = select;
    }

    public CubeColor Color { get; }

    [RelayCommand]
    private void Select() => _select(Color);
}
