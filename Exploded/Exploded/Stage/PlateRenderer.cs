using System;
using SkiaSharp;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>The stage palette, resolved from the token dictionary on the UI thread.</summary>
internal readonly record struct PlatePalette(
    SKColor Ink,
    SKColor InkDim,
    SKColor InkFaint,
    SKColor Sweep,
    SKColor Callout);

/// <summary>
/// Draws the exploded build in the Hairline manner: every part a rounded
/// solid, drawn as a silhouette and one dim crease, filled with the ground
/// colour and painted back to front so a nearer part covers a farther one.
/// One stroke is the only highlight: the selected layer's silhouettes turn
/// callout orange, which is the colour the callout bubbles already speak.
///
/// Each layer is recorded once into an <see cref="SKPicture"/>, plain and
/// selected, at its assembled height. Layers only ever rise straight up, which
/// in this camera is a screen translation, so a separation change replays five
/// pictures under five offsets and draws the dashed drops between them.
/// <see cref="Render"/> runs on the render thread and allocates nothing.
/// </summary>
internal sealed class PlateRenderer : IDisposable
{
    private const float Stroke = 1.1f;

    private readonly SKPicture[] _plain = new SKPicture[Explode.LayerCount];
    private readonly SKPicture[] _selected = new SKPicture[Explode.LayerCount];
    private readonly SKPaint _dash;
    private readonly SKPaint _fill;
    private readonly SKPaint _sil;
    private readonly SKPaint _hi;
    private readonly SKPaint _lo;
    private bool _disposed;

