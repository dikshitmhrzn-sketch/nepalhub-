namespace NepalHub.Api.Features.Calculators.Handlers;

/// <summary>Compound interest: <c>A = P x (1 + r/n)^(n x t)</c>.</summary>
internal sealed class CompoundInterestHandler : ICalculatorHandler
{
    public string Slug => "compound-interest";

    public CalculationResult Calculate(IReadOnlyDictionary<string, decimal> inputs)
    {
        var principal = (double)inputs["principal"];
        var annualRate = (double)inputs["annualRate"];
        var years = (double)inputs["years"];
        var compoundsPerYear = (double)inputs["compoundsPerYear"];

        var ratePerPeriod = annualRate / (100d * compoundsPerYear);
        var periods = compoundsPerYear * years;

        var finalAmount = principal * Math.Pow(1d + ratePerPeriod, periods);
        var interestEarned = finalAmount - principal;

        return new CalculationResult
        {
            Summary =
            [
                CalculatorMath.Numeric("finalAmount", "Amount after interest", finalAmount, "NPR", ValueFormat.Currency),
                CalculatorMath.Numeric("interestEarned", "Interest earned", interestEarned, "NPR", ValueFormat.Currency),
            ],
            Breakdown =
            [
                CalculatorMath.Numeric("principal", "Starting amount", principal, "NPR", ValueFormat.Currency),
                CalculatorMath.Numeric("ratePerPeriod", "Rate per period", ratePerPeriod * 100d, "%", ValueFormat.Percent, 4),
                CalculatorMath.Numeric("periods", "Number of periods", periods, null, ValueFormat.Number, 0),
            ],
            Notes =
            [
                "Interest is added " + ((int)compoundsPerYear) + " times a year.",
                "No tax, fees or inflation adjustment are applied.",
            ],
        };
    }
}
