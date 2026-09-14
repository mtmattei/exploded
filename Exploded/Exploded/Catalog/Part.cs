using System.Globalization;

namespace Exploded.Catalog;

public enum Availability
{
    InStock,
    LowStock,
    OutOfStock
}

/// <summary>
/// One purchasable layer of the build.
/// </summary>
/// <param name="LayerIndex">
/// Position in the physical stack: 0 is the case at the bottom, 4 the keycaps on
/// top. This is the index the stage transforms by, so it is the identity that
/// ties a catalogue row to a drawn sheet.
/// </param>
public record Part(
    int LayerIndex,
    string Name,
    string Spec,
    string Sku,
    decimal Price,
    Availability Status,
    string Swatch)
{
    /// <summary>Callout number. Parts lists are numbered top down, so the keycaps are part 1.</summary>
    public int Number => 5 - LayerIndex;

    public bool IsAvailable => Status != Availability.OutOfStock;

    /// <summary>Formatted invariantly so the plate reads the same on any machine locale.</summary>
    public string PriceLabel => "$" + Price.ToString("0.00", CultureInfo.InvariantCulture);

    public string StatusLabel => Status switch
    {
        Availability.InStock => "IN STOCK",
        Availability.LowStock => "LOW STOCK",
        _ => "OUT OF STOCK"
    };
}
