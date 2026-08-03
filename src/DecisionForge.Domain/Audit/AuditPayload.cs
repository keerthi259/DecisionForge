using System.Collections.ObjectModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.Audit;

public sealed class AuditPayload
{
    public const int MaximumFieldCount = 64;
    public const int MaximumFieldNameLength = 64;
    public const int MaximumFieldValueLength = 1_024;
    public const int MaximumCanonicalLength = 16_384;

    private static readonly string[] _sensitiveFragments =
    [
        "authorization",
        "cookie",
        "credential",
        "definitionjson",
        "password",
        "policyjson",
        "secret",
        "token",
    ];

    private AuditPayload(IReadOnlyDictionary<string, string> fields, string canonicalJson)
    {
        Fields = fields;
        CanonicalJson = canonicalJson;
    }

    public IReadOnlyDictionary<string, string> Fields { get; }

    public string CanonicalJson { get; }

    public static AuditPayload Empty { get; } = Create([]);

    public static AuditPayload Create(IEnumerable<KeyValuePair<string, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        SortedDictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in fields)
        {
            AddField(normalized, field.Key, field.Value);
        }

        if (normalized.Count > MaximumFieldCount)
        {
            throw DomainGuard.Validation(
                nameof(fields),
                $"Audit payload cannot contain more than {MaximumFieldCount} fields.");
        }

        string canonicalJson = Serialize(normalized);
        if (Encoding.UTF8.GetByteCount(canonicalJson) > MaximumCanonicalLength)
        {
            throw DomainGuard.Validation(
                nameof(fields),
                $"Canonical audit payload cannot exceed {MaximumCanonicalLength} UTF-8 bytes.");
        }

        return new AuditPayload(
            new ReadOnlyDictionary<string, string>(normalized),
            canonicalJson);
    }

    private static void AddField(
        IDictionary<string, string> destination,
        string? name,
        string? value)
    {
        string normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is 0 or > MaximumFieldNameLength
            || !char.IsAsciiLetter(normalizedName[0])
            || normalizedName.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw DomainGuard.Validation(nameof(name), "Audit field name is invalid.");
        }

        string comparableName = normalizedName.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        bool uncontrolledReason = comparableName.Contains("reason", StringComparison.Ordinal)
            && !comparableName.EndsWith("reasoncode", StringComparison.Ordinal)
            && !comparableName.EndsWith("reasonprovided", StringComparison.Ordinal)
            && !comparableName.EndsWith("reasonlength", StringComparison.Ordinal)
            && !comparableName.EndsWith("reasonsha256", StringComparison.Ordinal);
        if (uncontrolledReason
            || _sensitiveFragments.Any(fragment => comparableName.Contains(fragment, StringComparison.Ordinal)))
        {
            throw DomainGuard.Validation(
                nameof(name),
                $"Audit field '{normalizedName}' is not permitted in a safe payload.");
        }

        if (value is null || value.Length > MaximumFieldValueLength)
        {
            throw DomainGuard.Validation(nameof(value), "Audit field value is invalid.");
        }

        if (!destination.TryAdd(normalizedName, value))
        {
            throw DomainGuard.Validation(nameof(name), $"Audit field '{normalizedName}' is duplicated.");
        }
    }

    private static string Serialize(IReadOnlyDictionary<string, string> fields)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> field in fields)
            {
                writer.WriteString(field.Key, field.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
