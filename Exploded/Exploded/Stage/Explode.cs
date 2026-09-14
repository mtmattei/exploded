using System;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>
/// The explode transform, ported from the StrataApp composition study and
/// retuned for five physical parts rather than three flat sheets.
///
/// Uno implements neither UIElement.Transform3D nor a nestable PlaneProjection,
/// so the deck is tipped back by RotationX, spun by RotationZ, and each sheet is
/// lifted along the deck's normal here, then handed to a plain MatrixTransform.
///
/// A point (x, y, gap) on sheet n maps through Rx(tilt) . Rz(spin) to
///   X = x.cos s - y.sin s
///   Y = (x.sin s + y.cos s).cos t - gap.sin t
/// which is affine in x and y, so the whole thing collapses into one 2x3 matrix
/// per sheet. The sheet's own depth survives as the OffsetY term and as a slight
/// scale-up for the sheets nearest the viewer.
/// </summary>
internal static class Explode
{
    /// <summary>
    /// Tip-back at full separation. Shallower than Strata's 56 degrees: a
    /// keyboard is wide and shallow, and a steep tilt crushes it into a bar.
    /// </summary>
    private const double TiltDegrees = 52d;

    /// <summary>
    /// In-plane spin at full separation. Much smaller than Strata's 34 degrees,
    /// because spinning a 15u-wide object that far sweeps a bounding box half
    /// again as large and reads as chaos rather than as a drawing.
    /// </summary>
    private const double SpinDegrees = -14d;

    /// <summary>Gap between neighbouring sheets along the deck normal.</summary>
    private const double SheetGap = 44d;

    /// <summary>Viewer distance for the perspective foreshortening.</summary>
    private const double Perspective = 1600d;

    /// <summary>Stage design size. Every measurement is written at its real number; the Viewbox scales the whole surface.</summary>
    public const double StageWidth = 960d;
    public const double StageHeight = 440d;

    /// <summary>
    /// Where the assembled stack sits. Pushed below centre because the explode
    /// only ever grows upward, so the headroom has to be reserved for it.
    /// </summary>
    public const double StackCentreX = StageWidth / 2d;
    public const double StackCentreY = 275d;

    /// <summary>The callout ladder: five bubbles on a fixed rail down the right of the plate.</summary>
    public const double BubbleX = 838d;
    public const double BubbleRadius = 12d;
    private const double BubbleSpacing = 42d;
    private const double BubbleTop = 136d;

    public const int LayerCount = 5;

    /// <summary>Separation as 0..1.</summary>
    private static double T(double separation) => Math.Clamp(separation / 100d, 0d, 1d);

    /// <summary>Layer 0 is the case at the bottom of the stack; layer 4 is the keycaps on top.</summary>
    public static Matrix SheetMatrix(double separation, int layerIndex)
    {
        var t = T(separation);
        var tilt = TiltDegrees * t * Math.PI / 180d;
        var spin = SpinDegrees * t * Math.PI / 180d;
        var gap = SheetGap * layerIndex * t;

        var cosT = Math.Cos(tilt);
        var sinT = Math.Sin(tilt);
        var cosS = Math.Cos(spin);
        var sinS = Math.Sin(spin);

        // the deck shrinks a little as it tips away; each sheet then gains back
        // the perspective scale it earns by sitting closer to the viewer
        var scale = (1d - t * 0.06d) * (Perspective / (Perspective - gap * cosT));

        return new Matrix(
            m11: scale * cosS,
            m12: scale * sinS * cosT,
            m21: scale * -sinS,
            m22: scale * cosS * cosT,
            offsetX: 0d,
            offsetY: -gap * sinT);
    }

    /// <summary>Callout number, read top down the way a parts list is numbered: keycaps are part 1.</summary>
    public static int CalloutNumber(int layerIndex) => LayerCount - layerIndex;

    /// <summary>Where callout <paramref name="layerIndex"/> sits on the fixed ladder.</summary>
    public static Point BubbleCentre(int layerIndex)
        => new(BubbleX, BubbleTop + (CalloutNumber(layerIndex) - 1) * BubbleSpacing);

    /// <summary>
    /// The leader line from a sheet's right edge out to its callout bubble.
    ///
    /// The bubbles sit on a fixed ladder rather than tracking their sheet, which
    /// is both the drafting convention and the only way they avoid colliding
    /// with each other in the middle of the scrub, where sheets are only a few
    /// pixels apart. The last segment lands horizontally into the bubble.
    /// </summary>
    public static Geometry LeaderLine(double separation, int layerIndex)
    {
        var matrix = SheetMatrix(separation, layerIndex);
        var halfWidth = KeyLayout.CaseWidth / 2d;

        var anchor = new Point(
            StackCentreX + halfWidth * matrix.M11,
            StackCentreY + halfWidth * matrix.M12 + matrix.OffsetY);

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
