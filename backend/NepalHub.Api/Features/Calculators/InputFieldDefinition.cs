using System.Text.Json;
using System.Text.Json.Serialization;

namespace NepalHub.Api.Features.Calculators;

/// <summary>
/// One numeric input on a calculator. The whole form is described by an array of
/// these, stored as JSON in <c>calculators.inputs_json</c> and rendered by the
/// frontend, so publishing another calculator requires no frontend code.
/// </summary>
public sealed record InputFieldDefinition
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public InputKind Kind { get; init; }

    public string? Unit { get; init; }

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }

    public decimal? Step { get; init; }

    public decimal? DefaultValue { get; init; }

    public bool Required { get; init; } = true;

    public string? Help { get; init; }
}

/// <summary>Drives the unit suffix and the input step on the rendered field.</summary>
public enum InputKind
{
    Amount = 0,
    Percent = 1,
    Years = 2,
    Number = 3,
}

/// <summary>
/// Round-trips <see cref="InputFieldDefinition"/> between the JSON column and the API.
/// The same options back both directions so what an admin stores is exactly what a
/// client receives. A malformed column degrades to an empty schema rather than
/// throwing, because the column is staff-writable.
/// </summary>
internal static class CalculatorInputSchema
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string Serialize(IReadOnlyList<InputFieldDefinition> fields) =>
        JsonSerializer.Serialize(fields, SerializerOptions);

    internal static IReadOnlyList<InputFieldDefinition> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<InputFieldDefinition[]>(json, SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
