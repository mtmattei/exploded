using System.Linq;
using Exploded.Catalog;
using Exploded.Presentation;
using NUnit.Framework;

namespace Exploded.Tests;

public class BuildListTests
{
    private static readonly Part Plate = new(2, "Plate", "Steel", "PL", 48.00m, Availability.LowStock, "Steel");
    private static readonly Part Case = new(0, "Case", "Alu", "CS", 163.00m, Availability.InStock, "Aluminium");
    private static readonly Part Pcb = new(1, "PCB", "Hot-swap", "PB", 89.00m, Availability.OutOfStock, "Board");

    [Test]
    public void Empty_build_is_a_value_with_a_zero_total()
    {
        Assert.That(BuildList.Empty.TotalLabel, Is.EqualTo("$0.00"));
        Assert.That(BuildList.Empty.Hint, Is.EqualTo("Nothing added yet. Tap a layer to price it."));
    }

    [Test]
    public void Toggle_adds_then_removes()
    {
        var added = BuildList.Empty.Toggle(Plate).Toggle(Case);

        Assert.That(added.TotalLabel, Is.EqualTo("$211.00"));
        Assert.That(added.Hint, Is.EqualTo("2 of 5 parts added."));

        var removed = added.Toggle(Plate);

        Assert.That(removed.Parts.Single(), Is.EqualTo(Case));
        Assert.That(removed.Hint, Is.EqualTo("1 of 5 parts added."));
    }

    [Test]
    public void Action_names_the_part_and_what_the_button_will_do()
    {
        Assert.That(PartAction.For(Plate, BuildList.Empty), Is.EqualTo(new PartAction("Add plate to build", true)));
        Assert.That(PartAction.For(Plate, BuildList.Empty.Toggle(Plate)), Is.EqualTo(new PartAction("Remove plate", true)));
        Assert.That(PartAction.For(Pcb, BuildList.Empty), Is.EqualTo(new PartAction("PCB is out of stock", false)));
    }
}
