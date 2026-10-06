using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// What the checkout endpoints take and return. The signed form fields travel back to the
/// browser because that is where they are posted from — the secret that signed them never
/// does, and neither does anything else the provider's pages will need.
/// </summary>
public sealed record CreateCheckoutRequest
{
    [Required]
    public PaymentKind Kind { get; init; }

    /// <summary>The listing or the event being bought for, by the same address the site uses.</summary>
    [Required]
    [StringLength(160)]
    public string Slug { get; init; } = string.Empty;

    /// <summary>Which plan a subscription checkout is for. Free is never a checkout.</summary>
    public PlanTier? Tier { get; init; }

    /// <summary>Places held by one ticket checkout. Mirrors the booking rule of one to four.</summary>
    [Range(1, 4)]
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// Where to send the buyer. The url and the fields are present together or not at all:
/// a reference with no form is a payment that has already finished, which the buyer is
/// told rather than sent somewhere to pay again.
/// </summary>
public sealed record CheckoutResponse(
    string Reference,
    string? CheckoutUrl,
    IReadOnlyDictionary<string, string>? Fields,
    PaymentStatus Status);

/// <summary>
/// What the return page and the "check again" button send back. The signed payload from
/// the provider is optional: a buyer who closed the tab and returned later has only the
/// reference, and the status check answers just the same without it.
/// </summary>
public sealed record VerifyPaymentRequest
{
    [Required]
    [StringLength(64)]
    public string Reference { get; init; } = string.Empty;

    /// <summary>The Base64 payload the provider appended to the return URL, when there is one.</summary>
    [StringLength(16000)]
    public string? Data { get; init; }

    [StringLength(256)]
    public string? Signature { get; init; }
}

/// <summary>One payment as the buyer or an administrator reads it.</summary>
public sealed record PaymentResponse(
    string Reference,
    PaymentKind Kind,
    PaymentStatus Status,
    int AmountNpr,
    string Description,
    string? FailureCode,
    string? ProviderReference,
    DateTime CreatedAt,
    DateTime? SettledAt,
    DateTime? RefundedAt);

/// <summary>One row of the buyer's own history or the admin list.</summary>
public sealed record PaymentSummaryResponse(
    string Reference,
    PaymentKind Kind,
    PaymentStatus Status,
    int AmountNpr,
    string Description,
    string? FailureCode,
    DateTime CreatedAt,
    DateTime? SettledAt,
    DateTime? RefundedAt);

/// <summary>The admin list carries the buyer too: a refund question is always about a person.</summary>
public sealed record AdminPaymentResponse(
    string Reference,
    PaymentKind Kind,
    PaymentStatus Status,
    int AmountNpr,
    string Description,
    string? FailureCode,
    DateTime CreatedAt,
    DateTime? SettledAt,
    DateTime? RefundedAt,
    string BuyerName,
    string? BuyerEmail);

/// <summary>
/// Whether checkout is offered at all. Only this and the provider's name cross the wire —
/// never a merchant code, a url the provider keeps private, or anything else configured.
/// </summary>
public sealed record PaymentConfigResponse(bool Available, string Provider);

/// <summary>Filters the admin list by what a payment is waiting for. Same contract as the queue's.</summary>
public static class PaymentFilters
{
    public const string All = "all";

    public static bool TryParseStatus(string? raw, out PaymentStatus? status, out string field)
    {
        field = "status";
        status = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Equals(All, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<PaymentStatus>(trimmed, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }
}
