namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Catalogue grouping for a calculator. Persisted as an int rather than a MySQL
/// ENUM, and serialised to the API as its name so the client never has to know the
/// ordinal values.
/// </summary>
public enum CalculatorCategory
{
    Finance = 0,
    Education = 1,
    Health = 2,
    Engineering = 3,
    Everyday = 4,
}
