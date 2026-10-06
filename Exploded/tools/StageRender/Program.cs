using Exploded.Stage;
using SkiaSharp;
using Windows.Foundation;

var palette = new PlatePalette(
    Ink: SKColor.Parse("#1B1E1C"), InkFaint: SKColor.Parse("#A3A6A1"), Sweep: SKColor.Parse("#D9D7D2"), Paper: SKColor.Parse("#F0EEE9"),
    Callout: SKColor.Parse("#D2481E"), Board: SKColor.Parse("#2E6B4F"), Keycap: SKColor.Parse("#F0EEE9"), Housing: SKColor.Parse("#C9CBC6"),
    Steel: SKColor.Parse("#B9BDB8"), Aluminium: SKColor.Parse("#DEDCD6"));
using var renderer = new PlateRenderer(palette);
foreach (var (sep, sel) in new[] { (0d, -1), (50d, 2), (100d, 4) })
{
    using var surface = SKSurface.Create(new SKImageInfo((int)Explode.StageWidth * 2, (int)Explode.StageHeight * 2));
    var canvas = surface.Canvas;
    canvas.Clear(palette.Sweep);
    canvas.Scale(2);
    renderer.Render(canvas, sep, sel);
    // the hit test, read back at a few points
    var hits = new[] { new Point(430, 300), new Point(430, 150), new Point(200, 230), new Point(900, 50) }.Select(p => $"{p.X},{p.Y}->{renderer.HitTest(p, sep)}");
    Console.WriteLine($"sep {sep}: hits {string.Join(' ', hits)}");
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    File.WriteAllBytes(Path.Combine(args.Length > 0 ? args[0] : ".", sep switch { 0 => "stage-assembled.png", 100 => "stage-exploded.png", _ => "stage-separating.png" }), data.ToArray());
}
