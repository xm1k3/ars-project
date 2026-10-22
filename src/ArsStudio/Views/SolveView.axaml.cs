using ArsStudio.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace ArsStudio.Views;

public partial class SolveView : UserControl
{
    public SolveView()
    {
        InitializeComponent();
    }

    private void OnSolutionTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ToggleSolutionFormatCommand.Execute(null);
        }
    }
}
