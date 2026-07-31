using DecisionForge.Domain.Common;

namespace DecisionForge.Application.ReferenceData;

public sealed record ReferenceDataPage
{
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 100;

    private ReferenceDataPage(string? search, int offset, int pageSize)
    {
        Search = search;
        Offset = offset;
        PageSize = pageSize;
    }

    public string? Search { get; }

    public int Offset { get; }

    public int PageSize { get; }

    public static ReferenceDataPage Create(string? search, int offset, int pageSize)
    {
        if (offset < 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Reference-data offset must not be negative.",
                nameof(offset));
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                $"Reference-data page size must be between 1 and {MaximumPageSize}.",
                nameof(pageSize));
        }

        string? normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch?.Length > MaximumSearchLength)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                $"Reference-data search must not exceed {MaximumSearchLength} characters.",
                nameof(search));
        }

        return new ReferenceDataPage(normalizedSearch, offset, pageSize);
    }
}
