namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One business listing. A single table, because every listing is searched, filtered and paged
/// identically; a table per category would mean a copy of every query and a client that has to
/// guess which one to ask.
///
/// Staff publish the copy, and an owner account is attached afterwards by approving a claim:
/// the row records who a staff member checked it as, and whoever's claim was approved becomes
/// <see cref="OwnerUserId"/>. Null forever on a listing nobody has claimed, which is normal.
/// </summary>
public sealed class Business
{
    public Guid Id { get; set; }

    /// <summary>
    /// The public URL segment. Lowercase words joined by single hyphens and unique. It may be
    /// corrected while the row is a draft, and is frozen once published, because after that the
    /// slug is a live address people and search engines have recorded.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The business name, the way a reader would say it out loud.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What the business does or sells. Stored and rendered as plain text, never as markup: the
    /// frontend renders it as text so a staff member's angle brackets cannot become elements.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public BusinessCategory Category { get; set; }

    /// <summary>
    /// Where the business is, as a district. The province is read through the district rather than
    /// stored beside it, because two columns naming the same place can disagree and a filter that
    /// contradicts its own row is worse than one fewer column.
    /// <para>
    /// Required rather than nullable, which is where this departs from the job board: a remote job
    /// legitimately has no location, but a directory of local businesses is useless without one. A
    /// listing with no district cannot be found by anyone browsing their own area.
    /// </para>
    /// </summary>
    public Guid DistrictId { get; set; }

    public District? District { get; set; }

    /// <summary>The street address, free text. Not geocoded: maps are a later phase.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// The number a reader dials. Required before publishing, and stored as digits only so that
    /// one business cannot appear under two spellings of the same number. Not unique: two shops
    /// on one floor share a landline.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// An optional contact address. There is no mail server behind this product, so nothing here
    /// is verified by sending to it; it is a note that a person may or may not read.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// An optional link to the business's own site. Always an absolute https URL with a real host,
    /// shape-checked on the way in and never fetched by the server.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Whether readers can see this row. Retracting a listing is always allowed; deleting a
    /// published one is not, so the record of what was public is not simply lost.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Whether a staff member has checked this listing.
    /// <para>
    /// Deliberately independent of <see cref="IsPublished"/>, and the two together are why this is
    /// not one combined status column. Publishing requires verification, but unverifying does not
    /// retract, so a listing can be live without its badge when something turns out to be wrong
    /// and someone needs to take responsibility for the correction. Merging the flags would force
    /// an unverified business to be pulled off the directory entirely, which is a worse outcome
    /// than showing it honestly unchecked.
    /// </para>
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// When the listing was last checked, or null while it is unchecked. Stored so the staff list
    /// can show how stale a verified listing has become.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>
    /// Whoever owns the listing, set when staff approve their claim. Nullable and set null on
    /// delete: losing the owner's account must not take the listing, which readers already plan
    /// around, with it.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    public ApplicationUser? OwnerUser { get; set; }

    /// <summary>
    /// At most seven rows, one per weekday. Empty is meaningful and normal: a lot of businesses
    /// publish without stating their hours, and "not listed" is honest where a wrong guess is not.
    /// </summary>
    public ICollection<BusinessOpeningHours> OpeningHours { get; set; } = [];
}