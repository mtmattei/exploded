using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Exploded.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Exploded.Presentation;

/// <summary>
/// Token brushes, mirrored for code use. Custom dependency properties inside a
/// DataTemplate do not receive ThemeResource, so anything a row computes has to
/// resolve its brush here rather than through markup.
/// </summary>
internal static class Palette
{
    public static Brush Get(string key) => (Brush)Application.Current.Resources[key];

    public static Brush Ink => Get("Ink");
    public static Brush InkDim => Get("InkDim");
    public static Brush InkFaint => Get("InkFaint");
    public static Brush Callout => Get("Callout");
    public static Brush Board => Get("Board");
    public static Brush RowSelected => Get("RowSelected");
}

/// <summary>
/// One row of the parts table. A small mutable view object rather than an
/// immutable record, because IsSelected and IsInBuild are exactly the "a few
/// observable properties" case: the row has to tell the template it changed.
/// </summary>
public sealed class PartRow : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isInBuild;

    public PartRow(Part part) => Part = part;

    public Part Part { get; }

    public int LayerIndex => Part.LayerIndex;
    public string NumberLabel => Part.Number.ToString("00");
    public string Name => Part.Name;
    public string Spec => Part.Spec;
    public string Sku => Part.Sku;
    public string PriceLabel => Part.PriceLabel;
    public string StatusLabel => Part.StatusLabel;
    public bool IsAvailable => Part.IsAvailable;

    public Brush SwatchBrush => Palette.Get(Part.Swatch);

    /// <summary>The hatch that marks an unavailable part, so stock is never colour-alone.</summary>
    public Geometry HatchGeometry => Hatch;

    public Visibility HatchVisibility => IsAvailable ? Visibility.Collapsed : Visibility.Visible;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            Raise(nameof(IsSelected));
            Raise(nameof(NumberBrush));
            Raise(nameof(RowBackground));
        }
    }

    public bool IsInBuild
    {
        get => _isInBuild;
        set
        {
            if (_isInBuild == value)
            {
                return;
            }

            _isInBuild = value;
            Raise(nameof(IsInBuild));
            Raise(nameof(InBuildVisibility));
            Raise(nameof(AutomationName));
        }
    }

    /// <summary>What a screen reader announces for the row: everything the row shows, in reading order.</summary>
    public string AutomationName =>
        $"Part {Part.Number}, {Name}, {Spec}, {PriceLabel}, {StatusLabel.ToLowerInvariant()}"
        + (IsInBuild ? ", in build" : string.Empty);

    public Brush NumberBrush => IsSelected ? Palette.Callout : Palette.InkFaint;

    public Brush? RowBackground => IsSelected ? Palette.RowSelected : null;

    public Visibility InBuildVisibility => IsInBuild ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// A 45 degree hatch across the material swatch. Built as geometry rather
    /// than a tile brush because XAML has no pattern fill, and drawn once as a
    /// shared static since every unavailable row wants the identical figure.
    /// </summary>
    private static readonly Geometry Hatch = BuildHatch(18d, 4d);

    private static Geometry BuildHatch(double size, double spacing)
    {
        var group = new GeometryGroup();

        // Each line is x + y = c. Where it enters and leaves the box depends on
        // whether c has passed the far corner, which is the whole of the maths.
        for (var c = spacing; c < size * 2d; c += spacing)
        {
            var start = c <= size ? new Point(0d, c) : new Point(c - size, size);
            var end = c <= size ? new Point(c, 0d) : new Point(size, c - size);

            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = end });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            group.Children.Add(geometry);
        }

        return group;
    }
}
