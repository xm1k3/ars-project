using System;
using System.Globalization;
using ArsStudio.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace ArsStudio.Converters;

public class CubeColorToBrushConverter : IValueConverter
{
    public static CubeColorToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CubeColor color || Application.Current is not { } app)
        {
            return null;
        }

        return app.TryGetResource($"Cube{color}Brush", app.ActualThemeVariant, out var brush) ? brush : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
