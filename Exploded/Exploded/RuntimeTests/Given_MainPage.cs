using System;
using System.Linq;
using System.Threading.Tasks;
using Exploded.Presentation;
using Exploded.Stage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Uno.UI.RuntimeTests;
using Windows.System;

namespace Exploded.RuntimeTests;

/// <summary>
/// The page in the running app: real visual tree, real bindings, injected
/// pointer input. Keyboard cannot be injected on Skia, so key behaviour goes
/// through <see cref="MainPage.HandleRowKey"/> and focus moves are made with
/// FocusState.Keyboard, which is what a Tab produces.
///
/// Full window, so the page lays out at the app window's size and injected
/// taps land on the rows rather than off the edge of the runner's side panel.
///
/// Layer indices: 4 keycaps (selected on open), 3 switches, 2 plate, 1 PCB
/// (out of stock), 0 case.
/// </summary>
[TestClass]
[RunsOnUIThread]
[RequiresFullWindow]
public class Given_MainPage
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private MainPage _page = null!;

    [TestInitialize]
    public async Task Load()
    {
        _page = new MainPage();
        UnitTestsUIContentHelper.Content = _page;

        await UnitTestsUIContentHelper.WaitForLoaded(_page);
        await TestHelper.WaitFor(() => Rows().Length == 5, Timeout);
        await UnitTestsUIContentHelper.WaitForIdle();
    }

    [TestMethod]
    public void When_loaded_Then_five_rows_and_the_keycaps_are_selected()
    {
        Assert.AreEqual(5, Rows().Length);
        Assert.AreEqual(4, Selected());
        Assert.AreEqual("Add keycaps to build", ActionButton().Content);
    }

    [TestMethod]
    public async Task When_a_row_is_tapped_Then_it_is_selected_everywhere()
    {
        InputInjectorHelper.Current.Tap(Row(2));

        await TestHelper.WaitFor(() => Selected() == 2, Timeout);
        await TestHelper.WaitFor(() => Plate().SelectedLayer == 2, Timeout);
        await TestHelper.WaitFor(() => (ActionButton().Content as string) == "Add plate to build", Timeout);
    }

    [TestMethod]
    public async Task When_add_is_tapped_Then_the_part_is_marked_and_the_total_counts_to_its_price()
    {
        InputInjectorHelper.Current.Tap(Row(2));
        await TestHelper.WaitFor(() => (ActionButton().Content as string) == "Add plate to build", Timeout);

        InputInjectorHelper.Current.Tap(ActionButton());

        await TestHelper.WaitFor(() => Line(2).IsInBuild, Timeout);
        await TestHelper.WaitFor(() => Total() == "$48.00", Timeout);
        Assert.AreEqual("Remove plate", ActionButton().Content);
    }

    [TestMethod]
    public async Task When_an_out_of_stock_part_is_selected_Then_add_is_disabled()
    {
        InputInjectorHelper.Current.Tap(Row(1));

        await TestHelper.WaitFor(() => !ActionButton().IsEnabled, Timeout);
        Assert.AreEqual("PCB is out of stock", ActionButton().Content);
    }

    [TestMethod]
    public async Task When_a_row_takes_keyboard_focus_Then_it_is_selected_and_keeps_focus()
    {
        Row(3).Focus(FocusState.Keyboard);

        await TestHelper.WaitFor(() => Selected() == 3, Timeout);

        // Selecting re-emits the rows and replaces the focused one; the page has
        // to hand focus to the replacement, with the same keyboard focus state.
        await TestHelper.WaitFor(() => FocusedRow() is { } row && Key(row) == 3 && row.FocusState == FocusState.Keyboard, Timeout);
    }

    [TestMethod]
    public async Task When_down_then_up_Then_focus_and_selection_walk_the_table()
    {
        Row(4).Focus(FocusState.Keyboard);
        await TestHelper.WaitFor(() => FocusedRow() is { } r && Key(r) == 4, Timeout);

        Assert.IsTrue(_page.HandleRowKey(FocusedRow()!, VirtualKey.Down));
        await TestHelper.WaitFor(() => Selected() == 3 && FocusedRow() is { } r && Key(r) == 3, Timeout);

        Assert.IsTrue(_page.HandleRowKey(FocusedRow()!, VirtualKey.Down));
        await TestHelper.WaitFor(() => Selected() == 2 && FocusedRow() is { } r && Key(r) == 2, Timeout);

        Assert.IsTrue(_page.HandleRowKey(FocusedRow()!, VirtualKey.Up));
        await TestHelper.WaitFor(() => Selected() == 3 && FocusedRow() is { } r && Key(r) == 3, Timeout);
    }

    [TestMethod]
    public async Task When_up_on_the_first_row_Then_the_key_is_not_handled()
    {
        Row(4).Focus(FocusState.Keyboard);
        await TestHelper.WaitFor(() => FocusedRow() is { } r && Key(r) == 4, Timeout);

        Assert.IsFalse(_page.HandleRowKey(FocusedRow()!, VirtualKey.Up));
    }

    [TestMethod]
    public async Task When_enter_on_a_row_Then_it_toggles_and_focus_stays_on_it()
    {
        Row(2).Focus(FocusState.Keyboard);
        await TestHelper.WaitFor(() => Selected() == 2 && FocusedRow() is { } r && Key(r) == 2, Timeout);

        Assert.IsTrue(_page.HandleRowKey(FocusedRow()!, VirtualKey.Enter));

        await TestHelper.WaitFor(() => Line(2).IsInBuild, Timeout);
        await TestHelper.WaitFor(() => FocusedRow() is { } r && Key(r) == 2, Timeout);

        Assert.IsTrue(_page.HandleRowKey(FocusedRow()!, VirtualKey.Space));

        await TestHelper.WaitFor(() => !Line(2).IsInBuild, Timeout);
        await TestHelper.WaitFor(() => Total() == "$0.00", Timeout);
    }

    // ── the page, read through its visual tree ─────────────────────────────

    private ContentControl[] Rows()
        => UIHelper.GetChildren<ContentControl>(_page).Where(c => c.DataContext is PartLine).ToArray();

    private ContentControl Row(int layer) => Rows().Single(r => Key(r) == layer);

    private PartLine Line(int layer) => (PartLine)Row(layer).DataContext;

    private static int Key(FrameworkElement row) => ((PartLine)row.DataContext).Key;

    private int Selected() => Rows().Select(r => (PartLine)r.DataContext).Single(l => l.IsSelected).Key;

    private ContentControl? FocusedRow()
        => FocusManager.GetFocusedElement(_page.XamlRoot!) as ContentControl is { DataContext: PartLine } row ? row : null;

    private PlateCanvas Plate() => UIHelper.GetChild<PlateCanvas>(_page);

    private Button ActionButton()
        => UIHelper.GetChildren<Button>(_page).Single(b => b.Command is not null && b.Style == (Style)_page.Resources["PrimaryButton"]);

    private string? Total()
        => UIHelper.GetChildren<TextBlock>(_page).Single(t => t.ReadLocalValue(CountUp.AmountProperty) != DependencyProperty.UnsetValue).Text;
}
