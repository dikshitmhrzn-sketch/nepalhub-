using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Calculators;

internal static class CalculatorSlug
{
    /// <summary>Lowercase words separated by single hyphens.</summary>
    internal const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}

public sealed record CalculatorSummaryResponse(
    string Slug,
    string Name,
    string Summary,
    CalculatorCategory Category,
    bool IsPublished);

public sealed record CalculatorDetailResponse(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string Description,
    CalculatorCategory Category,
    IReadOnlyList<InputFieldDefinition> Inputs,
    bool IsPublished,
    int SortOrder,
    DateTime UpdatedAt);

public sealed record CalculationResponse(string Slug, string Name, CalculationResult Result);

/// <summary>
/// One row in the staff list. It deliberately omits the input schema so the list
/// stays a single SQL projection; the edit form fetches the full record by id.
/// </summary>
public sealed record CalculatorAdminSummaryResponse(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    CalculatorCategory Category,
    bool IsPublished,
    int SortOrder,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>The staff view of one row, which also carries the input schema.</summary>
public sealed record CalculatorAdminResponse(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string Description,
    CalculatorCategory Category,
    IReadOnlyList<InputFieldDefinition> Inputs,
    bool IsPublished,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByEmail);

/// <summary>
/// Values arrive as <see cref="JsonElement"/> rather than <c>decimal</c> so this
/// module decides the error key. Binding straight to <c>Dictionary&lt;string,
/// decimal&gt;</c> would put the key in the path ("inputs.principal") and report a
/// framework message instead of one the UI can put next to the right input.
/// </summary>
public sealed class CalculateRequest
{
    public Dictionary<string, JsonElement>? Inputs { get; init; }
}

/// <summary>Create and update share one shape, so the admin form sends the same body.</summary>
public sealed class SaveCalculatorRequest
{
    [Required(ErrorMessage = "A slug is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "The slug must be 2 to 80 characters.")]
    [RegularExpression(
        CalculatorSlug.Pattern,
        ErrorMessage = "Use lowercase words separated by single hyphens, for example loan-emi.")]
    public string Slug { get; init; } = string.Empty;

    [Required(ErrorMessage = "A name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "The name must be 2 to 120 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "A summary is required.")]
    [StringLength(280, MinimumLength = 10, ErrorMessage = "The summary must be 10 to 280 characters.")]
    public string Summary { get; init; } = string.Empty;

    [Required(ErrorMessage = "A description is required.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "The description must be 10 to 4000 characters.")]
    public string Description { get; init; } = string.Empty;

    [EnumDataType(typeof(CalculatorCategory), ErrorMessage = "Choose a valid category.")]
    public CalculatorCategory Category { get; init; }

    [Range(0, 10000, ErrorMessage = "The sort order must be between 0 and 10000.")]
    public int SortOrder { get; init; }

    public bool IsPublished { get; init; }

    public IReadOnlyList<InputFieldDefinition>? Inputs { get; init; }
}
