namespace NepalHub.Api.Data.Entities;

/// <summary>
/// The subject areas a question can belong to. It is an enum rather than a table because
/// the set is small, fixed and referenced by the API contract, and because the
/// <c>CalculatorCategory</c> enum already set the precedent for this module family.
/// A new category is a migration, which is the right amount of ceremony for content
/// that changes on the order of once a year.
/// </summary>
public enum QuizCategory
{
    Nepal = 0,
    GeneralKnowledge = 1,
    Science = 2,
    Technology = 3,
    History = 4,
    Geography = 5,
}
