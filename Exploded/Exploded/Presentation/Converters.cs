using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Exploded.Presentation;

/// <summary>
/// Token brushes, mirrored for code use: the stage and the callout bubbles are
/// built in code and cannot resolve StaticResource.
/// </summary>
internal static class Palette
{
    public static Brush Get(string key) => (Brush)Application.Current.Resources[key];

    public static Brush Callout => Get("Callout");
    public static Brush Paper => Get("Paper");
}

/// <summary>Picks one of two values from a bool. Declared per use in resources with its two values.</summary>
public sealed class BoolToValueConverter : IValueConverter
{
    public object? WhenTrue { get; set; }
    public object? WhenFalse { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is true ? WhenTrue : WhenFalse;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>Resolves a token key carried as data (a part's material swatch) to its brush.</summary>
public sealed class TokenBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is string key ? Palette.Get(key) : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
