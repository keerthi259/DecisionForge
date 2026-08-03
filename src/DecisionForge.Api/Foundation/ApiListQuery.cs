using System.Collections.ObjectModel;
using Microsoft.Extensions.Primitives;

namespace DecisionForge.Api.Foundation;

public enum ApiSortDirection
{
    Ascending = 1,
    Descending = 2,
}

public sealed record ApiSort(string Field, ApiSortDirection Direction);

public sealed record ApiListQuery(
    int Offset,
    int PageSize,
    ApiSort Sort,
    IReadOnlyDictionary<string, string> Filters);

public sealed class ApiListQueryDefinition
{
    private readonly HashSet<string> _sortFields;
    private readonly HashSet<string> _filterFields;

    public ApiListQueryDefinition(
        IReadOnlyCollection<string> sortFields,
        IReadOnlyCollection<string> filterFields,
        string defaultSortField,
        ApiSortDirection defaultSortDirection = ApiSortDirection.Ascending)
    {
        ArgumentNullException.ThrowIfNull(sortFields);
        ArgumentNullException.ThrowIfNull(filterFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSortField);
        _sortFields = new HashSet<string>(sortFields, StringComparer.OrdinalIgnoreCase);
        _filterFields = new HashSet<string>(filterFields, StringComparer.OrdinalIgnoreCase);
        if (_sortFields.Count == 0
            || _sortFields.Count != sortFields.Count
            || _filterFields.Count != filterFields.Count
            || !_sortFields.All(IsValidField)
            || !_filterFields.All(IsValidField)
            || _filterFields.Overlaps(ApiListQueryParser.ReservedFields)
            || !_sortFields.Contains(defaultSortField)
            || !Enum.IsDefined(defaultSortDirection))
        {
            throw new ArgumentException(
                "Sort and filter allow lists must be unique controlled fields with an allow-listed default.",
                nameof(sortFields));
        }

        DefaultSort = new ApiSort(defaultSortField, defaultSortDirection);
    }

    public ApiSort DefaultSort { get; }

    internal bool AllowsSort(string field)
    {
        return _sortFields.Contains(field);
    }

    internal bool AllowsFilter(string field)
    {
        return _filterFields.Contains(field);
    }

    private static bool IsValidField(string field)
    {
        return field.Length is > 0 and <= 64
            && char.IsAsciiLetterLower(field[0])
            && field.All(char.IsAsciiLetterOrDigit);
    }
}

public static class ApiListQueryParser
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    internal static readonly HashSet<string> ReservedFields = new(
        ["offset", "pageSize", "sort"],
        StringComparer.OrdinalIgnoreCase);

    public static ApiListQuery Parse(
        IQueryCollection query,
        ApiListQueryDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(definition);
        List<ApiValidationError> errors = [];
        int offset = ParseInteger(query, "offset", 0, 0, int.MaxValue, errors);
        int pageSize = ParseInteger(
            query,
            "pageSize",
            DefaultPageSize,
            1,
            MaximumPageSize,
            errors);
        ApiSort sort = ParseSort(query, definition, errors);
        Dictionary<string, string> filters = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, StringValues values) in query)
        {
            if (ReservedFields.Contains(key))
            {
                continue;
            }

            if (!definition.AllowsFilter(key))
            {
                errors.Add(new ApiValidationError(
                    "query.filter.unsupported",
                    key,
                    "The filter field is not supported."));
                continue;
            }

            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            {
                errors.Add(new ApiValidationError(
                    "query.filter.invalid",
                    key,
                    "A filter must contain exactly one non-empty value."));
                continue;
            }

            filters[key] = values[0]!.Trim();
        }

        if (errors.Count > 0)
        {
            throw new ApiRequestValidationException(errors);
        }

        return new ApiListQuery(
            offset,
            pageSize,
            sort,
            new ReadOnlyDictionary<string, string>(filters));
    }

    private static int ParseInteger(
        IQueryCollection query,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        List<ApiValidationError> errors)
    {
        if (!query.TryGetValue(key, out StringValues values))
        {
            return defaultValue;
        }

        if (values.Count != 1
            || !int.TryParse(
                values[0],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int parsed)
            || parsed < minimum
            || parsed > maximum)
        {
            errors.Add(new ApiValidationError(
                "query.pagination.invalid",
                key,
                $"The value must be an integer between {minimum} and {maximum}."));
            return defaultValue;
        }

        return parsed;
    }

    private static ApiSort ParseSort(
        IQueryCollection query,
        ApiListQueryDefinition definition,
        List<ApiValidationError> errors)
    {
        if (!query.TryGetValue("sort", out StringValues values))
        {
            return definition.DefaultSort;
        }

        string? value = values.Count == 1 ? values[0] : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ApiValidationError(
                "query.sort.invalid",
                "sort",
                "A sort must contain exactly one field."));
            return definition.DefaultSort;
        }

        ApiSortDirection direction = value[0] == '-'
            ? ApiSortDirection.Descending
            : ApiSortDirection.Ascending;
        string field = direction == ApiSortDirection.Descending ? value[1..] : value;
        if (string.IsNullOrWhiteSpace(field) || !definition.AllowsSort(field))
        {
            errors.Add(new ApiValidationError(
                "query.sort.unsupported",
                "sort",
                "The sort field is not supported."));
            return definition.DefaultSort;
        }

        return new ApiSort(field, direction);
    }
}
