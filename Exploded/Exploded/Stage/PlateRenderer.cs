using System;
using SkiaSharp;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>The plate palette, resolved from the token dictionary on the UI thread.</summary>
internal readonly record struct PlatePalette(
    SKColor Ink,
    SKColor InkFaint,
    SKColor Sweep,
    SKColor Paper,
    SKColor Callout,
    SKColor Board,
    SKColor Keycap,
    SKColor Housing,
    SKColor Steel,
    SKColor Aluminium);

/// <summary>
/// Draws the exploded plate.
///
/// Each sheet's art is recorded once into an <see cref="SKPicture"/> in
/// layer-local coordinates and replayed under that sheet's matrix, so a
/// separation change costs five matrix concatenations and five picture
/// replays rather than rebuilding any geometry. Paints are built once and
/// reused: <see cref="Render"/> runs on the render thread and must not
/// allocate.
/// </summary>
internal sealed class PlateRenderer : IDisposable
{
    private readonly SKPicture[] _sheets = new SKPicture[Explode.LayerCount];
    private readonly SKPath[] _footprints = new SKPath[Explode.LayerCount];
    private readonly SKPaint _selection;
    private bool _disposed;

    public PlateRenderer(PlatePalette palette)
    {
        _selection = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.2f,
            Color = palette.Callout,
            IsAntialias = true
        };

        // selection outlines trace each layer's own footprint, not its detail
        _footprints[0] = PlatePaths.CaseShell();
        _footprints[1] = PlatePaths.Board();
        _footprints[2] = PlatePaths.PlateOutline();
        _footprints[3] = PlatePaths.FieldOutline();
        _footprints[4] = PlatePaths.FieldOutline();

