using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Exploded.Stage;

/// <summary>
/// The five layers of the build as solids in one isometric camera, and the
/// one number that moves them: each layer rises straight up by its index
/// times the gap.
///
/// Rising straight up is what keeps the stage cheap. In an orthographic
/// camera a lift in z is a pure screen translation, so every layer keeps one
/// fixed drawing and only moves; nothing is re-projected per frame.
///
/// World units follow Hairline's keyboard: 14 to a key. Everything here is
/// computed once and never written again, so the render thread and the UI
/// thread read it freely.
/// </summary>
internal static class Stack
{
    public const double U = 14d;
    public const double FieldWidth = 15d * U;
    public const double FieldDepth = 5d * U;
    public const double Bezel = 5d;

    /// <summary>How far each layer rises above the one below it, fully exploded.</summary>
    public const double Gap = 20d;

    /// <summary>The camera: the 2:1 elevation, fitted to the fully exploded stack inside the drawing area left of the callout ladder.</summary>
    public static readonly IsoCamera Camera = CreateCamera();

    /// <summary>Each layer's assembled base and top, bottom to top: case, PCB, plate, switches, keycaps.</summary>
    public static readonly (double Base, double Top)[] Heights =
    {
        (-7d, 0d),
        (0d, 1.6d),
        (3.6d, 5.1d),
        (5.1d, 10.1d),
        (9d, 19d)
    };

    /// <summary>Each layer's footprint on the ground: the slabs carry a margin, switches and caps sit on the key field.</summary>
    public static readonly (double X0, double Y0, double X1, double Y1)[] Footprints =
    {
        (-Bezel, -Bezel, FieldWidth + Bezel, FieldDepth + Bezel),
        (-2d, -2d, FieldWidth + 2d, FieldDepth + 2d),
        (-1d, -1d, FieldWidth + 1d, FieldDepth + 1d),
        (0d, 0d, FieldWidth, FieldDepth),
        (0d, 0d, FieldWidth, FieldDepth)
    };

    /// <summary>Every key's footprint in world units, back row first and left to right: the order they paint in.</summary>
    public static readonly (double X0, double Y0, double X1, double Y1, int Row)[] Keys = KeyLayout.Keys()
        .Select(k => (
            X0: (k.X - KeyLayout.Bezel) / KeyLayout.Unit * U,
            Y0: (k.Y - KeyLayout.Bezel) / KeyLayout.Unit * U,
            X1: (k.X + k.Width - KeyLayout.Bezel) / KeyLayout.Unit * U,
            Y1: (k.Y + k.Height - KeyLayout.Bezel) / KeyLayout.Unit * U,
            Row: (int)Math.Round((k.Y - KeyLayout.Bezel) / KeyLayout.Unit)))
        .ToArray();

    /// <summary>Screen offset of a layer at a separation, 0 to 100: straight up the page.</summary>
    public static float Lift(int layer, double separation)
        => (float)(-layer * Gap * Explode.T(separation) * Camera.ZStep);

    /// <summary>A layer's footprint as a rounded ring.</summary>
    public static Sample[] FootprintRing(int layer)
    {
        var (x0, y0, x1, y1) = Footprints[layer];
        return Iso.RRect(x0, y0, x1, y1, layer == 0 ? 7d : 3d);
    }

    /// <summary>The layer's footprint at its assembled top, on screen: what the pointer is tested against.</summary>
    public static readonly SKPath[] HitAreas = Enumerable.Range(0, Explode.LayerCount)
        .Select(layer =>
        {
            var path = new SKPath();
            Iso.AddPoly(path, Iso.RingAt(Camera, FootprintRing(layer), Heights[layer].Top));
            return path;
        })
        .ToArray();

    /// <summary>
    /// Where each layer's leader line leaves it: the rightmost point of its
    /// footprint at its assembled top. Add <see cref="Lift"/> for the pose.
    /// </summary>
    public static readonly SKPoint[] Anchors = Enumerable.Range(0, Explode.LayerCount)
        .Select(layer =>
        {
            var right = Iso.Extremes(Camera, FootprintRing(layer))[1];
            return Camera.Project(right.U, right.V, Heights[layer].Top);
        })
        .ToArray();

    /// <summary>
    /// The dashed drops from each layer down to the one below, as assembled
    /// screen points: [from, to] for the left, right and nearest extremes.
    /// Layer 0 has none.
    /// </summary>
    public static readonly (SKPoint From, SKPoint To)[][] Drops = Enumerable.Range(0, Explode.LayerCount)
        .Select(layer => layer == 0
            ? Array.Empty<(SKPoint, SKPoint)>()
            : Iso.Extremes(Camera, FootprintRing(layer))
                .Select(q => (Camera.Project(q.U, q.V, Heights[layer].Base), Camera.Project(q.U, q.V, Heights[layer - 1].Top)))
                .ToArray())
        .ToArray();

    private static IsoCamera CreateCamera()
    {
        // Hairline rests at 45 degrees in a 5:4 frame. This stage is a 2.2:1
        // strip, so the board is turned to 30 degrees: its long side runs
        // flatter and the drawing grows into the width instead of the height.
        var camera = new IsoCamera(30d, 0.5d);
        var top = 4 * Gap + 19d;
        var x1 = FieldWidth + Bezel;
        var y1 = FieldDepth + Bezel;

        var extremePose = new List<(double, double, double)>
        {
            (-Bezel, -Bezel, -7d), (x1, y1, -7d), (x1, -Bezel, -7d), (-Bezel, y1, -7d),
            (-Bezel, -Bezel, top), (x1, -Bezel, top), (-Bezel, y1, top)
        };

        // centred left of the callout ladder, which starts near x 810
        camera.Fit(extremePose, cx: 400d, cy: 224d, width: 740d, height: 412d);
        return camera;
    }
}
