using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class MoveChipViewModel : ViewModelBase
{
    private readonly Action<int> _select;

    public MoveChipViewModel(int index, string text, Action<int> select)
    {
        Index = index;
        Text = text;
        _select = select;
    }

    public int Index { get; }

    public string Text { get; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsUpcoming { get; set; }

    [RelayCommand]
    private void Select() => _select(Index);
}
