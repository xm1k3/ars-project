using System;
using System.ComponentModel;
using System.Linq;
using ArsStudio.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ArsStudio.Views;

public partial class SolveView : UserControl
{
    private MainViewModel? _viewModel;

    public SolveView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentStep))
        {
            Dispatcher.UIThread.Post(ShowCurrentMove, DispatcherPriority.Background);
        }
    }

    private void ShowCurrentMove()
    {
        if (_viewModel is null)
        {
            return;
        }

        var current = _viewModel.MoveChips.FirstOrDefault(chip => chip.IsCurrent);
        if (current is null)
        {
            MovesScroll.Offset = default;
            return;
        }

        Moves.ContainerFromIndex(current.Index)?.BringIntoView();
    }
}
