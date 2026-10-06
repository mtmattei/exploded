using System;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>
/// The explode: five solids on one build axis, seen through one orthographic
/// camera. The camera is Hairline's (<see cref="Iso"/>): the ground turned by
/// an azimuth, y squashed to the 2:1 view, z lifted straight up the screen.
/// Because the projection is linear in z, lifting a layer by <c>gap</c> world
/// units is a plain screen translation of <c>-S · zf · gap</c>, so each layer's
/// art is recorded once at its assembled height and replayed shifted.
///
/// The earlier affine sheets (tilt, spin, perspective) are gone: parts now have
/// thickness, tapered caps and bevels, and the drawing stays honest from any
/// separation because plates are opaque and painted back to front.
/// </summary>
internal static class Explode
{
    /// <summary>Gap between neighbouring layers along the build axis at full separation, in world units.</summary>
    private const double Gap = 34d;

    /// <summary>Stage design size. Every measurement is written at its real number; the Viewbox scales the whole surface.</summary>
    public const double StageWidth = 960d;
    public const double StageHeight = 440d;

    /// <summary>
    /// Where the drawing is centred: the box of the fully exploded stack sits
    /// here, so the assembled stack rests lower and the explode grows into the
    /// headroom, left of the callout ladder.
    /// </summary>
    private const double CentreX = 430d;
    private const double CentreY = 222d;

    /// <summary>The callout ladder: five bubbles on a fixed rail down the right of the plate.</summary>
    public const double BubbleX = 838d;
    public const double BubbleRadius = 12d;
    private const double BubbleSpacing = 42d;
    private const double BubbleTop = 136d;

    public const int LayerCount = 5;

    /// <summary>
    /// Each layer's footprint on the ground and the heights it stands between
    /// when assembled. Layer 0 is the case at the bottom; layer 4 the keycaps.
    /// Depths are world units, the same units as the key field.
    /// </summary>
    public static readonly LayerShape[] Layers =
    {
        new(Iso.Rrect(0, 0, KeyLayout.CaseWidth, KeyLayout.CaseHeight, 9), -11d, 0d),
        new(Iso.Rrect(6, 6, KeyLayout.CaseWidth - 6, KeyLayout.CaseHeight - 6, 4), 0d, 1.6d),
        new(Iso.Rrect(4, 4, KeyLayout.CaseWidth - 4, KeyLayout.CaseHeight - 4, 5), 1.6d, 3.2d),
        new(Iso.Rrect(KeyLayout.Bezel, KeyLayout.Bezel, KeyLayout.Bezel + KeyLayout.FieldWidth, KeyLayout.Bezel + KeyLayout.FieldHeight, 4), 3.2d, 15d),
        new(Iso.Rrect(KeyLayout.Bezel, KeyLayout.Bezel, KeyLayout.Bezel + KeyLayout.FieldWidth, KeyLayout.Bezel + KeyLayout.FieldHeight, 4), 15d, 26d),
    };

    /// <summary>
    /// The one camera. A 35° azimuth rather than Hairline's 45°: a 60% board is
    /// nearly three times wider than deep, and at 45° it spends the stage's width
    /// on its depth. Fitted once to the fully exploded box.
    /// </summary>
    public static readonly Camera Camera = MakeCamera();

    private static Camera MakeCamera()
    {
        var camera = Iso.Cam(35, 0.5, 0.98);
        var top = Layers[LayerCount - 1].Z1 + Gap * (LayerCount - 1);
        var bottom = Layers[0].Z0;
        Iso.Fit(camera, new[]
        {
            new Vec3(0, 0, bottom),
            new Vec3(KeyLayout.CaseWidth, KeyLayout.CaseHeight, bottom),
            new Vec3(KeyLayout.CaseWidth, 0, bottom),
            new Vec3(0, KeyLayout.CaseHeight, bottom),
            new Vec3(0, 0, top),
            new Vec3(KeyLayout.CaseWidth, 0, top),
            new Vec3(0, KeyLayout.CaseHeight, top),
        }, CentreX, CentreY);
        return camera;
    }

    /// <summary>Separation as 0..1.</summary>
    private static double T(double separation) => Math.Clamp(separation / 100d, 0d, 1d);

    /// <summary>How far layer <paramref name="layerIndex"/> is lifted along the build axis, in world units.</summary>
    public static double Lift(double separation, int layerIndex) => Gap * layerIndex * T(separation);

    /// <summary>
    /// The screen translation that lifts a layer's recorded art: the projection
    /// is orthographic, so a rise of <c>gap</c> world units is a shift straight up
    /// the screen by <c>S · zf · gap</c>, and nothing else about the drawing changes.
    /// </summary>
    public static double LiftOffset(double separation, int layerIndex)
        => -Camera.S * Camera.Zf * Lift(separation, layerIndex);

    /// <summary>Callout number, read top down the way a parts list is numbered: keycaps are part 1.</summary>
    public static int CalloutNumber(int layerIndex) => LayerCount - layerIndex;

    /// <summary>Where callout <paramref name="layerIndex"/> sits on the fixed ladder.</summary>
    public static Point BubbleCentre(int layerIndex)
        => new(BubbleX, BubbleTop + (CalloutNumber(layerIndex) - 1) * BubbleSpacing);

    /// <summary>
    /// The leader line from a layer's rightmost edge, at its top, out to its
    /// callout bubble. The bubbles sit on a fixed ladder rather than tracking
    /// their layer, which is both the drafting convention and the only way they
    /// avoid colliding with each other in the middle of the scrub. The last
    /// segment lands horizontally into the bubble.
    /// </summary>
    public static Geometry LeaderLine(double separation, int layerIndex)
    {
        var layer = Layers[layerIndex];
        var project = Iso.Proj(Camera);
        var (_, right, _) = Iso.Extremes(project, layer.Ring);
        var edge = project(right.U, right.V, layer.Z1);
        var anchor = new Point(edge.X, edge.Y + LiftOffset(separation, layerIndex));

        var bubble = BubbleCentre(layerIndex);
        var landing = new Point(bubble.X - BubbleRadius - 18d, bubble.Y);
        var tip = new Point(bubble.X - BubbleRadius - 2d, bubble.Y);

        var figure = new PathFigure { StartPoint = anchor, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new LineSegment { Point = landing });
        figure.Segments.Add(new LineSegment { Point = tip });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// Callouts are annotation, not part of the product. They hold off until the
    /// stack is a third open so the assembled view stays a clean product shot.
    /// </summary>
    public static double CalloutFade(double separation)
        => Math.Clamp(T(separation) * 1.6d - 0.48d, 0d, 1d);

    public static string StageHint(double separation) => separation switch
    {
        <= 0 => "ASSEMBLED",
        > 85 => "FULLY EXPLODED",
        _ => "SEPARATING"
    };
}

/// <summary>A layer's footprint ring on the ground and the heights it stands between when assembled.</summary>
internal sealed record LayerShape(Sample[] Ring, double Z0, double Z1);
