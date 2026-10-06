namespace NepalHub.Api.Data.Entities;

/// <summary>Where a registration sits in the verification queue staff work through.</summary>
public enum BusinessVerificationStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// An owner's request that staff check the listing they registered themselves.
///
/// <para>
/// Phase 6c closes the other half of the directory's story: before it, only staff could
/// create a row, so an owner appeared by claiming something that already existed. Now an
/// owner can register a draft, and this row is how they ask for it to become public.
/// Publishing stays a staff decision — the request grants nothing, exactly like a claim.
/// </para>
///
/// <para>
/// No unique index on business alone: a rejected owner who corrected their listing should
/// be able to ask again, and a database that refused the second row would give no reason
/// at all. The one-open-request rule is checked in the service, where the refusal can be
/// named to the person.
/// </para>
/// </summary>
public sealed class BusinessVerificationRequest
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Business? Business { get; set; }

    /// <summary>The owner who asked, recorded even if they lose the account later.</summary>
    public Guid SubmittedByUserId { get; set; }

    public ApplicationUser? SubmittedBy { get; set; }

    /// <summary>The owner's own words about the listing, capped at 500.</summary>
    public string? Note { get; set; }

    public BusinessVerificationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedBy { get; set; }

    /// <summary>The instant staff decided, in UTC like every other written timestamp.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What staff recorded when deciding, for whoever reads the request later.</summary>
    public string? ReviewNote { get; set; }
}
