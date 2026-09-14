using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Exploded.Catalog;
using Exploded.Presentation;
using Exploded.Stage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Exploded;

/// <summary>
/// The plate and its parts table.
///
/// Two binding surfaces meet here on purpose. The stage follows the separation
/// slider through x:Bind, because it has to track the thumb continuously and a
/// value pipeline would be the wrong tool for that. Selection and build
/// membership are plain imperative state over five rows, which is all this kit
/// needs.
///
/// The sheets themselves are drawn by <see cref="Stage.PlateCanvas"/> rather
/// than by XAML shapes: as Paths they were composition shape visuals, and Uno
/// recomputes an anti-aliasing damage path for every shape visual whose
/// transform changes, which cost about a blocking second of UI thread per step
/// of the slider.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IPartsCatalog _catalog = new InMemoryPartsCatalog();
    private readonly List<PartRow> _rows = new();

    private readonly Ellipse[] _bubbleShells = new Ellipse[Explode.LayerCount];
    private readonly TextBlock[] _bubbleNumbers = new TextBlock[Explode.LayerCount];

    private int _selected = 4;

    public MainPage()
    {
        InitializeComponent();

        Plate.UseRenderer(new PlateRenderer(StagePalette()));

        BuildCallouts();
        LoadKit();
        Select(_selected);
    }

    // ── x:Bind function targets ───────────────────────────────────────────
    // Each re-evaluates when its argument path (the slider's Value) changes.

    public Geometry LeaderLine(double separation, int layerIndex)
        => Explode.LeaderLine(separation, layerIndex);

    public double CalloutFade(double separation) => Explode.CalloutFade(separation);

    public string StageHint(double separation) => Explode.StageHint(separation);

    // ── the drawing ───────────────────────────────────────────────────────

    /// <summary>
    /// The stage palette, read out of the token dictionary here because the
    /// renderer draws with Skia colours and cannot resolve XAML resources.
    /// </summary>
    private static PlatePalette StagePalette() => new(
        Ink: Token("Ink"),
        InkFaint: Token("InkFaint"),
        Sweep: Token("Sweep"),
        Paper: Token("Paper"),
        Callout: Token("Callout"),
        Board: Token("Board"),
        Keycap: Token("Keycap"),
        Housing: Token("Housing"),
        Steel: Token("Steel"),
        Aluminium: Token("Aluminium"));

    private static SkiaSharp.SKColor Token(string key)
    {
        var color = ((SolidColorBrush)Application.Current.Resources[key]).Color;
        return new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
    }

    /// <summary>Five numbered bubbles on the fixed ladder down the right of the plate.</summary>
    private void BuildCallouts()
    {
        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var centre = Explode.BubbleCentre(layer);
            var diameter = Explode.BubbleRadius * 2d;

            var shell = new Ellipse
            {
                Fill = Palette.Get("Paper"),
                Stroke = Palette.Callout,
                StrokeThickness = 1.2d
            };

            var number = new TextBlock
            {
                Text = Explode.CalloutNumber(layer).ToString(CultureInfo.InvariantCulture),
                FontFamily = (FontFamily)Application.Current.Resources["Font.DataStrong"],
                FontSize = 11,
                Foreground = Palette.Callout,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var bubble = new Grid { Width = diameter, Height = diameter };
            bubble.Children.Add(shell);
            bubble.Children.Add(number);

            Canvas.SetLeft(bubble, centre.X - Explode.BubbleRadius);
            Canvas.SetTop(bubble, centre.Y - Explode.BubbleRadius);
            Bubbles.Children.Add(bubble);

            _bubbleShells[layer] = shell;
            _bubbleNumbers[layer] = number;
        }
    }

    private void LoadKit()
    {
        var kit = _catalog.GetKit();

        KitName.Text = kit.Name.ToUpperInvariant();
        TitleFormFactor.Text = kit.FormFactor;
        TitleRevision.Text = kit.Revision;

        foreach (var part in kit.Parts)
        {
            _rows.Add(new PartRow(part));
        }

        PartsList.ItemsSource = _rows;
        UpdateBuild();
    }

    // ── selection ─────────────────────────────────────────────────────────

    private void Select(int layerIndex)
    {
        _selected = layerIndex;

        Plate.SelectedLayer = layerIndex;

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var isSelected = layer == layerIndex;

            _bubbleShells[layer].Fill = isSelected ? Palette.Callout : Palette.Get("Paper");
            _bubbleNumbers[layer].Foreground = isSelected ? Palette.Get("Paper") : Palette.Callout;
        }

        foreach (var row in _rows)
        {
            row.IsSelected = row.LayerIndex == layerIndex;
        }

        UpdateAction();
    }

    private void OnPlateTapped(object sender, TappedRoutedEventArgs e)
    {
        var layerIndex = Plate.LayerAt(e.GetPosition(Plate));

        if (layerIndex >= 0)
        {
            Select(layerIndex);
            e.Handled = true;
        }
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PartRow row })
        {
            Select(row.LayerIndex);
            e.Handled = true;
        }
    }

    // ── the build ─────────────────────────────────────────────────────────

    private void OnToggleBuild(object sender, RoutedEventArgs e)
    {
        var row = _rows.FirstOrDefault(r => r.LayerIndex == _selected);

        if (row is null || !row.IsAvailable)
        {
            return;
        }

        row.IsInBuild = !row.IsInBuild;
        UpdateBuild();
    }

    private void UpdateBuild()
    {
        var added = _rows.Where(r => r.IsInBuild).ToList();
        var total = added.Sum(r => r.Part.Price);

        BuildTotal.Text = "$" + total.ToString("0.00", CultureInfo.InvariantCulture);

        BuildHint.Text = added.Count switch
        {
            0 => "Nothing added yet. Tap a layer to price it.",
            1 => "1 of 5 parts added.",
            _ => $"{added.Count} of 5 parts added."
        };

        UpdateAction();
    }

    private void UpdateAction()
    {
        var row = _rows.FirstOrDefault(r => r.LayerIndex == _selected);

        if (row is null)
        {
            return;
        }

        if (!row.IsAvailable)
        {
            ToggleButton.IsEnabled = false;
            ToggleButton.Content = $"{row.Name} is out of stock";
            return;
        }

        ToggleButton.IsEnabled = true;
        ToggleButton.Content = row.IsInBuild
            ? $"Remove {row.Name.ToLowerInvariant()}"
            : $"Add {row.Name.ToLowerInvariant()} to build";
    }
}
