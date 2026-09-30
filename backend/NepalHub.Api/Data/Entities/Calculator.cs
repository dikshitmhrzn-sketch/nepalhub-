namespace NepalHub.Api.Data.Entities;

public sealed class Calculator
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public CalculatorCategory Category { get; set; }

    /// <summary>
    /// A serialised <c>InputFieldDefinition[]</c>. The whole input form is described
    /// by this column, so publishing another calculator needs no frontend code.
    /// </summary>
    public string InputsJson { get; set; } = "[]";

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