        _sheets[0] = RecordCase(palette);
        _sheets[1] = RecordBoard(palette);
        _sheets[2] = RecordPlate(palette);
        _sheets[3] = RecordSwitches(palette);
        _sheets[4] = RecordKeycaps(palette);
    }

    /// <summary>
    /// Layer 0 is the case at the bottom of the stack; layer 4 is the keycaps
    /// on top, so the sheets draw in index order.
    /// </summary>
    public void Render(SKCanvas canvas, double separation, int selectedLayer)
    {
        if (_disposed)
        {
            return;
        }

        var halfWidth = (float)(KeyLayout.CaseWidth / 2d);
        var halfHeight = (float)(KeyLayout.CaseHeight / 2d);

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var sheet = ToSkia(Explode.SheetMatrix(separation, layer));

            canvas.Save();

            // the sheet matrix is written about the sheet's own centre, the way
            // RenderTransformOrigin="0.5,0.5" reads in markup
            canvas.Translate((float)Explode.StackCentreX, (float)Explode.StackCentreY);
            canvas.Concat(sheet);
            canvas.Translate(-halfWidth, -halfHeight);

            canvas.DrawPicture(_sheets[layer]);

            if (layer == selectedLayer)
            {
                canvas.DrawPath(_footprints[layer], _selection);
            }

            canvas.Restore();
        }
    }

    /// <summary>
    /// Which sheet a point in stage coordinates lands on, topmost first, or -1
    /// for a miss. Each sheet's matrix is inverted rather than transforming the
    /// footprint, so the test happens in the layer's own coordinates.
    /// </summary>
    public int HitTest(Point stagePoint, double separation)
    {
        var halfWidth = (float)(KeyLayout.CaseWidth / 2d);
        var halfHeight = (float)(KeyLayout.CaseHeight / 2d);

        for (var layer = Explode.LayerCount - 1; layer >= 0; layer--)
        {
            var sheet = ToSkia(Explode.SheetMatrix(separation, layer));

            if (!sheet.TryInvert(out var inverse))
            {
                continue;
            }

            // undo the placement by hand rather than composing matrices: the
            // sheet matrix is applied about the stack centre, so the point is
            // taken there, run back through the sheet, then moved into the
            // layer's own top-left origin.
            var centred = inverse.MapPoint(
                (float)(stagePoint.X - Explode.StackCentreX),
                (float)(stagePoint.Y - Explode.StackCentreY));

            if (_footprints[layer].Contains(centred.X + halfWidth, centred.Y + halfHeight))
            {
                return layer;
            }
        }

        return -1;
    }

    /// <summary>
    /// A WinUI <see cref="Microsoft.UI.Xaml.Media.Matrix"/> maps
    /// (x,y) to (x.M11 + y.M21 + OffsetX, x.M12 + y.M22 + OffsetY), which is
    /// Skia's row order with the skews swapped.
    /// </summary>
    private static SKMatrix ToSkia(Microsoft.UI.Xaml.Media.Matrix m)
        => new(
            scaleX: (float)m.M11, skewX: (float)m.M21, transX: (float)m.OffsetX,
            skewY: (float)m.M12, scaleY: (float)m.M22, transY: (float)m.OffsetY,
            persp0: 0f, persp1: 0f, persp2: 1f);

    // ── sheet recording ───────────────────────────────────────────────────
    // The cull rect is a little larger than the case because the USB port
    // straddles the back edge and every stroke is centred on its path.

    private static readonly SKRect SheetBounds = new(-8f, -8f, (float)KeyLayout.CaseWidth + 8f, (float)KeyLayout.CaseHeight + 8f);

    private static SKPicture RecordCase(PlatePalette palette)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SheetBounds);

        using var shell = PlatePaths.CaseShell();
        using var well = PlatePaths.CaseWell();
        using var port = PlatePaths.CasePort();

        Fill(canvas, shell, palette.Aluminium);
        Stroke(canvas, shell, palette.Ink, 1.1f);
        Stroke(canvas, well, palette.InkFaint, 0.8f);
        Fill(canvas, port, palette.Sweep);
        Stroke(canvas, port, palette.Ink, 1f);

        return recorder.EndRecording();
    }

    private static SKPicture RecordBoard(PlatePalette palette)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SheetBounds);

        using var shell = PlatePaths.Board();
        using var traces = PlatePaths.BoardTraces();
        using var sockets = PlatePaths.BoardSockets();

        Fill(canvas, shell, palette.Board);
        Stroke(canvas, shell, palette.Ink, 1f);

        // opacity is baked into the paint: an element's Opacity does not reach
        // drawing done inside a canvas element
        Fill(canvas, traces, palette.Paper.WithAlpha(77));    // 0.3
        Fill(canvas, sockets, palette.Paper.WithAlpha(128));  // 0.5

        return recorder.EndRecording();
    }

    private static SKPicture RecordPlate(PlatePalette palette)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SheetBounds);

        using var slab = PlatePaths.Plate();

        Fill(canvas, slab, palette.Steel);
        Stroke(canvas, slab, palette.Ink, 1f);

        return recorder.EndRecording();
    }

    private static SKPicture RecordSwitches(PlatePalette palette)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SheetBounds);

        using var housings = PlatePaths.SwitchHousings();
        using var stems = PlatePaths.SwitchStems();

        Fill(canvas, housings, palette.Housing);
        Stroke(canvas, housings, palette.Ink, 0.7f);
        Fill(canvas, stems, palette.Steel);

        return recorder.EndRecording();
    }

    private static SKPicture RecordKeycaps(PlatePalette palette)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SheetBounds);

        using var shells = PlatePaths.Keycaps();
        using var dishes = PlatePaths.KeycapDishes();

        Fill(canvas, shells, palette.Keycap);
        Stroke(canvas, shells, palette.Ink, 1f);
        Stroke(canvas, dishes, palette.InkFaint, 0.7f);

        return recorder.EndRecording();
    }

    private static void Fill(SKCanvas canvas, SKPath path, SKColor color)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    private static void Stroke(SKCanvas canvas, SKPath path, SKColor color, float width)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, Color = color, StrokeWidth = width, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _selection.Dispose();

        foreach (var sheet in _sheets)
        {
            sheet?.Dispose();
        }

        foreach (var footprint in _footprints)
        {
            footprint?.Dispose();
        }
    }
}
