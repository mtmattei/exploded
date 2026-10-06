using SkiaSharp;

namespace Exploded.Stage;

/// <summary>
/// Each layer's footprint as a flat Skia path in world coordinates, where (0,0)
/// is the top-left of the case, for hit testing: a pointer is run back through
/// the camera onto the layer's top plane (<see cref="Iso.Unproj"/>) and tested
/// here, in the layer's own units, never in pixels. The drawing itself is built
/// from rings in <see cref="PlateRenderer"/>.
/// </summary>
internal static class PlatePaths
{
    public static SKPath Footprint(LayerShape layer)
    {
        var path = new SKPath();
        var first = true;
        foreach (var sample in layer.Ring)
        {
            if (first)
            {
                path.MoveTo((float)sample.U, (float)sample.V);
                first = false;
            }
            else
            {
                path.LineTo((float)sample.U, (float)sample.V);
            }
        }

        path.Close();
        return path;
    }
}
