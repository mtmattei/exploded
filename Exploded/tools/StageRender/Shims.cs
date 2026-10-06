// Just enough of WinUI for the stage classes to compile outside Uno.
namespace Windows.Foundation
{
    public readonly record struct Point(double X, double Y);
    public readonly record struct Rect(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
        public double Bottom => Y + Height;
    }
}
namespace Microsoft.UI.Xaml.Media
{
    public class Geometry { }
    public class PathSegment { }
    public class LineSegment : PathSegment { public Windows.Foundation.Point Point { get; set; } }
    public class PathFigure { public Windows.Foundation.Point StartPoint { get; set; } public bool IsClosed { get; set; } public bool IsFilled { get; set; } public List<PathSegment> Segments { get; } = new(); }
    public class PathGeometry : Geometry { public List<PathFigure> Figures { get; } = new(); }
}
