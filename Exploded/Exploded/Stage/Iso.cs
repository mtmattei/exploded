using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Exploded.Stage;

/// <summary>A point on a rounded outline, with its outward normal on the ground plane.</summary>
internal readonly record struct Sample(double U, double V, double NU, double NV);

/// <summary>
/// The drawing maths of Hairline (github.com/lucasmarkes/hairline, MIT,
/// copyright (c) 2026 Lucas Marques), ported from its <c>core/iso.ts</c>
/// arithmetic for arithmetic: an orthographic camera, rounded rings, and
/// solids drawn as a silhouette and one crease.
///
/// World space is x/y on the ground and z up. There is no hidden-line
/// removal: plates are filled with the ground colour and painted back to
/// front, so a nearer one simply covers.
/// </summary>
internal sealed class IsoCamera
{
    private readonly double _cos;
    private readonly double _sin;
    private readonly double _zf;

    /// <param name="azimuthDegrees">Turn of the ground plane; 45 is the classic view.</param>
    /// <param name="k">sin(elevation); 0.5 is the 2:1 view.</param>
    public IsoCamera(double azimuthDegrees, double k)
    {
        var az = azimuthDegrees * Math.PI / 180d;
        _cos = Math.Cos(az);
        _sin = Math.Sin(az);
        K = k;
        _zf = Math.Sqrt(1d - k * k);
    }

    public double K { get; }
    public double S { get; private set; } = 1d;
    public double Ox { get; private set; }
    public double Oy { get; private set; }

    /// <summary>Screen distance one world unit of height lifts a point.</summary>
    public double ZStep => S * _zf;

    public SKPoint Project(double x, double y, double z)
    {
        var sx = x * _cos - y * _sin;
        var sy = x * _sin + y * _cos;
        return new SKPoint((float)(Ox + S * sx), (float)(Oy + S * (sy * K - z * _zf)));
    }

    /// <summary>The world x/y under a screen point, on the plane at height z.</summary>
    public (double X, double Y) Unproject(double sx, double sy, double z)
    {
        var x = (sx - Ox) / S;
        var y = ((sy - Oy) / S + z * _zf) / K;
        return (x * _cos + y * _sin, -x * _sin + y * _cos);
    }

    /// <summary>
    /// Scales the camera so the box of <paramref name="points"/> fits
    /// <paramref name="width"/> by <paramref name="height"/>, then centres it on
    /// (cx, cy). Hairline's <c>fit</c> only centres; choosing S by the box is
    /// the same arithmetic run once more.
    /// </summary>
    public void Fit(IReadOnlyList<(double X, double Y, double Z)> points, double cx, double cy, double width, double height)
    {
        S = 1d;
        Ox = 0d;
        Oy = 0d;
        var (w, h, _, _) = Box(points);
        S = Math.Min(width / w, height / h);

        var (_, _, mx, my) = Box(points);
        Ox = cx - mx;
        Oy = cy - my;
    }

    /// <summary>Whether a ring sample faces the camera: its normal against the world direction of screen-down.</summary>
    public bool Facing(Sample q) => q.NU * _sin + q.NV * _cos >= -1e-6;

    private (double W, double H, double MidX, double MidY) Box(IReadOnlyList<(double X, double Y, double Z)> points)
    {
        double a = 1e9, b = -1e9, c = 1e9, d = -1e9;

        foreach (var (x, y, z) in points)
        {
            var p = Project(x, y, z);
            a = Math.Min(a, p.X);
            b = Math.Max(b, p.X);
            c = Math.Min(c, p.Y);
            d = Math.Max(d, p.Y);
        }

        return (b - a, d - c, (a + b) / 2d, (c + d) / 2d);
    }
}

internal static class Iso
{
    /// <summary>A rounded rectangle, sampled, n samples per corner, each with its outward normal.</summary>
    public static Sample[] RRect(double u0, double v0, double u1, double v1, double r, int n = 4)
    {
        r = Math.Max(0d, Math.Min(r, Math.Min((u1 - u0) / 2d, (v1 - v0) / 2d)));
        var corners = new (double U, double V, double A)[] { (u1 - r, v1 - r, 0), (u0 + r, v1 - r, 90), (u0 + r, v0 + r, 180), (u1 - r, v0 + r, 270) };
        var output = new List<Sample>(4 * (n + 1));

        foreach (var (cu, cv, a0) in corners)
        {
            for (var k = 0; k <= n; k++)
            {
                var a = (a0 + 90d * k / n) * Math.PI / 180d;
                var ca = Math.Cos(a);
                var sa = Math.Sin(a);
                output.Add(new Sample(cu + r * ca, cv + r * sa, ca, sa));
            }
        }

        return output.ToArray();
    }

