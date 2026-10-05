namespace NepalHub.Api.Features.Payments;

/// <summary>
/// Where a payment provider will attach in Phase 7f. Nothing implements this yet and nothing
/// calls it: 7e settles paid placements through an admin queue instead, so the interface exists
/// only to mark the seam — the moment a real provider is chosen, its implementation registers
/// here and the queue approval becomes the fallback rather than the mechanism.
///
/// <para>
/// The contract deliberately carries no credentials, keys or merchant identifiers. Those live
/// in configuration bound from user-secrets or the environment at 7f time and never appear in
/// this assembly's types, in an audit row, or in a response — a checkout reference and a
/// provider event are things a log may contain; a secret is not.
/// </para>
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Starts a checkout for one thing being bought and returns where the buyer must be sent,
    /// carrying a reference that both sides will recognise when the provider calls back.
    /// </summary>
    Task<CheckoutHandshake> BeginCheckoutAsync(
        CheckoutIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a provider callback is genuinely signed and reports what it says happened.
    /// Verification comes first and is not optional: the payload is only read after its
    /// signature has proved who sent it, because a callback is the one request in the system
    /// an outsider can forge if it is trusted.
    /// </summary>
    Task<ProviderNotice?> ReadNoticeAsync(
        string body,
        string signature,
        CancellationToken cancellationToken = default);
}

/// <summary>What is being bought, in the currency the price was quoted in.</summary>
public sealed record CheckoutIntent(
    string Reference,
    decimal Amount,
    string Currency,
    string Description);

/// <summary>The provider's answer: a place to send the buyer and a reference to recognise them by.</summary>
public sealed record CheckoutHandshake(string CheckoutId, Uri CheckoutUrl, string Reference);

/// <summary>A verified provider notice about one reference.</summary>
public sealed record ProviderNotice(string Reference, ProviderNoticeKind Kind, string ProviderReference);

public enum ProviderNoticeKind
{
    Succeeded,
    Failed,
    Refunded,
}

/// <summary>
/// The settings 7f will bind its provider from. The section exists in configuration shape only
/// — no placeholder keys are committed, because an appsettings entry that looks like a
/// credential is how one ends up in a diff.
/// </summary>
public sealed class PaymentSettings
{
    public const string SectionName = "Payments";

    /// <summary>Which provider is active. Nothing until 7f chooses one.</summary>
    public string Provider { get; set; } = "none";

    /// <summary>Whether a provider has actually been configured, for a feature to check before offering checkout.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Provider) && !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase);
}
