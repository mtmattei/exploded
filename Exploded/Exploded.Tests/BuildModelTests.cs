using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exploded.Catalog;
using Exploded.Presentation;
using NUnit.Framework;
using Uno.Extensions.Reactive;

namespace Exploded.Tests;

/// <summary>
/// The model against the in-memory kit, through the same feeds and states the
/// page binds to. Layer 2 is the plate (in stock), layer 1 the PCB (out).
///
/// Awaiting a feed takes one value from a fresh subscription, and a state write
/// propagates asynchronously, so a single read straight after a write can still
/// see the old value. Reads after a write go through <see cref="Eventually"/>,
/// which waits for the expected value the way a bound view would re-render.
/// </summary>
public class BuildModelTests
{
    private const int PlateLayer = 2;
    private const int PcbLayer = 1;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private BuildModel _model = null!;

    [SetUp]
    public void SetUp() => _model = new BuildModel(new InMemoryPartsCatalog());

    [Test]
    public async Task Opens_on_the_keycaps_with_an_empty_build()
    {
        Assert.That(await _model.SelectedLayer, Is.EqualTo(BuildModel.InitialLayer));
        Assert.That(await _model.Total, Is.EqualTo(0m));
        Assert.That((await _model.Action)!.Label, Is.EqualTo("Add keycaps to build"));
    }

    [Test]
    public async Task Lines_mark_the_selected_part_only()
    {
        await SelectAsync(PlateLayer);

        var lines = await Eventually(() => _model.Lines.AsFeed(), l => l?.Single(x => x.IsSelected).Key == PlateLayer);

        Assert.That(lines!.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task Toggle_adds_the_selected_part_then_removes_it()
    {
        await SelectAsync(PlateLayer);
        await _model.ToggleSelected(CancellationToken.None);

        await Eventually(() => _model.Total, 48.00m);
        await Eventually(() => _model.Lines.AsFeed(), l => l!.Single(x => x.Key == PlateLayer).IsInBuild);
        await Eventually(() => _model.Action, a => a?.Label == "Remove plate");

        await _model.ToggleSelected(CancellationToken.None);

        await Eventually(() => _model.Total, 0m);
    }

    [Test]
    public async Task Toggle_ignores_an_out_of_stock_part()
    {
        await SelectAsync(PcbLayer);
        await _model.ToggleSelected(CancellationToken.None);

        await Eventually(() => _model.Action, a => a is { IsEnabled: false });
        Assert.That((await _model.Build)!.Parts, Is.Empty);
    }

    [Test]
    public void A_failing_catalog_surfaces_as_a_feed_error()
    {
        var model = new BuildModel(new FailingCatalog());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await model.Kit);
    }

    /// <summary>Selects a layer and waits until the model reads it back, as a tap then a later Enter would.</summary>
    private async Task SelectAsync(int layer)
    {
        await _model.SelectedLayer.SetAsync(layer, CancellationToken.None);
        await Eventually(() => _model.SelectedLayer, layer);
    }

    private static Task<T?> Eventually<T>(Func<IFeed<T>> feed, T expected) where T : notnull
        => Eventually(feed, value => Equals(value, expected));

    private static async Task<T?> Eventually<T>(Func<IFeed<T>> feed, Func<T?, bool> matches) where T : notnull
    {
        var clock = Stopwatch.StartNew();
        T? value;

        while (!matches(value = await feed()))
        {
            if (clock.Elapsed > Timeout)
            {
                Assert.Fail($"Feed never reached the expected value; last saw {value}.");
            }

            await Task.Delay(10);
        }

        return value;
    }

    private sealed class FailingCatalog : IPartsCatalog
    {
        public ValueTask<PartsKit> GetKitAsync(CancellationToken ct)
            => throw new InvalidOperationException("catalog offline");
    }
}
