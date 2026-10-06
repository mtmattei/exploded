using System.Threading;
using System.Threading.Tasks;

namespace Exploded.Catalog;

/// <summary>A named build and the parts it is made of, ordered top down for the parts list.</summary>
public record PartsKit(string Name, string FormFactor, string Revision, ImmutableArray<Part> Parts);

/// <summary>
/// The seam. Today the kit is a fixed local build; behind this interface it could
/// become a network catalogue without the stage or the table knowing. It is
/// async already so the model and the table are written for that shape.
/// </summary>
public interface IPartsCatalog
{
    ValueTask<PartsKit> GetKitAsync(CancellationToken ct);
}

public sealed class InMemoryPartsCatalog : IPartsCatalog
{
    public ValueTask<PartsKit> GetKitAsync(CancellationToken ct) => ValueTask.FromResult(Kit);

    private static readonly PartsKit Kit = new(
        Name: "Sixty",
        FormFactor: "60% TRAY MOUNT",
        Revision: "REV B",
        Parts:
        [
            new Part(4, "Keycaps", "PBT dye-sublimated, Cherry profile", "KC-PBT-60", 139.00m, Availability.InStock, "Keycap"),
            new Part(3, "Switches", "Linear 45 g, factory lubed, 61 pcs", "SW-LIN-45", 62.00m, Availability.InStock, "Housing"),
            new Part(2, "Plate", "Steel, 1.5 mm, 60% ANSI cutouts", "PL-STL-60", 48.00m, Availability.LowStock, "Steel"),
            new Part(1, "PCB", "Hot-swap, USB-C, QMK firmware", "PB-HS-60", 89.00m, Availability.OutOfStock, "Board"),
            new Part(0, "Case", "Anodized aluminium, tray mount", "CS-ALU-60", 163.00m, Availability.InStock, "Aluminium")
        ]);
}
