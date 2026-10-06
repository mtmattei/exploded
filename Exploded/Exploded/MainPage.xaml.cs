using System.Globalization;
using Exploded.Catalog;
using Exploded.Presentation;
using Exploded.Stage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;

namespace Exploded;

/// <summary>
/// The plate and its parts table.
///
/// Two binding surfaces meet here on purpose. Everything that is data (the kit,
/// the selected layer, the build) lives in <see cref="BuildModel"/> and reaches
/// the page through {Binding} on the generated view model. The stage follows
/// the separation slider through x:Bind instead, because it has to track the
/// thumb continuously and a feed would be the wrong tool for that.
///
/// What is left in code-behind is view work the model should not know about:
/// the Skia stage, the callout bubbles, and moving keyboard focus between rows.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly Ellipse[] _bubbleShells = new Ellipse[Explode.LayerCount];
    private readonly TextBlock[] _bubbleNumbers = new TextBlock[Explode.LayerCount];

    // Every selection or build change re-emits the parts lines, and the table
    // replaces the rows that changed, so a focused row is thrown away under the
    // user. The key of the row that should keep focus is held here until its
    // replacement loads.
    private int _refocusKey = -1;
    private FocusState _refocusState;

    public MainPage()
    {
        InitializeComponent();

        ViewModel = new BuildViewModel(new InMemoryPartsCatalog());
        DataContext = ViewModel;

        Plate.UseRenderer(new PlateRenderer(StagePalette()));

        BuildCallouts();

        // The bubbles are code-built, so they follow the selection off the
        // plate's bound property rather than through bindings of their own.
        Plate.RegisterPropertyChangedCallback(PlateCanvas.SelectedLayerProperty, (_, _) => ShowSelection());
        ShowSelection();
    }

    // Private: the XAML bindable-metadata generator cannot see MVUX-generated
    // types, and fails the build on a public member of one.
    private BuildViewModel ViewModel { get; }

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
                Fill = Palette.Paper,
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

    // ── selection ─────────────────────────────────────────────────────────

    private void ShowSelection()
    {
        var selected = Plate.SelectedLayer;

        for (var layer = 0; layer < Explode.LayerCount; layer++)
        {
            var isSelected = layer == selected;

            _bubbleShells[layer].Fill = isSelected ? Palette.Callout : Palette.Paper;
            _bubbleNumbers[layer].Foreground = isSelected ? Palette.Paper : Palette.Callout;
        }

        AutomationProperties.SetName(Plate, selected >= 0
            ? $"Exploded plate, part {Explode.CalloutNumber(selected)} selected"
            : "Exploded plate");
    }

    private void Select(int layerIndex)
    {
        if (ViewModel.SelectedLayer != layerIndex)
        {
            ViewModel.SelectedLayer = layerIndex;
        }
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
        if (sender is FrameworkElement { DataContext: PartLine line })
        {
            Select(line.Key);
            e.Handled = true;
        }
    }

    private void OnRowFocused(object sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PartLine line } row && line.Key != ViewModel.SelectedLayer)
        {
            HoldFocus(line.Key, row.FocusState);
            Select(line.Key);
        }
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PartLine line } row && line.Key == _refocusKey)
        {
            _refocusKey = -1;
            row.Focus(_refocusState);
        }
    }

    private void HoldFocus(int key, FocusState state)
    {
        _refocusKey = key;
        _refocusState = state == FocusState.Unfocused ? FocusState.Programmatic : state;
    }

    /// <summary>
    /// Up and Down walk the table, Enter and Space toggle the focused part in
    /// the build. Focus moves row to row by index so it can never wander out of
    /// the table the way directional XY focus would.
    /// </summary>
    private void OnRowKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PartLine line } row
            || VisualTreeHelper.GetParent(row) is not DependencyObject container
            || ItemsControl.ItemsControlFromItemContainer(container) is not ItemsControl list)
        {
            return;
        }

        var index = list.IndexFromContainer(container);

        switch (e.Key)
        {
            case VirtualKey.Down:
                e.Handled = FocusRow(list, index + 1);
                break;
            case VirtualKey.Up:
                e.Handled = FocusRow(list, index - 1);
                break;
            case VirtualKey.Enter:
            case VirtualKey.Space:
                HoldFocus(line.Key, ((Control)row).FocusState);
                Select(line.Key);
                ViewModel.ToggleSelected.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static bool FocusRow(ItemsControl list, int index)
        => index >= 0
           && list.ContainerFromIndex(index) is DependencyObject container
           && VisualTreeHelper.GetChildrenCount(container) > 0
           && VisualTreeHelper.GetChild(container, 0) is Control control
           && control.Focus(FocusState.Keyboard);
}
