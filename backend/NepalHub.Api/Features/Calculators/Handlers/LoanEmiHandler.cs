namespace NepalHub.Api.Features.Calculators.Handlers;

/// <summary>
/// Equated monthly instalment for an amortising loan:
/// <c>E = P x r x (1 + r)^n / ((1 + r)^n - 1)</c> where <c>r</c> is the monthly
/// rate and <c>n</c> the number of monthly payments.
/// </summary>
internal sealed class LoanEmiHandler : ICalculatorHandler
{
    public string Slug => "loan-emi";

    public CalculationResult Calculate(IReadOnlyDictionary<string, decimal> inputs)
    {
        var principal = (double)inputs["principal"];
        var annualRate = (double)inputs["annualRate"];
        var years = (double)inputs["years"];

        var months = years * 12d;
        var monthlyRate = annualRate / 1200d;

        double payment;
        if (monthlyRate <= 0d)
        {
            payment = principal / months;
        }
        else
        {
            var growth = Math.Pow(1d + monthlyRate, months);
            payment = principal * monthlyRate * growth / (growth - 1d);
        }

        var totalPayable = payment * months;
        var totalInterest = totalPayable - principal;

        return new CalculationResult
        {
            Summary =
            [
                CalculatorMath.Numeric("monthlyPayment", "Monthly payment", payment, "NPR", ValueFormat.Currency),
                CalculatorMath.Numeric("totalInterest", "Total interest", totalInterest, "NPR", ValueFormat.Currency),
                CalculatorMath.Numeric("totalPayable", "Total payable", totalPayable, "NPR", ValueFormat.Currency),
            ],
            Breakdown =
            [
                CalculatorMath.Numeric("amount", "Loan amount", principal, "NPR", ValueFormat.Currency),
                CalculatorMath.Numeric("months", "Number of payments", months, "months", ValueFormat.Number, 0),
                CalculatorMath.Numeric(
                    "monthlyRate", "Monthly interest rate", monthlyRate * 100d, "%", ValueFormat.Percent, 4),
            ],
            Notes =
            [
                "The annual rate is treated as nominal and reduced to a monthly rate.",
                "Service fees, insurance and prepayment charges are not included.",
            ],
        };
    }
}
