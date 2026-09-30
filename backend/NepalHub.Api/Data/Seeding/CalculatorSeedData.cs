using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Calculators;

namespace NepalHub.Api.Data.Seeding;

/// <summary>
/// The calculators the platform ships with. This is editable content rather than
/// reference data, so it is applied at start-up instead of being baked into a
/// migration: a staff edit must not be undone by the next migration's diff.
/// </summary>
internal static class CalculatorSeedData
{
    internal static IReadOnlyList<CalculatorDefinition> All =>
    [
        new(
            "loan-emi",
            "Loan EMI calculator",
            "Work out the monthly instalment, total interest and total repayable on a loan.",
            """
            Enter the loan amount, the annual interest rate and the term. The result is the fixed
            monthly instalment, what you pay in interest over the life of the loan, and the total
            you repay. The rate is treated as nominal and reduced to a monthly rate, which is how
            most Nepali lenders quote a reducing-balance loan.
            """,
            CalculatorCategory.Finance,
            10,
            [
                Field("principal", "Loan amount", InputKind.Amount, "NPR", 10_000m, 100_000_000m, 10_000m, 2_000_000m),
                Field("annualRate", "Annual interest rate", InputKind.Percent, "%", 0m, 100m, 0.05m, 10.5m,
                    "A nominal yearly rate, not an effective one."),
                Field("years", "Loan term", InputKind.Years, "years", 1m, 40m, 1m, 20m),
            ]),
        new(
            "compound-interest",
            "Compound interest calculator",
            "See what a deposit grows to when the interest is added back into the balance.",
            """
            Enter the starting amount, the annual rate, how long you leave it, and how often the
            interest is added. A higher compounding frequency always grows faster for the same
            nominal rate, which this shows side by side with the interest earned.
            """,
            CalculatorCategory.Finance,
            20,
            [
                Field("principal", "Starting amount", InputKind.Amount, "NPR", 100m, 100_000_000m, 1_000m, 100_000m),
                Field("annualRate", "Annual interest rate", InputKind.Percent, "%", 0m, 100m, 0.05m, 8m),
                Field("years", "Time period", InputKind.Years, "years", 1m, 50m, 1m, 10m),
                Field("compoundsPerYear", "Compounds per year", InputKind.Number, "times", 1m, 12m, 1m, 4m,
                    "For example 4 for quarterly, or 12 for monthly."),
            ]),
        new(
            "percentage-calculator",
            "Percentage calculator",
            "Find what a given percentage of a value comes to.",
            """
            Enter any value and a percentage. The result is that share of the value, with the
            remainder shown underneath. Useful for a discount, a tip, a share of a bill, or a
            exam mark out of a total.
            """,
            CalculatorCategory.Everyday,
            30,
            [
                Field("value", "Value", InputKind.Number, null, 0m, 1_000_000_000m, 1m, 1_000m),
                Field("percent", "Percentage", InputKind.Percent, "%", 0m, 1_000m, 0.5m, 15m),
            ]),
    ];

    private static InputFieldDefinition Field(
        string key,
        string label,
        InputKind kind,
        string? unit,
        decimal minimum,
        decimal maximum,
        decimal step,
        decimal defaultValue,
        string? help = null) =>
        new()
        {
            Key = key,
            Label = label,
            Kind = kind,
            Unit = unit,
            Minimum = minimum,
            Maximum = maximum,
            Step = step,
            DefaultValue = defaultValue,
            Required = true,
            Help = help,
        };
}

internal sealed record CalculatorDefinition(
    string Slug,
    string Name,
    string Summary,
    string Description,
    CalculatorCategory Category,
    int SortOrder,
    IReadOnlyList<InputFieldDefinition> Inputs);
