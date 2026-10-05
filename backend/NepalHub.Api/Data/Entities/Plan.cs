namespace NepalHub.Api.Data.Entities;

/// <summary>
/// What a listing buys. Two members for now — free and featured — because a third tier
/// would need a third thing to display before it could be worth buying.
/// </summary>
public enum PlanTier
{
    Free,
    Featured,
}

/// <summary>
/// One purchasable subscription plan, seeded once and edited by staff rather than
/// hard-coded, so the price and the length of a featured slot can change without a
/// deployment.
///
/// <para>
/// <see cref="PriceNpr"/> is in whole rupees and is zero for the free plan. It is a
/// catalogue price: no money moves in this phase, and the number exists so an
/// admin screen can show it and a future checkout can read the same row instead of
/// inventing its own.
/// </para>
/// </summary>
public sealed class Plan
{
    public Guid Id { get; set; }

    public PlanTier Tier { get; set; }

    /// <summary>The display name, e.g. "Free" or "Featured".</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>How many days one purchase of this plan lasts.</summary>
    public int PeriodDays { get; set; }

    public int PriceNpr { get; set; }

    /// <summary>
    /// Whether staff may still grant it. Plans are never deleted: past subscriptions
    /// point at them, and a row that vanished would take its history with it.
    /// </summary>
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
