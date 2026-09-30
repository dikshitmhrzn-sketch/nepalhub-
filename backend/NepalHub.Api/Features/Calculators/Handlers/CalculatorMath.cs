namespace NepalHub.Api.Features.Calculators.Handlers;

internal static class CalculatorMath
{
    /// <summary>A numeric output row, rounded to a sane number of decimal places.</summary>
    internal static CalculationLine Numeric(
        string key,
        string label,
        double value,
        string? unit,
        ValueFormat format,
        int places = 2) =>
        new(key, label, format, Round(value, places), unit);

    /// <summary>A textual output row, such as a risk band.</summary>
    internal static CalculationLine Label(string key, string label, string text) =>
        new(key, label, ValueFormat.Text, Text: text);

    /// <summary>
    /// Rounds half away from zero, so a half-rupee never silently rounds down. A
    /// non-finite result means the inputs produced an impossible figure, which the
    /// service reports as a failed calculation rather than returning a wrong number.
    /// </summary>
    internal static decimal Round(double value, int places)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException("The calculation produced a value that is not a number.");
        }

        return Math.Round((decimal)value, places, MidpointRounding.AwayFromZero);
    }
}
