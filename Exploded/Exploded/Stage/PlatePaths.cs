using SkiaSharp;

namespace Exploded.Stage;

/// <summary>
/// Every layer of the plate as Skia paths, in layer-local coordinates where
/// (0,0) is the top-left of the case.
///
/// Two decisions are baked in here. Every layer is a plan view, because the
/// explode transform is affine per sheet and cannot model real part thickness,
/// which is exactly what an exploded plate in a service manual shows anyway.
/// And each layer collapses to a handful of <see cref="SKPath"/> objects rather
/// than a few hundred elements.
///
/// These are Skia paths rather than XAML <c>Geometry</c> on purpose: a XAML
/// Path is a composition shape visual, and Uno recomputes an anti-aliasing
/// damage path for every shape visual whose transform changes. With five sheets
/// moving on every slider tick that cost one blocking second per frame. Skia
/// paths inside one canvas element are drawing, not visuals, so they cost
/// nothing to move.
/// </summary>
internal static class PlatePaths
{
    /// <summary>The case tray, with its rounded outline.</summary>
    public static SKPath CaseShell()
        => RoundedRect(0, 0, (float)KeyLayout.CaseWidth, (float)KeyLayout.CaseHeight, 6f);

    /// <summary>Inner tray wall, drawn as an outline only.</summary>
    public static SKPath CaseWell()
        => RoundedRect(5, 5, (float)KeyLayout.CaseWidth - 10, (float)KeyLayout.CaseHeight - 10, 4f);

    /// <summary>USB port, straddling the back edge of the tray.</summary>
    public static SKPath CasePort()
    {
        var width = (float)(9d * KeyLayout.PixelsPerMm);
        var path = new SKPath();
        path.AddRect(SKRect.Create(((float)KeyLayout.CaseWidth - width) / 2f, -2f, width, 7f));
        return path;
    }

    /// <summary>The PCB outline, sitting just inside the tray.</summary>
    public static SKPath Board()
        => RoundedRect(6, 6, (float)KeyLayout.CaseWidth - 12, (float)KeyLayout.CaseHeight - 12, 3f);

    /// <summary>Switch sockets, two per key, as the board's surface texture.</summary>
    public static SKPath BoardSockets()
    {
        var path = new SKPath();

        foreach (var centre in KeyLayout.SwitchCentres())
        {
            path.AddCircle((float)centre.X - 3.4f, (float)centre.Y - 2.4f, 1.6f);
            path.AddCircle((float)centre.X + 2.5f, (float)centre.Y - 4.2f, 1.6f);
        }

        return path;
    }

    /// <summary>A few traces and the controller footprint, so the board reads as a board.</summary>
    public static SKPath BoardTraces()
    {
        var path = new SKPath();

        // controller, back-left of the board
        path.AddRect(SKRect.Create((float)KeyLayout.CaseWidth / 2f - 16f, 9f, 32f, 12f));

        // bus lines running the length of the board between key rows
        for (var row = 1; row < 5; row++)
        {
            var y = (float)(KeyLayout.Bezel + row * KeyLayout.Unit - 2d);
            path.AddRect(SKRect.Create(14f, y, (float)KeyLayout.CaseWidth - 28f, 0.8f));
        }

        return path;
    }

    /// <summary>
    /// The switch plate: one slab with 61 square cutouts punched through it.
    /// EvenOdd is what makes the cutouts real holes rather than filled squares,
    /// so the board below shows through them as the stack separates.
    /// </summary>
    public static SKPath Plate()
    {
        var path = RoundedRect(4, 4, (float)KeyLayout.CaseWidth - 8, (float)KeyLayout.CaseHeight - 8, 4f);
        path.FillType = SKPathFillType.EvenOdd;

        var size = (float)KeyLayout.SwitchSize;
        foreach (var centre in KeyLayout.SwitchCentres())
        {
            path.AddRect(SKRect.Create((float)centre.X - size / 2f, (float)centre.Y - size / 2f, size, size));
        }

        return path;
    }

    /// <summary>The plate slab without its cutouts, for drawing a selection outline.</summary>
    public static SKPath PlateOutline()
        => RoundedRect(4, 4, (float)KeyLayout.CaseWidth - 8, (float)KeyLayout.CaseHeight - 8, 4f);

    /// <summary>The key field, which is the footprint of the switch and keycap layers.</summary>
    public static SKPath FieldOutline()
        => RoundedRect((float)KeyLayout.Bezel, (float)KeyLayout.Bezel, (float)KeyLayout.FieldWidth, (float)KeyLayout.FieldHeight, 3f);

    /// <summary>Switch top housings, one per key.</summary>
    public static SKPath SwitchHousings()
    {
        var path = new SKPath();
        var size = (float)KeyLayout.SwitchSize;

        foreach (var centre in KeyLayout.SwitchCentres())
        {
            path.AddRect(SKRect.Create((float)centre.X - size / 2f, (float)centre.Y - size / 2f, size, size));
        }

        return path;
    }

    /// <summary>The cross stems, which are what actually reads as "switch".</summary>
    public static SKPath SwitchStems()
    {
        var path = new SKPath();
        const float arm = 8f;
        const float thickness = 2.4f;

        foreach (var centre in KeyLayout.SwitchCentres())
        {
            path.AddRect(SKRect.Create((float)centre.X - thickness / 2f, (float)centre.Y - arm / 2f, thickness, arm));
            path.AddRect(SKRect.Create((float)centre.X - arm / 2f, (float)centre.Y - thickness / 2f, arm, thickness));
        }

        return path;
    }

    /// <summary>Keycap outlines, one per key, at the real per-key widths.</summary>
    public static SKPath Keycaps()
    {
        var path = new SKPath();

        foreach (var key in KeyLayout.Keys())
        {
            path.AddRoundRect(
                SKRect.Create((float)key.X + 1.5f, (float)key.Y + 1.5f, (float)key.Width - 3f, (float)key.Height - 3f),
                3.5f,
                3.5f);
        }

        return path;
    }

    /// <summary>The dished top of each cap, which gives the caps their sculpt without shading.</summary>
    public static SKPath KeycapDishes()
    {
        var path = new SKPath();

        foreach (var key in KeyLayout.Keys())
        {
            path.AddRoundRect(
                SKRect.Create((float)key.X + 4.5f, (float)key.Y + 4.5f, (float)key.Width - 9f, (float)key.Height - 9f),
                2.5f,
                2.5f);
        }

        return path;
    }

    private static SKPath RoundedRect(float x, float y, float width, float height, float radius)
    {
        var path = new SKPath();
        path.AddRoundRect(SKRect.Create(x, y, width, height), radius, radius);
        return path;
    }
}
