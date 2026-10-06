using System.Linq;
using Exploded.Catalog;

namespace Exploded.Presentation;

/// <summary>The parts the user has added, and what they come to.</summary>
public record BuildList(ImmutableList<Part> Parts)
{
    public static BuildList Empty { get; } = new(ImmutableList<Part>.Empty);

    public decimal Total => Parts.Sum(p => p.Price);

    public string TotalLabel => Part.FormatPrice(Total);

    public string Hint => Parts.Count switch
    {
        0 => "Nothing added yet. Tap a layer to price it.",
        1 => "1 of 5 parts added.",
        var n => $"{n} of 5 parts added."
    };

    public bool Contains(Part part) => Parts.Any(p => p.LayerIndex == part.LayerIndex);

    public BuildList Toggle(Part part) => Contains(part)
        ? new(Parts.RemoveAll(p => p.LayerIndex == part.LayerIndex))
        : new(Parts.Add(part));
}

/// <summary>What the primary button says and whether it can act, for the selected part.</summary>
public record PartAction(string Label, bool IsEnabled)
{
    public static PartAction For(Part part, BuildList build) =>
        !part.IsAvailable ? new($"{part.Name} is out of stock", false)
        : build.Contains(part) ? new($"Remove {part.Name.ToLowerInvariant()}", true)
        : new($"Add {part.Name.ToLowerInvariant()} to build", true);
}
