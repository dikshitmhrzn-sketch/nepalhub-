namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The field of work a posting belongs to. Persisted as an int rather than a MySQL ENUM, and
/// serialised to the API as its name so the client never has to know the ordinal values.
///
/// An enum rather than a <c>job_categories</c> table, for the same reason the calculator and
/// quiz catalogues make the same choice: the set is small, it changes rarely, and keeping it
/// inline means the hot job list query needs no join to read a category filter. The list is
/// pitched at the Nepali labour market a graduate is actually searching — the sectors that
/// hire in volume, plus <c>Other</c> as the catch-all so a posting is never blocked for
/// lacking a category that does not exist yet.
/// </summary>
public enum JobCategory
{
    InformationTechnology = 0,
    Teaching = 1,
    Healthcare = 2,
    Administration = 3,
    Finance = 4,
    Sales = 5,
    Engineering = 6,
    Construction = 7,
    Hospitality = 8,
    Agriculture = 9,
    Driving = 10,
    Security = 11,
    Government = 12,
    NGO = 13,
    Other = 14,
}
