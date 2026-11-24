using System;
using ArsStudio.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class ScanStepViewModel : ViewModelBase
{
    private readonly Action<CubeFace> _clear;

    public ScanStepViewModel(CubeFace face, CubeColor centerColor, string label, Action<CubeFace> clear)
    {
        Face = face;
        CenterColor = centerColor;
        Label = label;
        _clear = clear;
    }

    public CubeFace Face { get; }

    public CubeColor CenterColor { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [RelayCommand]
    private void Clear() => _clear(Face);
}
