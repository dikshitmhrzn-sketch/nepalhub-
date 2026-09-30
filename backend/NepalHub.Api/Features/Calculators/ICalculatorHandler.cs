namespace NepalHub.Api.Features.Calculators;

/// <summary>
/// Computes one calculator. Every calculation runs on the server, so the browser
/// ships no financial or health arithmetic and a published calculator with no
/// handler is rejected rather than left broken.
/// </summary>
public interface ICalculatorHandler
{
    /// <summary>Matches <c>calculators.slug</c>.</summary>
    string Slug { get; }

    /// <summary>
    /// Runs the calculation. Inputs are already validated against the field
    /// definitions, so a handler may assume every value is present and in range.
    /// </summary>
    CalculationResult Calculate(IReadOnlyDictionary<string, decimal> inputs);
}

public sealed record CalculationResult
{
    public IReadOnlyList<CalculationLine> Summary { get; init; } = [];

    public IReadOnlyList<CalculationLine> Breakdown { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// One output row. A line is either numeric (<see cref="Value"/>) or textual
/// (<see cref="Text"/>), which is how a categorical answer such as a risk band is
/// returned without faking a number.
/// </summary>
public sealed record CalculationLine(
    string Key,
    string Label,
    ValueFormat Format,
    decimal? Value = null,
    string? Unit = null,
    string? Text = null);

/// <summary>Tells the client how to render a value; it never formats numbers itself.</summary>
public enum ValueFormat
{
    Number = 0,
    Currency = 1,
    Percent = 2,
    Text = 3,
}