    public PlateRenderer(PlatePalette palette)
    {
        _fill = new SKPaint { Style = SKPaintStyle.Fill, Color = palette.Sweep, IsAntialias = true };
        _sil = StrokePaint(palette.InkDim);
        _hi = StrokePaint(palette.Callout);
        _lo = StrokePaint(palette.Ink.WithAlpha(46));
        _dash = StrokePaint(palette.InkFaint);
        _dash.PathEffect = SKPathEffect.CreateDash([1.2f, 3.4f], 0f);

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            _plain[layer] = Record(layer, selected: false);
            _selected[layer] = Record(layer, selected: true);
        }
    }

    /// <summary>Bottom layer first: each one up covers what it rises over.</summary>
    public void Render(SKCanvas canvas, double separation, int selectedLayer)
    {
        if (_disposed)
        {
            return;
        }

        var open = Explode.T(separation) > 0.01d;

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var lift = Stack.Lift(layer, separation);

            // a drop goes in before the layer it leads to, so that layer hides it
            if (open)
            {
                var below = Stack.Lift(layer - 1 < 0 ? 0 : layer - 1, separation);

                foreach (var (from, to) in Stack.Drops[layer])
                {
                    canvas.DrawLine(from.X, from.Y + lift, to.X, to.Y + below, _dash);
                }
            }

            canvas.Save();
            canvas.Translate(0f, lift);
            canvas.DrawPicture(layer == selectedLayer ? _selected[layer] : _plain[layer]);
            canvas.Restore();
        }
    }

    /// <summary>
    /// The topmost layer whose footprint, at the pose the slider sets, holds the
    /// point; or -1. The test is against footprints, not drawn strokes, so a
    /// layer cannot slip out from under a held pointer.
    /// </summary>
    public int HitTest(Point stagePoint, double separation)
    {
        for (var layer = Explode.LayerCount - 1; layer >= 0; layer--)
        {
            if (Stack.HitAreas[layer].Contains((float)stagePoint.X, (float)(stagePoint.Y - Stack.Lift(layer, separation))))
            {
                return layer;
            }
        }

        return -1;
    }

    // ── recording ─────────────────────────────────────────────────────────

    private SKPicture Record(int layer, bool selected)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0f, 0f, (float)Explode.StageWidth, (float)Explode.StageHeight));
        var edge = selected ? _hi : _sil;

        switch (layer)
        {
            case 0: DrawCase(canvas, edge); break;
            case 1: DrawBoard(canvas, edge); break;
            case 2: DrawPlate(canvas, edge); break;
            case 3: DrawSwitches(canvas, selected ? _hi : _lo); break;
            default: DrawKeycaps(canvas, edge); break;
        }

        return recorder.EndRecording();
    }

    /// <summary>A tray: its body, the inner edge of its rim, and the floor seam along the far walls.</summary>
    private void DrawCase(SKCanvas canvas, SKPaint edge)
    {
        var c = Stack.Camera;
        var (z0, z1) = Stack.Heights[0];
        var ring = Stack.FootprintRing(0);
        var (x0, y0, x1, y1) = Stack.Footprints[0];
        var rim = Iso.RRect(x0 + 3d, y0 + 3d, x1 - 3d, y1 - 3d, 4d);

        Solid(canvas, Iso.Silhouette(c, ring, z0, ring, z1), [], edge);
        Line(canvas, Iso.RingAt(c, rim, z1), _lo, closed: true);
        Line(canvas, Iso.RingAt(c, Iso.Run(rim, q => !c.Facing(q)), z0 + 2d), _lo, closed: false);
    }

    /// <summary>The PCB: a thin slab with a hot-swap socket under every key and the USB-C port at the back edge.</summary>
    private void DrawBoard(SKCanvas canvas, SKPaint edge)
    {
        var c = Stack.Camera;
        var (z0, z1) = Stack.Heights[1];
        var (ring, inner) = SlabRings(1);

        Solid(canvas, Iso.Silhouette(c, ring, z0, ring, z1), Iso.Crease(c, inner, z1), edge);

        using var sockets = new SKPath();
        foreach (var k in Stack.Keys)
        {
            Iso.AddPoly(sockets, Iso.RingAt(c, Iso.Circle((k.X0 + k.X1) / 2d, (k.Y0 + k.Y1) / 2d + 3d, 1.6d, 10), z1));
        }
        canvas.DrawPath(sockets, _lo);

        var (port, portInner) = Iso.Rings(Stack.FieldWidth / 2d - 7d, -2d, Stack.FieldWidth / 2d + 7d, 4d, 2d, 0.6d);
        Solid(canvas, Iso.Silhouette(c, port, z1, port, z1 + 3d), Iso.Crease(c, portInner, z1 + 3d), edge);
    }

    /// <summary>The plate: a steel slab with a 14 mm cutout for every switch.</summary>
    private void DrawPlate(SKCanvas canvas, SKPaint edge)
    {
        var c = Stack.Camera;
        var (z0, z1) = Stack.Heights[2];
        var (ring, inner) = SlabRings(2);

        Solid(canvas, Iso.Silhouette(c, ring, z0, ring, z1), Iso.Crease(c, inner, z1), edge);

        using var cutouts = new SKPath();
        foreach (var k in Stack.Keys)
        {
            Iso.AddPoly(cutouts, Iso.RingAt(c, Square(k, 5.2d, 0.8d), z1));
        }
        canvas.DrawPath(cutouts, _lo);
    }

    /// <summary>The switches: a housing narrower at its top, and the cross of its stem. Back to front, so a nearer one covers.</summary>
    private void DrawSwitches(SKCanvas canvas, SKPaint edge)
    {
        var c = Stack.Camera;
        var (z0, z1) = Stack.Heights[3];

        foreach (var k in Stack.Keys)
        {
            Solid(canvas, Iso.Silhouette(c, Square(k, 5.2d, 1d), z0, Square(k, 4d, 1.2d), z1), [], edge);

            var (cx, cy) = ((k.X0 + k.X1) / 2d, (k.Y0 + k.Y1) / 2d);
            canvas.DrawLine(c.Project(cx - 1.6d, cy, z1), c.Project(cx + 1.6d, cy, z1), _lo);
            canvas.DrawLine(c.Project(cx, cy - 1.6d, z1), c.Project(cx, cy + 1.6d, z1), _lo);
        }
    }

    /// <summary>
    /// The keycaps: each narrower at its top than at its foot, each row at its
    /// own height (the sculpted profile, lowest on the home row), and the
    /// homing bars on F and J.
    /// </summary>
    private void DrawKeycaps(SKCanvas canvas, SKPaint edge)
    {
        ReadOnlySpan<double> rowHeights = [10d, 9d, 8.2d, 8.7d, 9.4d];
        const double pad = 0.8d, taper = 1.6d;

        var c = Stack.Camera;
        var z0 = Stack.Heights[4].Base;
        var inRow = 0;
        var lastRow = -1;

        foreach (var k in Stack.Keys)
        {
            inRow = k.Row == lastRow ? inRow + 1 : 0;
            lastRow = k.Row;

            var h = z0 + rowHeights[k.Row];
            var t = pad + taper;
            var foot = Iso.RRect(k.X0 + pad, k.Y0 + pad, k.X1 - pad, k.Y1 - pad, 2.4d);
            var top = Iso.RRect(k.X0 + t, k.Y0 + t, k.X1 - t, k.Y1 - t, 1.8d);
            var inner = Iso.RRect(k.X0 + t + 0.9d, k.Y0 + t + 0.9d, k.X1 - t - 0.9d, k.Y1 - t - 0.9d, 1.1d);

            Solid(canvas, Iso.Silhouette(c, foot, z0, top, h), Iso.Crease(c, inner, h), edge);

            // F and J are the fourth and seventh keys of the home row
            if (k.Row == 2 && (inRow == 4 || inRow == 7))
            {
                var cx = (k.X0 + k.X1) / 2d;
                var cy = k.Y1 - t - 2.4d;
                canvas.DrawLine(c.Project(cx - 2.2d, cy, h), c.Project(cx + 2.2d, cy, h), _lo);
            }
        }
    }

    // ── drawing helpers ───────────────────────────────────────────────────

    private static (Sample[] Ring, Sample[] Inner) SlabRings(int layer)
    {
        var (x0, y0, x1, y1) = Stack.Footprints[layer];
        return Iso.Rings(x0, y0, x1, y1, 3d, 1.2d);
    }

    private static Sample[] Square((double X0, double Y0, double X1, double Y1, int Row) key, double half, double radius)
    {
        var (cx, cy) = ((key.X0 + key.X1) / 2d, (key.Y0 + key.Y1) / 2d);
        return Iso.RRect(cx - half, cy - half, cx + half, cy + half, radius, 3);
    }

    /// <summary>A plate filled with the ground colour, its silhouette in the edge stroke, its crease dim.</summary>
    private void Solid(SKCanvas canvas, SKPoint[] silhouette, SKPoint[] crease, SKPaint edge)
    {
        using var sil = new SKPath();
        Iso.AddPoly(sil, silhouette);
        canvas.DrawPath(sil, _fill);
        canvas.DrawPath(sil, edge);

        Line(canvas, crease, _lo, closed: false);
    }

    private static void Line(SKCanvas canvas, SKPoint[] points, SKPaint paint, bool closed)
    {
        using var path = new SKPath();

        if (closed)
        {
            Iso.AddPoly(path, points);
        }
        else
        {
            Iso.AddOpen(path, points);
        }

        canvas.DrawPath(path, paint);
    }

    private static SKPaint StrokePaint(SKColor color) => new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = Stroke,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeCap = SKStrokeCap.Round,
        Color = color,
        IsAntialias = true
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var picture in _plain) picture?.Dispose();
        foreach (var picture in _selected) picture?.Dispose();

        _fill.Dispose();
        _sil.Dispose();
        _hi.Dispose();
        _lo.Dispose();
        _dash.PathEffect?.Dispose();
        _dash.Dispose();
    }
}
