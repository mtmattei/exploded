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
/// Draws the exploded plate as five layered isometric solids.
///
/// Every solid is built the way Hairline builds one (<see cref="Iso"/>): the
/// hull of two rounded rings, its foot and its top, so no vertical corner is
/// ever drawn, with one dim crease inside the top edge as its bevel. Plates are
/// opaque and painted back to front, so a nearer part covers a farther one
/// without any hidden-line work.
///
/// Each layer's art is recorded once into an <see cref="SKPicture"/> in stage
/// coordinates at its assembled height and replayed shifted up the screen by
/// its lift, so a separation change costs five translations and five picture
/// replays rather than rebuilding any geometry. Paints are built once and
/// reused: <see cref="Render"/> runs on the render thread and must not
/// allocate.
/// </summary>
internal sealed class PlateRenderer : IDisposable
{
    private readonly SKPicture[] _layers = new SKPicture[Explode.LayerCount];
    private readonly SKPath[] _outlines = new SKPath[Explode.LayerCount];
    private readonly SKPath[] _footprints = new SKPath[Explode.LayerCount];
    private readonly SKPaint _selection;
    private bool _disposed;

    /// <summary>Each key row's cap height, back to front: the sculpted profile, lowest on the home row.</summary>
    private static readonly double[] CapHeight = { 10.6, 9.6, 8.9, 9.4, 10.1 };

    public PlateRenderer(PlatePalette palette)
    {
        _selection = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.2f,
            Color = palette.Callout,
            IsAntialias = true
        };

