namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One time-limited offer from a business. A single table, because every offer is searched,
/// filtered, paged and expired identically; a table per kind of discount would mean a copy of every
/// query and a client that has to guess which one to ask.
///
/// <para>
/// This phase is staff-published copy only. There is no coupon code table, no redemption tracking
/// and no payment: a deal is a description of an offer somebody else honours, and Nepal Hub's job is
/// to say it accurately and then get out of the way.
/// </para>
/// </summary>
public sealed class Deal
{
    public Guid Id { get; set; }

    /// <summary>
    /// The public URL segment. Lowercase words joined by single hyphens and unique. It may be
    /// corrected while the row is a draft, and is frozen once published, because after that the slug
    /// is a live address people and search engines have recorded.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// One line for the listing cards. The description is the full text, and carrying that on every
    /// row of a page of offers would make the list several times larger for content the reader has
    /// not asked for.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// The offer in full, stored and rendered as plain text. It is never treated as markup: the
    /// frontend renders it as text so a staff member's angle brackets cannot become elements.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The offer itself, in whatever words the business actually uses.
    /// <para>
    /// Text and not a number, which is the decision that matters most in this table. A percentage is
    /// the easiest thing to store and the least honest: real offers here are "free chiya with any
    /// thakali set" and "two samosa for the price of one", and a column set up for a percentage
    /// would push staff to invent one or leave the offer out. It is also not this module's claim to
    /// make. Nepal Hub is repeating what a business said, not promising it, and a number reads as a
    /// guarantee in a way the business's own words do not.
    /// </para>
    /// </summary>
    public string? DiscountText { get; set; }

    /// <summary>
    /// The business this offer belongs to, when it belongs to one. Nullable and not required to be
    /// published, because most offers exist before the business is listed here.
    /// <para>
    /// Set null on delete rather than cascading: a deal is public copy that a reader may already have
    /// saved, so losing the business it names must not silently delete the offer as well. The name is
    /// denormalised into the response for exactly this case, so a deal whose business is retracted
    /// keeps working and says whose offer it was.
    /// </para>
    /// <para>
    /// Deliberately not validated against the business being published. Drafting a deal against a
    /// draft business is normal, and requiring a published one would block the only order in which
    /// staff can work: write the business, then write its offers.
    /// </para>
    /// </summary>
    public Guid? BusinessId { get; set; }

    public Business? Business { get; set; }

    /// <summary>
    /// Where the offer applies, as a district. The province is read through the district rather than
    /// stored beside it, because two columns naming the same place can disagree and a filter that
    /// contradicts its own row is worse than one fewer column.
    /// <para>
    /// Nullable because a national offer genuinely has no district. That is the case which separates
    /// a deal from a business listing, where a district is required.
    /// </para>
    /// </summary>
    public Guid? DistrictId { get; set; }

    public District? District { get; set; }

    /// <summary>
    /// The first day the offer can be used, on the Nepal calendar. A <see cref="DateOnly"/> rather
    /// than a timestamp because nobody cares that a lunch discount began at one second past midnight.
    /// </summary>
    public DateOnly StartsOn { get; set; }

    /// <summary>
    /// The last day the offer can be used, inclusive, on the Nepal calendar. Required rather than
    /// nullable: an offer with no end is an offer that never leaves the page, and expiring offers is
    /// the one thing this module exists to get right. Compared against the current Nepal date, so a
    /// deal ends at the same moment for every reader in the country.
    /// </summary>
    public DateOnly EndsOn { get; set; }

    /// <summary>
    /// Where to redeem, when the business wants readers somewhere specific. Always an absolute https
    /// URL with a real host; shape-checked on the way in and never fetched by the server, so a deal
    /// cannot be used to make it call out to anything.
    /// </summary>
    public string? RedemptionUrl { get; set; }

    /// <summary>
    /// Whether readers can see this row. Retracting an offer is always allowed; deleting a published
    /// one is not, so the record of what was public is not simply lost.
    /// </summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
