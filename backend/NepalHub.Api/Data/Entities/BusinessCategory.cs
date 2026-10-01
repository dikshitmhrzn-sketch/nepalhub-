namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The kind of business a listing belongs to. Persisted as an int rather than a MySQL ENUM, and
/// serialised to the API as its name so the client never has to know the ordinal values.
///
/// An enum rather than a <c>business_categories</c> table, for the same reason the job, calculator
/// and quiz catalogues all make the same choice: the set is small, it changes rarely, and keeping
/// it inline means the hot directory query needs no join to read a category filter. An earlier
/// draft of this plan did commit to a seeded tree via <c>parent_id</c>, which would have bought a
/// subcategory drilldown this phase does not have a filter, a screen or a reader for.
///
/// The list is pitched at what a person in a Nepali city actually looks for when they need
/// something nearby and trustworthy, plus <c>Other</c> as the catch-all so a listing is never
/// blocked for lacking a category that does not exist yet.
/// </summary>
public enum BusinessCategory
{
    Restaurants = 0,
    Cafes = 1,
    HotelsAndAccommodation = 2,
    GymsAndFitness = 3,
    SalonsAndBeauty = 4,
    EducationAndTuition = 5,
    RepairAndMaintenance = 6,
    Shops = 7,
    InformationTechnology = 8,
    ProfessionalServices = 9,
    HealthcareServices = 10,
    Other = 11,
}