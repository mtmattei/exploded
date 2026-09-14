using System;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>
/// The stage: one canvas element that draws all five sheets.
///
/// The whole drawing is a single element on purpose. As five layers of XAML
/// Paths it was five sheets' worth of composition shape visuals, and Uno
/// recomputes an anti-aliasing damage path per shape visual whose transform
/// changes, which put a blocking second on the UI thread for every step of the
/// separation slider. Here the sheets move inside the canvas, so a separation
/// change is one invalidation and five picture replays.
/// </summary>
public sealed partial class PlateCanvas : SKCanvasElement
{
    public static readonly DependencyProperty SeparationProperty =
        DependencyProperty.Register(
            nameof(Separation),
            typeof(double),
            typeof(PlateCanvas),
            new PropertyMetadata(0d, OnStageChanged));

    public static readonly DependencyProperty SelectedLayerProperty =
        DependencyProperty.Register(
            nameof(SelectedLayer),
            typeof(int),
            typeof(PlateCanvas),
            new PropertyMetadata(-1, OnStageChanged));

    // RenderOverride runs on the render thread, so what it reads is snapshotted
    // here on the UI thread rather than read back off the dependency properties.
    private double _separation;
    private int _selectedLayer = -1;
    private PlateRenderer? _renderer;

    public PlateCanvas()
    {
        if (!IsSupportedOnCurrentPlatform())
        {
            throw new PlatformNotSupportedException(
                "The plate is drawn with SKCanvasElement, which needs Skia rendering on this platform.");
        }

        Unloaded += (_, _) =>
        {
            _renderer?.Dispose();
            _renderer = null;
        };
    }

    public double Separation
    {
        get => (double)GetValue(SeparationProperty);
        set => SetValue(SeparationProperty, value);
    }

    /// <summary>Index of the highlighted sheet, or -1 for none.</summary>
    public int SelectedLayer
    {
        get => (int)GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    /// <summary>Built on the UI thread, because the palette comes from the resource dictionary.</summary>
    internal void UseRenderer(PlateRenderer renderer)
    {
        _renderer?.Dispose();
        _renderer = renderer;
        Invalidate();
    }

    /// <summary>Which sheet is under a point in this element's coordinates, or -1.</summary>
    public int LayerAt(Point point) => _renderer?.HitTest(point, _separation) ?? -1;

    protected override void RenderOverride(SKCanvas canvas, Size area)
        => _renderer?.Render(canvas, _separation, _selectedLayer);

    private static void OnStageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var canvas = (PlateCanvas)sender;
        canvas._separation = canvas.Separation;
        canvas._selectedLayer = canvas.SelectedLayer;
        canvas.Invalidate();
    }
}
