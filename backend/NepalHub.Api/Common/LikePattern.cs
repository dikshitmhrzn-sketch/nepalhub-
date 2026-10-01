namespace NepalHub.Api.Common;

/// <summary>
/// Builds the pattern for a keyword <c>LIKE</c> search.
///
/// MySQL treats <c>%</c> and <c>_</c> as wildcards inside a <c>LIKE</c> pattern, and the
/// backslash as the default escape character. A search term is visitor input, so a term of
/// <c>%</c> would otherwise turn into a pattern that matches every row in the table — the
/// opposite of what was asked for, and a cheap way to walk the whole catalogue one page at a
/// time.
///
/// The escape character is passed explicitly to <c>EF.Functions.Like</c> rather than relying
/// on the server default, because a database configured with <c>NO_BACKSLASH_ESCAPES</c>
/// would otherwise interpret the inserted backslashes as ordinary characters and search for
/// the wrong thing.
/// </summary>
public static class LikePattern
{
    /// <summary>
    /// The escape character this module escapes with. Must be the same single character
    /// passed as the third argument to <c>EF.Functions.Like</c>.
    /// </summary>
    public const string Escape = "\\";

    /// <summary>
    /// Wraps <paramref name="term"/> in wildcards and escapes the wildcards it contains, so
    /// the result matches any field containing that exact text.
    /// </summary>
    public static string Contains(string term) => $"%{EscapeTerm(term)}%";

    /// <summary>
    /// Escapes the <c>LIKE</c> metacharacters in a term. Order matters: the backslash is
    /// doubled first, otherwise the backslashes added for <c>%</c> and <c>_</c> would then be
    /// escaped a second time.
    /// </summary>
    private static string EscapeTerm(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
