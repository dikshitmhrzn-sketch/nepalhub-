using System.Security.Cryptography;
using System.Text;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// Where a payment provider attaches. One implementation is registered from configuration
/// (eSewa in 7f); nothing else in the assembly knows which provider is live, so swapping
/// it is a registration change rather than a rewrite of checkout or settlement.
///
/// <para>
/// The contract deliberately carries no credentials, keys or merchant identifiers. Those
/// live in configuration bound from user-secrets or the environment and never appear in
/// this assembly's types, in an audit row, or in a response — a checkout reference and a
/// provider event are things a log may contain; a secret is not.
/// </para>
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Starts a checkout for one thing being bought and returns where the buyer must be
    /// sent, carrying a reference that both sides will recognise when the provider answers
    /// back, and the signed form fields the browser posts to leave this site.
    /// </summary>
    Task<CheckoutHandshake> BeginCheckoutAsync(
        CheckoutIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a provider callback is genuinely signed and reports what it says happened.
    /// Verification comes first and is not optional: the payload is only read after its
    /// signature has proved who sent it, because a callback is the one request in the
    /// system an outsider can forge if it is trusted.
    /// </summary>
    Task<ProviderNotice?> ReadNoticeAsync(
        string body,
        string signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider directly what became of one reference, sending the amount we
    /// quoted so a mismatched answer cannot be mistaken for this payment. This — not the
    /// redirect back from the provider's pages — is what settles anything: the browser is
    /// under the buyer's control, a server-to-server status check is not.
    /// </summary>
    Task<StatusReport?> CheckStatusAsync(
        string reference,
        int expectedTotalNpr,
        CancellationToken cancellationToken = default);
}

/// <summary>What is being bought, in the currency the price was quoted in.</summary>
public sealed record CheckoutIntent(
    string Reference,
    int AmountNpr,
    string Description);

/// <summary>
/// The provider's answer: a place to send the buyer, the reference to recognise them by,
/// and the signed fields the browser posts there. The signature is computed here, server-
/// side, so the secret that produced it never leaves this process — the fields themselves
/// are public by design; they travel in the buyer's browser on the way out anyway.
/// </summary>
public sealed record CheckoutHandshake(
    string CheckoutId,
    Uri CheckoutUrl,
    string Reference,
    IReadOnlyDictionary<string, string> Fields);

/// <summary>A verified provider notice about one reference.</summary>
public sealed record ProviderNotice(string Reference, ProviderNoticeKind Kind, string? ProviderReference);

/// <summary>
/// What the provider's own status check said, including the amount it recorded and the
/// reference it thinks it answered about. Both are echoed rather than assumed, so the
/// caller can compare them against the payment it meant to ask about and refuse a reply
/// about some other transaction or a different total.
/// </summary>
public sealed record StatusReport(
    string Reference,
    int TotalNpr,
    ProviderNoticeKind Kind,
    string? ProviderReference);

public enum ProviderNoticeKind
{
    /// <summary>The provider has not finished deciding. Nothing may settle on this alone.</summary>
    Pending,

    Succeeded,
    Failed,
    Refunded,
}

/// <summary>
/// The settings the provider binds from. The section exists in configuration shape only —
/// no placeholder keys are committed, because an appsettings entry that looks like a
/// credential is how one ends up in a diff.
/// </summary>
public sealed class PaymentSettings
{
    public const string SectionName = "Payments";

    /// <summary>Which provider is active. "none" until one is deliberately configured.</summary>
    public string Provider { get; set; } = "none";

    /// <summary>Sandbox or production. Decides which hosts the provider is called on.</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>The merchant code. Not a secret — it is posted to the buyer's browser — but
    /// per-merchant, so it comes from configuration rather than being committed.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// The HMAC key that signs requests and verifies callbacks. Bound from user-secrets or
    /// the environment only; never listed in any committed file and never surfaced.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// The public origin of the frontend, where the provider sends the buyer back. Built
    /// into the success and failure URLs, so it is configuration rather than a request
    /// header the buyer could lie about.
    /// </summary>
    public string ReturnBaseUrl { get; set; } = string.Empty;

    /// <summary>Base URL of the hosted checkout form, by environment.</summary>
    public Uri CheckoutBaseUrl { get; set; } = new("https://rc-epay.esewa.com.np");

    /// <summary>Base URL of the server-to-server status check, by environment.</summary>
    public Uri StatusBaseUrl { get; set; } = new("https://rc.esewa.com.np");

    /// <summary>
    /// How often the background sweep asks the provider about checkouts left Pending for
    /// nobody to come back to. Zero disables the sweep, which is what tests and hosts
    /// without a provider run with.
    /// </summary>
    public int ReconcileIntervalSeconds { get; set; } = 300;

    /// <summary>Whether a provider has actually been configured, for a feature to check before offering checkout.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Provider)
        && !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ProductCode)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(ReturnBaseUrl);
}
