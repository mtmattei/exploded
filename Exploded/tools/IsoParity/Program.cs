using System.Globalization;
using System.Text;
using Exploded.Stage;

// Prints the same cases the Node side prints, as JSON, for a numeric diff.
var sb = new StringBuilder();
var inv = CultureInfo.InvariantCulture;
string N(double v) => v.ToString("R", inv);
string Pts(IEnumerable<Vec2> pts) => "[" + string.Join(",", pts.Select(p => $"[{N(p.X)},{N(p.Y)}]")) + "]";
string Smp(IEnumerable<Sample> s) => "[" + string.Join(",", s.Select(q => $"[{N(q.U)},{N(q.V)},{N(q.Nu)},{N(q.Nv)}]")) + "]";

var cases = new List<string>();
foreach (var (az, k, s) in new[] { (45d, 0.5, 1.62), (0d, 0.34, 2d), (123d, 0.64, 1.3), (-80d, 0.5, 1.85) })
{
    var c = Iso.Cam(az, k, s);
    Iso.Fit(c, new[] { new Vec3(0, 0, 0), new Vec3(100, 80, 0), new Vec3(0, 0, 40) }, 200, 160);
    var p = Iso.Proj(c);
    var pts = new[] { (0d, 0d, 0d), (12.5, -40, 0), (80, 33, 17), (-20, 60, -9) };
    cases.Add($"{{\"cam\":[{N(c.Ox)},{N(c.Oy)}],\"proj\":{Pts(pts.Select(q => p(q.Item1, q.Item2, q.Item3)))},\"unproj\":{Pts(pts.Select(q => { var sp = p(q.Item1, q.Item2, q.Item3); return Iso.Unproj(c, sp.X, sp.Y, q.Item3); }))}}}");
}
var C = Iso.Cam(45, 0.5, 1.42);
Iso.Fit(C, new[] { new Vec3(0, 0, 0), new Vec3(132, 96, 0), new Vec3(132, 0, 0), new Vec3(0, 96, 0), new Vec3(0, 0, 104.4), new Vec3(132, 0, 104.4) }, 180, 166);
var P = Iso.Proj(C);
var front = Iso.Facing(C);
var (ring, inner) = Iso.Rings(0, 0, 132, 96, 7, 1.3);
var prism = Iso.Prism(P, front, ring, inner, 0, 2.4);
var foot = Iso.Rrect(0.8, 0.8, 13.2, 13.2, 2.6);
var top = Iso.Rrect(2.6, 2.6, 11.4, 11.4, 2.0);
var tin = Iso.Rrect(3.5, 3.5, 10.5, 10.5, 1.2);
var taper = Iso.Taper(P, front, foot, top, tin, 1, 11.3);
var (l, r, n) = Iso.Extremes(P, ring);
var card = Iso.Fillet(new[] { new Vec2(0, 0), new Vec2(84, 0), new Vec2(84, 54), new Vec2(28, 54), new Vec2(28, 61), new Vec2(6, 61), new Vec2(6, 54), new Vec2(0, 54) }, new[] { 1d, 1, 3.2, 1.8, 2.4, 2.4, 1.8, 3.2 });
var circ = Iso.Circ(5, 24);
var hull = Iso.Hull(new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10), new Vec2(5, 5), new Vec2(5, 12), new Vec2(-1, 5) });
sb.Append("{\"cams\":[").Append(string.Join(",", cases)).Append("],");
sb.Append("\"ring\":").Append(Smp(ring)).Append(",\"inner\":").Append(Smp(inner)).Append(',');
sb.Append("\"sil\":").Append(Pts(prism.Silhouette)).Append(",\"crease\":").Append(Pts(prism.Crease)).Append(',');
sb.Append("\"tsil\":").Append(Pts(taper.Silhouette)).Append(",\"tcrease\":").Append(Pts(taper.Crease)).Append(',');
sb.Append("\"ext\":").Append(Smp(new[] { l, r, n })).Append(",\"fillet\":").Append(Pts(card)).Append(',');
sb.Append("\"circ\":").Append(Smp(circ)).Append(",\"hull\":").Append(Pts(hull)).Append('}');
Console.WriteLine(sb.ToString());