        var camera = Explode.Camera;
        var project = Iso.Proj(camera);
        var front = Iso.Facing(camera);

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var shape = Explode.Layers[layer];
            // selection traces the layer's own top edge; hit tests use its footprint on the ground
            _outlines[layer] = IsoPaths.Poly(Iso.RingAt(project, shape.Ring, shape.Z1));
            _footprints[layer] = PlatePaths.Footprint(shape);
        }

        _layers[0] = RecordCase(palette, project, front);
        _layers[1] = RecordBoard(palette, project, front);
        _layers[2] = RecordPlate(palette, project, front);
        _layers[3] = RecordSwitches(palette, project, front);
        _layers[4] = RecordKeycaps(palette, project, front);
    }

    /// <summary>
    /// Layer 0 is the case at the bottom of the stack; layer 4 is the keycaps
    /// on top, so the layers draw in index order and a higher one covers.
    /// </summary>
    public void Render(SKCanvas canvas, double separation, int selectedLayer)
    {
        if (_disposed)
        {
            return;
        }

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            canvas.Save();
            canvas.Translate(0f, (float)Explode.LiftOffset(separation, layer));
            canvas.DrawPicture(_layers[layer]);

            if (layer == selectedLayer)
            {
                canvas.DrawPath(_outlines[layer], _selection);
            }

            canvas.Restore();
        }
    }

    /// <summary>
    /// Which layer a point in stage coordinates lands on, topmost first, or -1
    /// for a miss. The point is run back through the camera onto each layer's
    /// top plane, at the height the layer is lifted to, and tested against the
    /// layer's footprint in world units, so the test never reads pixels.
    /// </summary>
    public int HitTest(Point stagePoint, double separation)
    {
        for (var layer = Explode.LayerCount - 1; layer >= 0; layer--)
        {
            var shape = Explode.Layers[layer];
            var ground = Iso.Unproj(
                Explode.Camera,
                stagePoint.X,
                stagePoint.Y - Explode.LiftOffset(separation, layer),
                shape.Z1);

            if (_footprints[layer].Contains((float)ground.X, (float)ground.Y))
            {
                return layer;
            }
        }

        return -1;
    }

    // ── layer recording ───────────────────────────────────────────────────

    private static readonly SKRect StageBounds = new(-20f, -40f, (float)Explode.StageWidth + 20f, (float)Explode.StageHeight + 20f);

    /// <summary>The case: a tray with a bevelled rim, and the USB port straddling its back edge.</summary>
    private static SKPicture RecordCase(PlatePalette palette, Projector project, Func<Sample, bool> front)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(StageBounds);
        var shape = Explode.Layers[0];

        var inner = Iso.Rrect(5, 5, KeyLayout.CaseWidth - 5, KeyLayout.CaseHeight - 5, 6);
        Solid(canvas, Iso.Prism(project, front, shape.Ring, inner, shape.Z0, shape.Z1), palette.Aluminium, palette.Ink, palette.InkFaint, 1.1f);

        // the port: a short block let into the back wall
        var width = 9d * KeyLayout.PixelsPerMm;
        var port = Iso.Rrect((KeyLayout.CaseWidth - width) / 2d, -2, (KeyLayout.CaseWidth + width) / 2d, 5, 1);
        Solid(canvas, Iso.Prism(project, front, port, null, shape.Z0 + 3, shape.Z1 - 2), palette.Sweep, palette.Ink, palette.InkFaint, 1f);

        return recorder.EndRecording();
    }

    /// <summary>The PCB: a thin green plate with bus traces and two sockets under each key.</summary>
    private static SKPicture RecordBoard(PlatePalette palette, Projector project, Func<Sample, bool> front)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(StageBounds);
        var shape = Explode.Layers[1];

        Solid(canvas, Iso.Prism(project, front, shape.Ring, null, shape.Z0, shape.Z1), palette.Board, palette.Ink, palette.InkFaint, 1f);

        using var traces = new SKPath();
        for (var row = 1; row < 5; row++)
        {
            var y = KeyLayout.Bezel + row * KeyLayout.Unit - 2d;
            IsoPaths.AddSeg(traces, project(14, y, shape.Z1), project(KeyLayout.CaseWidth - 14, y, shape.Z1));
        }

        // the controller, back-left of the board
        IsoPaths.AddPoly(traces, Iso.RingAt(project, Iso.Rrect(KeyLayout.CaseWidth / 2d - 16, 9, KeyLayout.CaseWidth / 2d + 16, 21, 1), shape.Z1));
        Stroke(canvas, traces, palette.Paper.WithAlpha(110), 0.8f);

        using var sockets = new SKPath();
        var socket = Iso.Circ(1.6, 12);
        foreach (var centre in KeyLayout.SwitchCentres())
        {
            IsoPaths.AddPoly(sockets, Ring(project, socket, centre.X - 3.4, centre.Y - 2.4, shape.Z1));
            IsoPaths.AddPoly(sockets, Ring(project, socket, centre.X + 2.5, centre.Y - 4.2, shape.Z1));
        }

        Fill(canvas, sockets, palette.Paper.WithAlpha(128));

        return recorder.EndRecording();
    }

    /// <summary>The switch plate: a steel slab with 61 square cutouts punched through its top.</summary>
    private static SKPicture RecordPlate(PlatePalette palette, Projector project, Func<Sample, bool> front)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(StageBounds);
        var shape = Explode.Layers[2];

        Solid(canvas, Iso.Prism(project, front, shape.Ring, null, shape.Z0, shape.Z1), palette.Steel, palette.Ink, palette.InkFaint, 1f);

        // a cutout shows the dark of the well beneath, and its edge is a hairline
        using var cutouts = new SKPath();
        var half = KeyLayout.SwitchSize / 2d;
        foreach (var centre in KeyLayout.SwitchCentres())
        {
            IsoPaths.AddPoly(cutouts, Iso.RingAt(project, Iso.Rrect(centre.X - half, centre.Y - half, centre.X + half, centre.Y + half, 0.8, 2), shape.Z1));
        }

        Fill(canvas, cutouts, palette.Sweep);
        Stroke(canvas, cutouts, palette.InkFaint, 0.6f);

        return recorder.EndRecording();
    }

    /// <summary>Switches: 61 housings, each a short tapered block with the cross stem on top.</summary>
    private static SKPicture RecordSwitches(PlatePalette palette, Projector project, Func<Sample, bool> front)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(StageBounds);
        var shape = Explode.Layers[3];
        var half = KeyLayout.SwitchSize / 2d;

        using var stems = new SKPath();
        foreach (var centre in KeyLayout.SwitchCentres())
        {
            // back to front, left to right: KeyLayout walks rows from the back, so a nearer housing covers
            var foot = Iso.Rrect(centre.X - half, centre.Y - half, centre.X + half, centre.Y + half, 1.4);
            var top = Iso.Rrect(centre.X - half + 1.2, centre.Y - half + 1.2, centre.X + half - 1.2, centre.Y + half - 1.2, 1.2);
            var inner = Iso.Rrect(centre.X - half + 2.2, centre.Y - half + 2.2, centre.X + half - 2.2, centre.Y + half - 2.2, 0.8);
            Solid(canvas, Iso.Taper(project, front, foot, top, inner, shape.Z0, shape.Z1), palette.Housing, palette.Ink, palette.InkFaint, 0.7f);

            IsoPaths.AddSeg(stems, project(centre.X, centre.Y - 4, shape.Z1), project(centre.X, centre.Y + 4, shape.Z1));
            IsoPaths.AddSeg(stems, project(centre.X - 4, centre.Y, shape.Z1), project(centre.X + 4, centre.Y, shape.Z1));
        }

        Stroke(canvas, stems, palette.Steel, 2.2f);

        return recorder.EndRecording();
    }

    /// <summary>Keycaps: 61 caps, each narrower at its top than at its foot, with the dish as the crease, on the sculpted row profile.</summary>
    private static SKPicture RecordKeycaps(PlatePalette palette, Projector project, Func<Sample, bool> front)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(StageBounds);
        var shape = Explode.Layers[4];

        var row = 0;
        var rowY = double.NaN;
        foreach (var key in KeyLayout.Keys())
        {
            if (key.Y != rowY)
            {
                row = (int)Math.Round((key.Y - KeyLayout.Bezel) / KeyLayout.Unit);
                rowY = key.Y;
            }

            const double pad = 1.5d, taper = 3d, dish = 1.4d;
            var foot = Iso.Rrect(key.X + pad, key.Y + pad, key.Right - pad, key.Bottom - pad, 3.5);
            var top = Iso.Rrect(key.X + pad + taper, key.Y + pad + taper, key.Right - pad - taper, key.Bottom - pad - taper, 2.5);
            var inner = Iso.Rrect(key.X + pad + taper + dish, key.Y + pad + taper + dish, key.Right - pad - taper - dish, key.Bottom - pad - taper - dish, 1.6);
            Solid(canvas, Iso.Taper(project, front, foot, top, inner, shape.Z0, shape.Z0 + CapHeight[row]), palette.Keycap, palette.Ink, palette.InkFaint, 1f);
        }

        return recorder.EndRecording();
    }

    /// <summary>A ring moved to a centre on the ground and projected at height z.</summary>
    private static Vec2[] Ring(Projector project, Sample[] ring, double cx, double cy, double z)
    {
        var pts = new Vec2[ring.Length];
        for (var i = 0; i < ring.Length; i++)
        {
            pts[i] = project(cx + ring[i].U, cy + ring[i].V, z);
        }

        return pts;
    }

    /// <summary>One solid: its silhouette filled and stroked, then its crease in the faint ink.</summary>
    private static void Solid(SKCanvas canvas, PrismOutline outline, SKColor fill, SKColor edge, SKColor crease, float width)
    {
        using var silhouette = IsoPaths.Poly(outline.Silhouette);
        Fill(canvas, silhouette, fill);
        Stroke(canvas, silhouette, edge, width);

        if (outline.Crease.Length >= 2)
        {
            using var bevel = IsoPaths.Open(outline.Crease);
            Stroke(canvas, bevel, crease, Math.Max(0.6f, width - 0.3f));
        }
    }

    private static void Fill(SKCanvas canvas, SKPath path, SKColor color)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    private static void Stroke(SKCanvas canvas, SKPath path, SKColor color, float width)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, Color = color, StrokeWidth = width, IsAntialias = true, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
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

        foreach (var layer in _layers)
        {
            layer?.Dispose();
        }

        foreach (var outline in _outlines)
        {
            outline?.Dispose();
        }

        foreach (var footprint in _footprints)
        {
            footprint?.Dispose();
        }
    }
}
