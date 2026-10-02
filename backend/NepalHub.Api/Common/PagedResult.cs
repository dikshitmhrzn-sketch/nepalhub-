namespace NepalHub.Api.Common;

/// <summary>
/// The single paged shape every list endpoint returns. Keeping pagination, sorting
/// and filtering on one convention is what lets the later global-search module reuse
/// it instead of inventing its own.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>Normalised paging arguments, with the page size capped.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// The number of pages a set of <paramref name="totalCount"/> rows fills at this page size.
    /// An empty set fills no pages, which is reported as one so that a page number can always be
    /// clamped to something a caller can actually ask for.
    /// </summary>
    public static int TotalPagesFor(int totalCount, int pageSize)
    {
        if (totalCount <= 0 || pageSize <= 0)
        {
            return 1;
        }

        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>
    /// Returns the page to actually read, once the total is known.
    /// <para>
    /// <see cref="Normalise"/> can only reject arguments that are wrong on their own terms. Whether a
    /// page is past the end is not knowable until the rows have been counted, so it is settled here,
    /// between the count query and the <c>Skip</c>. A request for page 999 of a two-page list is
    /// answered with the second page rather than with nothing, because an empty page is a worse
    /// answer than the one the reader was obviously trying to reach — and echoing 999 back next to a
    /// <c>TotalPages</c> of 1 produces a response that contradicts itself.
    /// </para>
    /// <para>
    /// A result set with no rows has no last page to fall back to, so an empty list is page 1. That
    /// keeps <c>Page</c> at 1 in the only case where there is nothing to read, and keeps
    /// <c>HasPrevious</c> false there.
    /// </para>
    /// </summary>
    public PageRequest ClampTo(int totalCount)
    {
        var lastPage = TotalPagesFor(totalCount, PageSize);

        return Page > lastPage ? this with { Page = lastPage } : this;
    }

    /// <summary>
    /// Clamps whatever the caller sent. A page below 1 becomes the first page, and a
    /// page size above <see cref="MaxPageSize"/> is reduced rather than rejected, so a
    /// client cannot ask the database for an unbounded result set.
    /// </summary>
    public static PageRequest Normalise(int? page, int? pageSize)
    {
        var normalisedPage = page is null || page < 1 ? 1 : page.Value;
        var requested = pageSize is null || pageSize < 1 ? DefaultPageSize : pageSize.Value;

        return new PageRequest(normalisedPage, Math.Min(requested, MaxPageSize));
    }
}
