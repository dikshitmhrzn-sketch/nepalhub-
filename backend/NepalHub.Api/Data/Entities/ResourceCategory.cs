namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The subject a resource belongs to. Persisted as an int rather than a MySQL ENUM, and
/// serialised to the API as its name so the client never has to know the ordinal values.
///
/// An enum rather than a <c>resource_categories</c> table, which is the same choice the
/// calculator and quiz catalogues already make: the set is small, it changes rarely, and
/// keeping it inline means the hot list query needs no join to read a subject filter.
/// <c>General</c> is the first member so a resource that is not subject-specific still has a
/// value, and so the default is never a null column.
/// </summary>
public enum ResourceCategory
{
    General = 0,
    Mathematics = 1,
    Science = 2,
    English = 3,
    Nepali = 4,
    SocialStudies = 5,
    ComputerScience = 6,
    Management = 7,
}