    /// <summary>A circle of radius R about (cu, cv), sampled the same way.</summary>
    public static Sample[] Circle(double cu, double cv, double radius, int n)
    {
        var output = new Sample[n];

        for (var k = 0; k < n; k++)
        {
            var a = (double)k / n * Math.PI * 2d;
            output[k] = new Sample(cu + radius * Math.Cos(a), cv + radius * Math.Sin(a), Math.Cos(a), Math.Sin(a));
        }

        return output;
    }

    /// <summary>A rounded footprint and its crease ring, inset by b.</summary>
    public static (Sample[] Ring, Sample[] Inner) Rings(double x0, double y0, double x1, double y1, double r, double b)
        => (RRect(x0, y0, x1, y1, r), RRect(x0 + b, y0 + b, x1 - b, y1 - b, Math.Max(0.3d, r - b)));

    public static SKPoint[] RingAt(IsoCamera c, IReadOnlyList<Sample> ring, double z)
    {
        var output = new SKPoint[ring.Count];

        for (var i = 0; i < ring.Count; i++)
        {
            output[i] = c.Project(ring[i].U, ring[i].V, z);
        }

        return output;
    }

    /// <summary>The one cyclic run of samples that pass <paramref name="keep"/>, in ring order.</summary>
    public static Sample[] Run(IReadOnlyList<Sample> ring, Func<Sample, bool> keep)
    {
        var n = ring.Count;
        var start = -1;

        for (var i = 0; i < n; i++)
        {
            if (keep(ring[i]) && !keep(ring[(i + n - 1) % n]))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return keep(ring[0]) ? [.. ring] : [];
        }

        var output = new List<Sample>();

        for (var k = 0; k < n && keep(ring[(start + k) % n]); k++)
        {
            output.Add(ring[(start + k) % n]);
        }

        return output.ToArray();
    }

    /// <summary>Convex hull (monotone chain) of screen points.</summary>
    public static SKPoint[] Hull(IEnumerable<SKPoint> input)
    {
        var pts = new List<SKPoint>(input);
        pts.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        static double Cross(SKPoint o, SKPoint a, SKPoint b) => (a.X - o.X) * (double)(b.Y - o.Y) - (a.Y - o.Y) * (double)(b.X - o.X);

        var lower = new List<SKPoint>();
        var upper = new List<SKPoint>();

        foreach (var p in pts)
        {
            while (lower.Count > 1 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        for (var i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count > 1 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower.ToArray();
    }

    /// <summary>A solid standing from z0 to z1: the hull of its two rings, so no vertical corner is ever drawn.</summary>
    public static SKPoint[] Silhouette(IsoCamera c, IReadOnlyList<Sample> foot, double z0, IReadOnlyList<Sample> top, double z1)
    {
        var points = new List<SKPoint>(RingAt(c, foot, z0));
        points.AddRange(RingAt(c, top, z1));
        return Hull(points);
    }

    /// <summary>The one dim line inside a solid: the front run of an inset ring on its lid, which reads as a bevel.</summary>
    public static SKPoint[] Crease(IsoCamera c, IReadOnlyList<Sample> inner, double z)
        => RingAt(c, Run(inner, c.Facing), z);

    /// <summary>The leftmost, rightmost and nearest samples of a ring: where construction lines drop from.</summary>
    public static Sample[] Extremes(IsoCamera c, IReadOnlyList<Sample> ring)
    {
        int a = 0, b = 0, d = 0;
        var pr = RingAt(c, ring, 0d);

        for (var k = 0; k < pr.Length; k++)
        {
            if (pr[k].X < pr[a].X) a = k;
            if (pr[k].X > pr[b].X) b = k;
            if (pr[k].Y > pr[d].Y) d = k;
        }

        return [ring[a], ring[b], ring[d]];
    }

    public static void AddPoly(SKPath path, SKPoint[] points)
    {
        if (points.Length > 1)
        {
            path.AddPoly(points, close: true);
        }
    }

    public static void AddOpen(SKPath path, SKPoint[] points)
    {
        if (points.Length > 1)
        {
            path.AddPoly(points, close: false);
        }
    }
}
