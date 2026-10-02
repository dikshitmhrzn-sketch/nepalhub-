namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The kind of event a listing describes. Persisted as an int rather than a MySQL ENUM, and
/// serialised to the API as its name so the client never has to know the ordinal values.
///
/// An enum rather than an <c>event_categories</c> table, for the same reason the job, calculator,
/// quiz and business catalogues all make the same choice: the set is small, it changes rarely, and
/// keeping it inline means the public list query needs no join to read a category filter.
///
/// The list is pitched at what someone in a Nepali city actually looks for on a weekend, plus
/// <c>Other</c> as the catch-all so an event is never blocked for lacking a category that does not
/// exist yet.
/// </summary>
public enum EventCategory
{
    Community = 0,
    Cultural = 1,
    Sports = 2,
    Education = 3,
    Religious = 4,
    Business = 5,
    Exhibition = 6,
    Other = 7,
}
