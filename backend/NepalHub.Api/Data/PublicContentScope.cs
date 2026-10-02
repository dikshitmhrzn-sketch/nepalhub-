using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Data;

/// <summary>
/// What a reader is allowed to see, written down once.
///
/// <para>
/// Every module service filters its public list and its public detail through this class, and so
/// does the global search endpoint. That is the whole reason it exists. A visibility rule
/// duplicated in two places is a rule that will eventually be changed in one of them, and the
/// failure is silent: the search would keep returning a row the module page has already stopped
/// serving, or the reverse, and neither would look broken on its own.
/// </para>
///
/// <para>
/// Each predicate below carries the reasoning that belongs to it, moved here with the rule rather
/// than left behind with the caller that first needed it. Changing a rule means changing it here,
/// and every reader of public content changes with it.
/// </para>
/// </summary>
public static class PublicContentScope
{
    /// <summary>
    /// Published resources that have not passed their deadline. A resource with no deadline never
    /// expires, so the comparison is skipped for it rather than treating a null as a date.
    /// </summary>
    public static IQueryable<StudentResource> StudentResources(IQueryable<StudentResource> source, DateOnly today) =>
        source
            .AsNoTracking()
            .Where(resource => resource.IsPublished)
            .Where(resource => resource.ExpiresOn == null || resource.ExpiresOn >= today);

    /// <summary>
    /// A deadline that falls today is still open: the day is not over, and an applicant applying on
    /// the deadline date is not too late. Only a strictly earlier date has closed the posting.
    /// </summary>
    public static IQueryable<Job> Jobs(IQueryable<Job> source, DateOnly today) =>
        source
            .AsNoTracking()
            .Where(job => job.IsPublished)
            .Where(job => job.ApplicationDeadline >= today);

    /// <summary>
    /// Published and nothing else. There is no expiry rule to pair with it: a restaurant does not
    /// stop existing because its deadline passed, so the second predicate the job board needs has no
    /// equivalent here.
    /// </summary>
    public static IQueryable<Business> Businesses(IQueryable<Business> source) =>
        source
            .AsNoTracking()
            .Where(business => business.IsPublished);

    /// <summary>
    /// The end comparison is inclusive because an offer is live through its last day: someone reading
    /// at six in the evening on the final date is exactly the reader this page exists for.
    /// </summary>
    public static IQueryable<Deal> Deals(IQueryable<Deal> source, DateOnly today) =>
        source
            .AsNoTracking()
            .Where(deal => deal.IsPublished && deal.EndsOn >= today);

    /// <summary>
    /// Published and not finished, to the minute, on the Nepal clock. The end comparison is inclusive
    /// so an event is still listed at the moment it ends rather than vanishing mid-sentence in the
    /// middle of a session.
    ///
    /// There is deliberately no start comparison. An event that begins at seven this evening is still
    /// the thing a reader needs to see at four this afternoon, and filtering on the start would put it
    /// past the reader's reach for most of the day it matters.
    /// </summary>
    public static IQueryable<Event> Events(IQueryable<Event> source, DateTime now) =>
        source
            .AsNoTracking()
            .Where(calendarEvent => calendarEvent.IsPublished && calendarEvent.EndsAt >= now);

    /// <summary>
    /// Published calculators, which is the whole rule: a calculator has no expiry, so the second
    /// predicate the job board needs has no equivalent here either.
    /// </summary>
    public static IQueryable<Calculator> Calculators(IQueryable<Calculator> source) =>
        source
            .AsNoTracking()
            .Where(calculator => calculator.IsPublished);
}