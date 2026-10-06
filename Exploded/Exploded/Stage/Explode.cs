using System;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>
/// The stage's fixed measurements and the callout system. Where the layers
/// sit and how they rise is <see cref="Stack"/>; this class owns the
/// separation value's meaning, the callout ladder and the leader lines.
/// </summary>
internal static class Explode
{
    /// <summary>Stage design size. Every measurement is written at its real number; the Viewbox scales the whole surface.</summary>
    public const double StageWidth = 960d;
    public const double StageHeight = 440d;

    /// <summary>The callout ladder: five bubbles on a fixed rail down the right of the plate.</summary>
    public const double BubbleX = 838d;
    public const double BubbleRadius = 12d;
    private const double BubbleSpacing = 42d;
    private const double BubbleTop = 136d;

    public const int LayerCount = 5;

    /// <summary>Separation as 0..1.</summary>
    public static double T(double separation) => Math.Clamp(separation / 100d, 0d, 1d);

    /// <summary>Callout number, read top down the way a parts list is numbered: keycaps are part 1.</summary>
    public static int CalloutNumber(int layerIndex) => LayerCount - layerIndex;

    /// <summary>Where callout <paramref name="layerIndex"/> sits on the fixed ladder.</summary>
    public static Point BubbleCentre(int layerIndex)
        => new(BubbleX, BubbleTop + (CalloutNumber(layerIndex) - 1) * BubbleSpacing);

    /// <summary>
    /// The leader line from a layer's right corner out to its callout bubble.
    ///
    /// The bubbles sit on a fixed ladder rather than tracking their sheet, which
    /// is both the drafting convention and the only way they avoid colliding
    /// with each other in the middle of the scrub, where layers are only a few
    /// pixels apart. The last segment lands horizontally into the bubble.
    /// </summary>
    public static Geometry LeaderLine(double separation, int layerIndex)
    {
        var corner = Stack.Anchors[layerIndex];
        var anchor = new Point(corner.X, corner.Y + Stack.Lift(layerIndex, separation));

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
