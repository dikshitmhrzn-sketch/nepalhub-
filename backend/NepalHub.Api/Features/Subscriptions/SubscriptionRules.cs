using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Subscriptions;

/// <summary>
/// The rules every subscription path shares, written once so the owner's screen, the admin
/// grant, the request approval and the public featured strip cannot drift apart on what
/// "featured right now" means or on how long a purchase lasts.
/// </summary>
internal static class SubscriptionRules
{
    /// <summary>
    /// Whether a subscription counts today. Derived from the dates against the Nepal date on
    /// every read and never stored, so a slot expires the moment its last day passes in Nepal
    /// without any job having run, and a clock in another timezone cannot disagree about it.
    /// </summary>
    internal static bool IsLive(BusinessSubscription subscription, DateOnly today) =>
        subscription.Status == BusinessSubscriptionStatus.Active
        && today >= subscription.StartsOn
        && today <= subscription.EndsOn;

    /// <summary>
    /// The subset of businesses the paid strip may show: live *and* on the featured tier, for
    /// the directory's strip. Tier matters as much as liveness — a free-plan listing is live
    /// but has not bought the placement, and listing it as featured would be giving away the
    /// one thing the paid plan sells. The predicate is the same one <see cref="IsLive"/>
    /// applies plus the tier, expressed in SQL so the strip is one query rather than every
    /// subscription loaded into memory.
    /// </summary>
    internal static IQueryable<Guid> LiveBusinessIds(IQueryable<BusinessSubscription> subscriptions, DateOnly today) =>
        subscriptions
            .Where(subscription => subscription.Status == BusinessSubscriptionStatus.Active
                && subscription.Plan!.Tier == PlanTier.Featured
                && subscription.StartsOn <= today
                && subscription.EndsOn >= today)
            .Select(subscription => subscription.BusinessId);

    /// <summary>
    /// The end date after a purchase: the current window rolled forward by the plan's period,
    /// where a window that has already run out rolls from today rather than from the day it
    /// ended. Extending a live slot therefore adds the full period on top of what is left,
    /// and reviving an expired one starts the period from now — the same arithmetic for a
    /// direct grant, an owner's request and an approved request, because three copies of this
    /// would eventually disagree.
    /// </summary>
    internal static DateOnly ExtendTo(DateOnly? currentEnd, DateOnly today, int periodDays) =>
        (currentEnd is { } end && end > today ? end : today).AddDays(periodDays);
}
