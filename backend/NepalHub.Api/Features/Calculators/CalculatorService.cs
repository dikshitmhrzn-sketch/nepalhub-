using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Calculators;

/// <summary>Why a calculation did not produce numbers, so the controller can pick a status.</summary>
public enum CalculationStatus
{
    Success,
    NotFound,
    Unavailable,
    Invalid,
    Failed,
}

public sealed record CalculationOutcome(
    CalculationStatus Status,
    CalculationResponse? Response = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

public enum SaveStatus
{
    Created,
    Updated,
    NotFound,
    SlugTaken,
    SlugImmutable,
    Unsupported,
    Invalid,
}

public sealed record SaveOutcome(
    SaveStatus Status,
    CalculatorDetailResponse? Calculator = null,
    IReadOnlyList<FieldError>? FieldErrors = null,
    string? Message = null);

public enum DeleteStatus
{
    Deleted,
    NotFound,
    Published,
}

/// <summary>
/// All calculator business rules: paging and search, input validation against the
/// stored field definitions, handler dispatch, and the staff write paths. Controllers
/// only map HTTP to these calls.
/// </summary>
public sealed partial class CalculatorService
{
    private const int MaxInputFields = 12;

    private readonly AppDbContext _dbContext;
    private readonly ILogger<CalculatorService> _logger;
    private readonly Dictionary<string, ICalculatorHandler> _handlers;

    public CalculatorService(
        AppDbContext dbContext,
        IEnumerable<ICalculatorHandler> handlers,
        ILogger<CalculatorService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _handlers = handlers.ToDictionary(handler => handler.Slug, StringComparer.Ordinal);
    }

    internal async Task<PagedResult<CalculatorSummaryResponse>> ListAsync(
        PageRequest paging,
        string? search,
        CalculatorCategory? category,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Calculators.AsNoTracking();

        if (!includeUnpublished)
        {
            query = PublicContentScope.Calculators(query);
        }

        if (category is { } selected)
        {
            query = query.Where(calculator => calculator.Category == selected);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // The wildcards are escaped so a search for "50%" cannot match everything.
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(calculator =>
                EF.Functions.Like(calculator.Name, pattern, LikeEscape)
                || EF.Functions.Like(calculator.Summary, pattern, LikeEscape));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var paged = paging.ClampTo(totalCount);

        var items = await query
            .OrderBy(calculator => calculator.SortOrder)
            .ThenBy(calculator => calculator.Name)
            .Skip(paged.Skip)
            .Take(paged.PageSize)
            .Select(calculator => new CalculatorSummaryResponse(
                calculator.Slug,
                calculator.Name,
                calculator.Summary,
                calculator.Category,
                calculator.IsPublished))
            .ToListAsync(cancellationToken);

        return new PagedResult<CalculatorSummaryResponse>(items, paged.Page, paged.PageSize, totalCount);
    }

    internal async Task<PagedResult<CalculatorAdminSummaryResponse>> ListForAdminAsync(
        PageRequest paging,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Calculators.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(calculator =>
                EF.Functions.Like(calculator.Name, pattern, LikeEscape)
                || EF.Functions.Like(calculator.Summary, pattern, LikeEscape)
                || EF.Functions.Like(calculator.Slug, pattern, LikeEscape));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var paged = paging.ClampTo(totalCount);

        var items = await query
            .OrderBy(calculator => calculator.SortOrder)
            .ThenBy(calculator => calculator.Name)
            .Skip(paged.Skip)
            .Take(paged.PageSize)
            .Select(calculator => new CalculatorAdminSummaryResponse(
                calculator.Id,
                calculator.Slug,
                calculator.Name,
                calculator.Summary,
                calculator.Category,
                calculator.IsPublished,
                calculator.SortOrder,
                calculator.UpdatedAt,
                calculator.CreatedByUser == null ? null : calculator.CreatedByUser.Email))
            .ToListAsync(cancellationToken);

        return new PagedResult<CalculatorAdminSummaryResponse>(items, paged.Page, paged.PageSize, totalCount);
    }

    /// <summary>
    /// The full staff record for one row, including the input schema. Loaded as an
    /// entity because the schema is JSON and has to be deserialised in memory, so it
    /// is deliberately not part of the list projection.
    /// </summary>
    internal async Task<CalculatorAdminResponse?> FindForAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var calculator = await _dbContext.Calculators
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calculator is null)
        {
            return null;
        }

        var createdBy = calculator.CreatedByUserId is null
            ? null
            : await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == calculator.CreatedByUserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

        return new CalculatorAdminResponse(
            calculator.Id,
            calculator.Slug,
            calculator.Name,
            calculator.Summary,
            calculator.Description,
            calculator.Category,
            CalculatorInputSchema.Deserialize(calculator.InputsJson),
            calculator.IsPublished,
            calculator.SortOrder,
            calculator.CreatedAt,
            calculator.UpdatedAt,
            createdBy);
    }

    internal async Task<CalculatorDetailResponse?> FindBySlugAsync(
        string slug,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        var calculator = await _dbContext.Calculators
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Slug == slug && (includeUnpublished || item.IsPublished),
                cancellationToken);

        return calculator is null ? null : ToDetail(calculator);
    }

    internal async Task<CalculationOutcome> CalculateAsync(
        string slug,
        IReadOnlyDictionary<string, JsonElement>? supplied,
        CancellationToken cancellationToken)
    {
        var calculator = await _dbContext.Calculators
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == slug && item.IsPublished, cancellationToken);

        if (calculator is null)
        {
            return new CalculationOutcome(CalculationStatus.NotFound);
        }

        var fields = CalculatorInputSchema.Deserialize(calculator.InputsJson);
        if (fields.Count == 0 || !_handlers.TryGetValue(calculator.Slug, out var handler))
        {
            _logger.LogWarning(
                "Published calculator {Slug} has no usable handler or input schema.",
                calculator.Slug);

            return new CalculationOutcome(
                CalculationStatus.Unavailable,
                Message: "This calculator is not available at the moment.");
        }

        var fieldErrors = Validate(fields, supplied);
        if (fieldErrors.Count > 0)
        {
            return new CalculationOutcome(CalculationStatus.Invalid, FieldErrors: fieldErrors);
        }

        var values = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (supplied is not null && supplied.TryGetValue(field.Key, out var element) && TryReadDecimal(element, out var value))
            {
                values[field.Key] = value;
            }
            else if (field.DefaultValue is { } fallback)
            {
                values[field.Key] = fallback;
            }
        }

        try
        {
            var result = handler.Calculate(values);

            return new CalculationOutcome(
                CalculationStatus.Success,
                new CalculationResponse(calculator.Slug, calculator.Name, result));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The bounds are validated first, so reaching here means the handler met a
            // combination it could not handle. Report it rather than a wrong number.
            _logger.LogWarning(exception, "Calculator {Slug} failed for the supplied inputs.", slug);

            return new CalculationOutcome(
                CalculationStatus.Failed,
                Message: "These inputs could not be calculated. Check the values and try again.");
        }
    }

    internal async Task<SaveOutcome> CreateAsync(
        SaveCalculatorRequest request,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim();

        var definitionErrors = ValidateDefinition(request);
        if (definitionErrors.Count > 0)
        {
            return new SaveOutcome(SaveStatus.Invalid, FieldErrors: definitionErrors);
        }

        if (await _dbContext.Calculators.AnyAsync(calculator => calculator.Slug == slug, cancellationToken))
        {
            return new SaveOutcome(SaveStatus.SlugTaken);
        }

        var calculator = new Calculator
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name.Trim(),
            Summary = request.Summary.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            InputsJson = CalculatorInputSchema.Serialize(request.Inputs!),
            SortOrder = request.SortOrder,
            IsPublished = request.IsPublished,
            CreatedByUserId = createdByUserId,
        };

        _dbContext.Calculators.Add(calculator);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveOutcome(SaveStatus.Created, ToDetail(calculator));
    }

    internal async Task<SaveOutcome> UpdateAsync(
        Guid id,
        SaveCalculatorRequest request,
        CancellationToken cancellationToken)
    {
        var calculator = await _dbContext.Calculators
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calculator is null)
        {
            return new SaveOutcome(SaveStatus.NotFound);
        }

        // A slug is part of the public URL, so it is fixed once the row exists.
        if (!string.Equals(request.Slug.Trim(), calculator.Slug, StringComparison.Ordinal))
        {
            return new SaveOutcome(
                SaveStatus.SlugImmutable,
                Message: "A slug cannot be changed after the calculator is created.");
        }

        var definitionErrors = ValidateDefinition(request);
        if (definitionErrors.Count > 0)
        {
            return new SaveOutcome(SaveStatus.Invalid, FieldErrors: definitionErrors);
        }

        calculator.Name = request.Name.Trim();
        calculator.Summary = request.Summary.Trim();
        calculator.Description = request.Description.Trim();
        calculator.Category = request.Category;
        calculator.InputsJson = CalculatorInputSchema.Serialize(request.Inputs!);
        calculator.SortOrder = request.SortOrder;
        calculator.IsPublished = request.IsPublished;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveOutcome(SaveStatus.Updated, ToDetail(calculator));
    }

    internal async Task<DeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var calculator = await _dbContext.Calculators
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (calculator is null)
        {
            return DeleteStatus.NotFound;
        }

        // Anything that has been public stays: retract it by clearing IsPublished.
        if (calculator.IsPublished)
        {
            return DeleteStatus.Published;
        }

        _dbContext.Calculators.Remove(calculator);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return DeleteStatus.Deleted;
    }

    private static CalculatorDetailResponse ToDetail(Calculator calculator) =>
        new(
            calculator.Id,
            calculator.Slug,
            calculator.Name,
            calculator.Summary,
            calculator.Description,
            calculator.Category,
            CalculatorInputSchema.Deserialize(calculator.InputsJson),
            calculator.IsPublished,
            calculator.SortOrder,
            calculator.UpdatedAt);

    private const string LikeEscape = "\\";

    private static string EscapeLike(string term) => term
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            case JsonValueKind.String:
                return decimal.TryParse(
                    element.GetString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out value);
            default:
                value = default;
                return false;
        }
    }

    private IReadOnlyList<FieldError> ValidateDefinition(SaveCalculatorRequest request)
    {
        var errors = new List<FieldError>();

        if (request.IsPublished && !_handlers.ContainsKey(request.Slug.Trim()))
        {
            errors.Add(new FieldError(
                "isPublished",
                "unsupported",
                "No calculation is implemented for this slug yet, so it cannot be published."));
        }

        errors.AddRange(ValidateFieldDefinitions(request.Inputs));
        return errors;
    }

    private static IReadOnlyList<FieldError> ValidateFieldDefinitions(IReadOnlyList<InputFieldDefinition>? fields)
    {
        var errors = new List<FieldError>();

        if (fields is null || fields.Count == 0)
        {
            errors.Add(new FieldError("inputs", "required", "Add at least one input field."));
            return errors;
        }

        if (fields.Count > MaxInputFields)
        {
            errors.Add(new FieldError("inputs", "too_many", $"A calculator can have at most {MaxInputFields} inputs."));
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var key = field.Key?.Trim() ?? string.Empty;

            if (key.Length == 0)
            {
                errors.Add(new FieldError("inputs", "invalid", "Every input field needs a key."));
                continue;
            }

            if (!SlugKeyPattern().IsMatch(key))
            {
                errors.Add(new FieldError(
                    "inputs", "invalid", $"'{key}' is not a valid key. Use lowercase words separated by single hyphens."));
                continue;
            }

            if (!seen.Add(key))
            {
                errors.Add(new FieldError("inputs", "invalid", $"'{key}' is used more than once."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(field.Label))
            {
                errors.Add(new FieldError("inputs", "invalid", $"'{key}' needs a label."));
                continue;
            }

            if (field.Minimum is { } minimum && field.Maximum is { } maximum && minimum > maximum)
            {
                errors.Add(new FieldError("inputs", "invalid", $"'{key}' has a minimum above its maximum."));
                continue;
            }

            if (field.Step is { } step && step <= 0m)
            {
                errors.Add(new FieldError("inputs", "invalid", $"'{key}' must have a step above zero."));
                continue;
            }

            if (field.DefaultValue is { } fallback
                && ((field.Minimum is { } floor && fallback < floor)
                    || (field.Maximum is { } ceiling && fallback > ceiling)))
            {
                errors.Add(new FieldError("inputs", "invalid", $"'{key}' has a default value outside its own range."));
            }
        }

        return errors;
    }

    private static IReadOnlyList<FieldError> Validate(
        IReadOnlyList<InputFieldDefinition> fields,
        IReadOnlyDictionary<string, JsonElement>? supplied)
    {
        var errors = new List<FieldError>();

        foreach (var field in fields)
        {
            if (supplied is null
                || !supplied.TryGetValue(field.Key, out var element)
                || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (field.Required)
                {
                    errors.Add(new FieldError(field.Key, "required", $"{field.Label} is required."));
                }

                continue;
            }

            if (!TryReadDecimal(element, out var value))
            {
                errors.Add(new FieldError(field.Key, "invalid", $"{field.Label} must be a number."));
                continue;
            }

            if (field.Minimum is { } minimum && value < minimum)
            {
                errors.Add(new FieldError(
                    field.Key, "out_of_range", $"{field.Label} must be at least {minimum}."));
                continue;
            }

            if (field.Maximum is { } maximum && value > maximum)
            {
                errors.Add(new FieldError(
                    field.Key, "out_of_range", $"{field.Label} must be at most {maximum}."));
            }
        }

        // An unknown key is almost always a client typo. Silently ignoring it would
        // fall back to a default and answer with a plausible but wrong figure.
        if (supplied is not null)
        {
            var known = fields.Select(field => field.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var key in supplied.Keys)
            {
                if (!known.Contains(key))
                {
                    errors.Add(new FieldError(key, "unexpected", $"'{key}' is not an input on this calculator."));
                }
            }
        }

        return errors;
    }

    [GeneratedRegex(CalculatorSlug.Pattern)]
    private static partial Regex SlugKeyPattern();
}
