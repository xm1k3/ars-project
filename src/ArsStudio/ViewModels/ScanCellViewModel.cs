using ArsStudio.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArsStudio.ViewModels;

public partial class ScanCellViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial CubeColor Color { get; set; }

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Size { get; set; }
}
