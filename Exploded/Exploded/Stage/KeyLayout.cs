using System.Collections.Generic;
using Windows.Foundation;

namespace Exploded.Stage;

/// <summary>
/// Key geometry for a 61-key ANSI 60% board, measured in key units where 1u is
/// one key pitch. One table, five consumers: keycaps, switches, plate cutouts,
/// PCB sockets and the case outline all measure from here, so they cannot drift
/// out of register with each other.
/// </summary>
internal static class KeyLayout
{
    /// <summary>Design pixels per key unit. Everything in the stage is written at its real number and the Viewbox does the scaling.</summary>
    public const double Unit = 34d;

    /// <summary>Case bezel around the key field.</summary>
    public const double Bezel = 11d;

    /// <summary>1u pitch is 19.05 mm, which sets the scale for millimetre-specified parts.</summary>
    public const double PixelsPerMm = Unit / 19.05d;

    public const double FieldWidth = 15d * Unit;
    public const double FieldHeight = 5d * Unit;
    public const double CaseWidth = FieldWidth + 2d * Bezel;
    public const double CaseHeight = FieldHeight + 2d * Bezel;

    /// <summary>A switch top housing is 14 mm square, which is also the plate cutout.</summary>
    public static readonly double SwitchSize = 14d * PixelsPerMm;

    /// <summary>
    /// Key widths per row, in units. Every row sums to 15u and the five rows
    /// total 61 keys, which is what makes this an ANSI 60%.
    /// </summary>
    private static readonly double[][] Rows =
    {
        new[] { 1d, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2 },          // grave .. backspace
        new[] { 1.5d, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5 },      // tab .. backslash
        new[] { 1.75d, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25 },       // caps .. enter
        new[] { 2.25d, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75 },          // shift .. shift
        new[] { 1.25d, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25 }     // modifiers and space
    };

    /// <summary>Every key's footprint, in case coordinates.</summary>
    public static IEnumerable<Rect> Keys()
    {
        for (var row = 0; row < Rows.Length; row++)
        {
            var x = Bezel;
            var y = Bezel + row * Unit;

            foreach (var widthInUnits in Rows[row])
            {
                var width = widthInUnits * Unit;
                yield return new Rect(x, y, width, Unit);
                x += width;
            }
        }
    }

    /// <summary>
    /// Where a switch sits under each key. Wide keys carry stabilisers either
    /// side in reality, but a plate drawing shows one switch per key, centred.
    /// </summary>
    public static IEnumerable<Point> SwitchCentres()
    {
        foreach (var key in Keys())
        {
            yield return new Point(key.X + key.Width / 2d, key.Y + key.Height / 2d);
        }
    }
}
