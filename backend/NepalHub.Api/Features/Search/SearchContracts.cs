using System.Globalization;
using System.Text;

namespace NepalHub.Api.Features.Search;

/// <summary>
/// Which part of the site a search covers. <see cref="All"/> is a filter value rather than a result
/// module: no result is ever tagged with it.
/// </summary>
public enum SearchModule
{
    All = 0,
    Students = 1,
    Jobs = 2,
    Businesses = 3,
    Deals = 4,
    Events = 5,
    Calculators = 6,
}

/// <summary>
/// One hit.
///
/// <para>
/// Note what is not here: no URL path. The client owns its own routes, and a Flutter app in a later
/// phase will have different ones, so the server publishes the module and the slug and lets the
/// client build the address. A server-emitted path is a coupling that breaks the first time either
/// side moves a page.
/// </para>
///
/// <para>
/// Most fields are null for most rows, because six modules do not share a shape. A deal has dates
/// and no employer; a job has an employer and no dates. Rather than flatten them into one string the
/// client cannot interpret, each module fills in the fields it has and leaves the rest null, and the
/// client reads the ones its module knows. The date fields are named separately for the same reason:
/// an offer's <see cref="EndsOn"/> and a resource's <see cref="ExpiresOn"/> mean different things
/// and are not the same column.
/// </para>
///
/// <para>
/// Nothing in this record is private. Every field is already on the item's own public page, which is
/// the whole privacy argument for search: a result row is a summary of a page the reader is allowed
/// to open, not a new surface.
/// </para>
/// </summary>
public sealed record SearchResultItem(
    SearchModule Module,
    string ReferenceId,
    string Slug,
    string Title,
    string? Summary,
    string? Category,
    string? Organisation,
    string? WorkMode,
    string? Provider,
    string? DiscountText,
    string? Organiser,
    string? Venue,
    string? Place,
    bool IsVerified,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    DateOnly? ApplicationDeadline,
    DateOnly? ExpiresOn,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>Every hit from one module, in relevance order.</summary>
public sealed record SearchGroupResponse(
    SearchModule Module,
    string Label,
    int TotalCount,
    IReadOnlyList<SearchResultItem> Items);

/// <summary>
/// A page of results, grouped by module.
///
/// <para>
/// <see cref="Truncated"/> is the honest part of this contract. Each module contributes at most
/// <see cref="SearchLimits.CandidateCap"/> candidates, so a term matching thousands of rows reports
/// the strongest matches rather than pretending to have counted them all. A client that renders
/// "4 matches" when four were shown is telling the truth; one that renders "4 results in Nepal Hub"
/// is making a claim the server cannot support.
/// </para>
/// </summary>
public sealed record SearchResponse(
    string Query,
    int Page,
    int PageSize,
    int TotalCount,
    bool Truncated,
    IReadOnlyList<SearchGroupResponse> Groups)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// The ceilings, in one place, so the contract and the queries cannot disagree about them.
///
/// <para>
/// The page size is capped lower than the shared <see cref="NepalHub.Api.Common.PageRequest"/> cap
/// because a global search runs six queries rather than one, and the difference between 30 and 100
/// rows is four extra queries' worth of work for no reader who would scroll it.
/// </para>
/// </summary>
public static class SearchLimits
{
    public const int MinQueryLength = 2;

    public const int MaxQueryLength = 100;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 30;

    /// <summary>
    /// Rows taken from each module before ranking. This is the bound that keeps one request from
    /// walking a whole catalogue, and it is why <c>truncated</c> exists.
    /// </summary>
    public const int CandidateCap = 50;
}

/// <summary>
/// Normalising a term so the in-memory ranking agrees with the database filter.
///
/// <para>
/// MySQL matches on <c>utf8mb4_0900_ai_ci</c>, which is case-insensitive and accent-insensitive, so
/// a reader searching <c>CAFÉ</c> is meant to find <c>cafe</c>. If the ranking then compared the
/// same strings with <c>Ordinal</c>, a result the database returned could be scored as a non-match
/// and pushed off the page — the search would look broken in a way that is very hard to reproduce.
/// Both sides go through here instead.
/// </para>
/// </summary>
public static class SearchText
{
    /// <summary>
    /// Compatibility normalisation, stripped of diacritics and lowercased, so the ranking comparison
    /// and the <c>LIKE</c> pattern are measuring the same thing.
    /// </summary>
    public static string Normalise(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            // The marks are dropped and the base letter kept, which turns "é" into "e" without
            // leaving an empty string behind for text that is nothing but marks.
            if (char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Trims the term, collapses runs of whitespace to a single space, and strips control characters
    /// that would otherwise travel through the URL and the log line.
    ///
    /// A term of only whitespace or only control characters reduces to the empty string, which the
    /// controller answers as a missing query rather than searching for nothing.
    /// </summary>
    public static string Prepare(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return string.Empty;
        }

        var collapsed = new StringBuilder(term.Length);
        var pendingSpace = false;

        foreach (var character in term.Normalize(NormalizationForm.FormKC))
        {
            // A control character is dropped rather than escaped, and it takes any whitespace run
            // before it with it, so "\n\t " cannot masquerade as a term with leading spaces.
            if (char.IsControl(character))
            {
                pendingSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                collapsed.Append(' ');
                pendingSpace = false;
            }

            collapsed.Append(character);
        }

        return collapsed.ToString();
    }

    /// <summary>Whether a candidate field matches the prepared term.</summary>
    public static bool Contains(string? haystack, string normalisedTerm) =>
        !string.IsNullOrEmpty(haystack) && Normalise(haystack).Contains(normalisedTerm, StringComparison.Ordinal);
}