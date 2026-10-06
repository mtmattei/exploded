using Exploded.Catalog;

namespace Exploded.Presentation;

/// <summary>
/// One row of the parts table: the part plus the two marks the table shows.
/// <see cref="Key"/> is the layer index, which gives the record generated key
/// equality, so a selection change is seen as the same five rows updated
/// rather than five new ones.
/// </summary>
public partial record PartLine(Part Part, bool IsSelected, bool IsInBuild)
{
    public int Key => Part.LayerIndex;

    public string NumberLabel => Part.Number.ToString("00");
    public string Name => Part.Name;
    public string Spec => Part.Spec;
    public string Sku => Part.Sku;
    public string PriceLabel => Part.PriceLabel;
    public string StatusLabel => Part.StatusLabel;
    public string Swatch => Part.Swatch;
    public bool IsAvailable => Part.IsAvailable;

    /// <summary>What a screen reader announces for the row: everything the row shows, in reading order.</summary>
    public string AutomationName =>
        $"Part {Part.Number}, {Name}, {Spec}, {PriceLabel}, {StatusLabel.ToLowerInvariant()}"
        + (IsInBuild ? ", in build" : string.Empty);
}
