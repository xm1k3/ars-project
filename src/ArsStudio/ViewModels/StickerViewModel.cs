using System;
using ArsStudio.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class StickerViewModel : ViewModelBase
{
    private readonly Func<CubeColor> _selectedColor;
    private readonly Action _painted;

    public StickerViewModel(CubeColor color, Func<CubeColor> selectedColor, Action painted)
    {
        Color = color;
        _selectedColor = selectedColor;
        _painted = painted;
    }

    [ObservableProperty]
    public partial CubeColor Color { get; set; }

    [ObservableProperty]
    public partial bool IsDoubtful { get; set; }

    [RelayCommand]
    private void Paint()
    {
        IsDoubtful = false;
        Color = _selectedColor();
        _painted();
    }
}
