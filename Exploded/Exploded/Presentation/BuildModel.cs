using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exploded.Catalog;
using Uno.Extensions.Reactive;

namespace Exploded.Presentation;

/// <summary>
/// Everything on the page that is data: the kit, which layer is selected, and
/// what is in the build. The generated <c>BuildViewModel</c> is the page's
/// DataContext.
///
/// Separation is deliberately not here. It changes on every pointer move while
/// the slider is dragged, and the stage reads it straight off the slider
/// through x:Bind; routing it through a feed would only add latency.
/// </summary>
public partial record BuildModel(IPartsCatalog Catalog)
{
    /// <summary>The layer selected when the page opens: the keycaps, top of the stack.</summary>
    public const int InitialLayer = 4;

    public IFeed<PartsKit> Kit => Feed.Async(Catalog.GetKitAsync);

    /// <summary>0..4, the sheet index. Drives the stage, the callouts and the row highlight.</summary>
    public IState<int> SelectedLayer => State.Value(this, () => InitialLayer);

    /// <summary>
    /// The build is one value, not a list state: an empty list state reads as
    /// None, and an empty build is a real value here with a total of $0.00.
    /// </summary>
    public IState<BuildList> Build => State.Value(this, () => BuildList.Empty);

    /// <summary>The parts table: each part with its selection and build marks.</summary>
    public IListFeed<PartLine> Lines => Feed
        .Combine(Kit, SelectedLayer, Build)
        .Select(x => x.Item1.Parts
            .Select(part => new PartLine(part, part.LayerIndex == x.Item2, x.Item3.Contains(part)))
            .ToImmutableList())
        .AsListFeed();

    public IFeed<string> KitName => Kit.Select(kit => kit.Name.ToUpperInvariant());
    public IFeed<string> FormFactor => Kit.Select(kit => kit.FormFactor);
    public IFeed<string> Revision => Kit.Select(kit => kit.Revision);

    /// <summary>A number rather than a label, so the view can count between totals.</summary>
    public IFeed<decimal> Total => Build.Select(build => build.Total);
    public IFeed<string> BuildHint => Build.Select(build => build.Hint);

    public IFeed<PartAction> Action => Feed
        .Combine(Kit, SelectedLayer, Build)
        .Select(x => PartAction.For(x.Item1.Parts.First(p => p.LayerIndex == x.Item2), x.Item3));

    /// <summary>
    /// Adds the selected part, or removes it if it is already in. Reads the
    /// selection from state rather than taking the row as a parameter.
    /// </summary>
    public async ValueTask ToggleSelected(CancellationToken ct)
    {
        var kit = await Kit;
        var layer = await SelectedLayer;
        var part = kit?.Parts.FirstOrDefault(p => p.LayerIndex == layer);

        if (part is { IsAvailable: true })
        {
            await Build.UpdateAsync(build => (build ?? BuildList.Empty).Toggle(part), ct);
        }
    }
}
