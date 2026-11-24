using Avalonia.Data.Converters;

namespace ArsStudio.Views;

public static class ScanLabels
{
    public static FuncValueConverter<bool, string> Freeze { get; } =
        new(frozen => frozen ? "Riprendi video" : "Istantanea");

    public static FuncValueConverter<bool, string> AutoCapture { get; } =
        new(enabled => enabled ? "Auto griglia: attiva" : "Auto griglia: spenta");

    public static FuncValueConverter<bool, string> Tracking { get; } =
        new(enabled => enabled ? "Rilevamento: automatico" : "Rilevamento: griglia fissa");
}
