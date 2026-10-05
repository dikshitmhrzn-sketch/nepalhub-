using System.ComponentModel.DataAnnotations;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>Optional request-status parsing for the admin queue, same contract as the claim queue's.</summary>
internal static class SubscriptionFilters
{
    internal static bool TryParseStatus(string? raw, out SubscriptionRequestStatus? status, out string field)
    {
        field = "status";
        status = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<SubscriptionRequestStatus>(trimmed, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            status = parsed;

            return true;
        }

        return false;
    }
}

/// <summary>
/// One plan as the public catalogue shows it. The price is in whole rupees and is a catalogue
/// figure only: nothing is charged here, and the number exists so the same row can price a
/// future checkout.
/// </summary>
public sealed record PlanResponse(
    Guid Id,
    PlanTier Tier,
    string Name,
    string? Description,
    int PeriodDays,
    int PriceNpr);

/// <summary>
/// One business's standing with the plans, for the owner's screen and for the admin screens
/// alike. Everything on it is derived at read time — the live window is computed against
/// today's Nepal date rather than stored — so an expired slot reports itself as expired
/// without any job having run.
/// <para>
/// <see cref="PendingRequestId"/> is the owner's request still in the queue, if there is one.
/// </para>
/// </summary>
public sealed record SubscriptionStateResponse(
    Guid BusinessId,
    string Slug,
    PlanTier? Tier,
    string? PlanName,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    bool IsActive,
    Guid? PendingRequestId);

/// <summary>An owner asking for a plan: the free plan is granted on the spot, featured awaits staff.</summary>
public sealed class CreateSubscriptionRequest
{
    [EnumDataType(typeof(PlanTier), ErrorMessage = "Choose a valid plan.")]
    public PlanTier Tier { get; init; }

    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? Note { get; init; }
}

/// <summary>What staff grant from the business screen: which listing, which tier.</summary>
public sealed class GrantSubscriptionRequest
{
    public Guid BusinessId { get; init; }

    [EnumDataType(typeof(PlanTier), ErrorMessage = "Choose a valid plan.")]
    public PlanTier Tier { get; init; }
}

/// <summary>The note staff attach when deciding a request. Shared by approve and reject.</summary>
public sealed class CloseSubscriptionRequestRequest
{
    [StringLength(500, ErrorMessage = "The note must be 500 characters or fewer.")]
    public string? ReviewNote { get; init; }
}

/// <summary>One row of the admin queue: which listing, who asked, and where it stands.</summary>
public sealed record AdminSubscriptionRequestResponse(
    Guid Id,
    Guid BusinessId,
    string Slug,
    string BusinessName,
    PlanTier Tier,
    string RequesterName,
    string? RequesterEmail,
    string? Note,
    SubscriptionRequestStatus Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);
