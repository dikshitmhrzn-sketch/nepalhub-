namespace NepalHub.Api.Data.Entities;

/// <summary>
/// Which stage of education a resource is aimed at. Persisted as an int rather than a MySQL
/// ENUM, and serialised to the API as its name.
///
/// This is the axis a Nepali student actually navigates by, which is why it is a filter of
/// its own rather than part of the subject. The school bands match the national structure:
/// Grades 6 to 8, then the Grade 9-10 Secondary School Examination, then Grades 11-12, then
/// the two post-secondary stages. <c>General</c> covers material that is not level-specific,
/// and is the default so the column is never null.
/// </summary>
public enum EducationLevel
{
    General = 0,
    Grade6to8 = 1,
    Grade9to10 = 2,
    Grade11to12 = 3,
    PlusTwo = 4,
    Bachelors = 5,
    Competitive = 6,
}
