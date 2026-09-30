namespace NepalHub.Api.Features.Calculators.Handlers;

/// <summary>What a given percentage of a value comes to.</summary>
internal sealed class PercentageHandler : ICalculatorHandler
{
    public string Slug => "percentage-calculator";

    public CalculationResult Calculate(IReadOnlyDictionary<string, decimal> inputs)
    {
        var value = (double)inputs["value"];
        var percent = (double)inputs["percent"];

        var result = value * percent / 100d;
        var remainder = value - result;

        return new CalculationResult
        {
            Summary =
            [
                CalculatorMath.Numeric("result", "Result", result, null, ValueFormat.Number),
            ],
            Breakdown =
            [
                CalculatorMath.Numeric("value", "Value", value, null, ValueFormat.Number),
                CalculatorMath.Numeric("percent", "Percentage", percent, "%", ValueFormat.Percent),
                CalculatorMath.Numeric("remainder", "Remainder", remainder, null, ValueFormat.Number),
            ],
            Notes = ["A percentage above 100 gives a result larger than the original value."],
        };
    }
}
