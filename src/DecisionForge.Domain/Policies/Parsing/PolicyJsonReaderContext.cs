using System.Text.Json;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Parsing;

internal sealed class PolicyJsonReaderContext
{
    private readonly List<PolicyValidationError> _errors = [];

    public IReadOnlyList<PolicyValidationError> Errors => _errors;

    public Dictionary<string, JsonElement>? ReadObject(
        JsonElement? element,
        string path,
        IReadOnlyCollection<string> allowed,
        IReadOnlyCollection<string> required)
    {
        if (!RequireKind(element, JsonValueKind.Object, path))
        {
            return null;
        }

        Dictionary<string, JsonElement> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element!.Value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                Error(
                    Append(path, property.Name),
                    "policy.json.unknown-property",
                    "The JSON object contains an unsupported property.");
                continue;
            }

            if (!result.TryAdd(property.Name, property.Value))
            {
                Error(
                    Append(path, property.Name),
                    "policy.json.duplicate-property",
                    "JSON object property names must be unique.");
            }
        }

        foreach (string name in required)
        {
            if (!result.ContainsKey(name))
            {
                Error(
                    Append(path, name),
                    "policy.json.required",
                    "A required JSON property is missing.");
            }
        }

        return result;
    }

    public string? ReadCode(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath,
        int maximumLength)
    {
        string? value = ReadString(properties, name, parentPath);
        if (value is null)
        {
            return null;
        }

        try
        {
            return StringValueValidation.Code(value, maximumLength, name);
        }
        catch (DomainRuleException)
        {
            Error(
                $"{parentPath}.{name}",
                "policy.value.format",
                "The policy code value has an invalid format or length.");
            return null;
        }
    }

    public ReasonCode? ReadReasonCode(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath)
    {
        string? value = ReadString(properties, name, parentPath);
        if (value is null)
        {
            return null;
        }

        try
        {
            return ReasonCode.Parse(value);
        }
        catch (DomainRuleException)
        {
            Error(
                $"{parentPath}.{name}",
                "policy.value.format",
                "The reason code has an invalid format or length.");
            return null;
        }
    }

    public string? ReadRequiredText(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath,
        int maximumLength)
    {
        string? value = ReadString(properties, name, parentPath);
        if (value is null)
        {
            return null;
        }

        try
        {
            return StringValueValidation.Required(value, maximumLength, name);
        }
        catch (DomainRuleException)
        {
            Error(
                $"{parentPath}.{name}",
                "policy.value.length",
                "The policy text value has an invalid length.");
            return null;
        }
    }

    public TEnum? ReadControlledEnum<TEnum>(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath,
        string errorCode,
        string errorMessage)
        where TEnum : struct, Enum
    {
        string? value = ReadString(properties, name, parentPath);
        if (value is null)
        {
            return null;
        }

        if (ControlledEnumParser.TryParse(value, out TEnum result))
        {
            return result;
        }

        Error($"{parentPath}.{name}", errorCode, errorMessage);
        return null;
    }

    public int? ReadInteger(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath)
    {
        if (!properties.TryGetValue(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result))
        {
            return result;
        }

        Error($"{parentPath}.{name}", "policy.json.type", "The JSON value must be an integer.");
        return null;
    }

    public string? ReadString(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string parentPath)
    {
        if (!properties.TryGetValue(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        Error($"{parentPath}.{name}", "policy.json.type", "The JSON value must be a string.");
        return null;
    }

    public bool RequireKind(JsonElement? element, JsonValueKind kind, string path)
    {
        if (element is not null && element.Value.ValueKind == kind)
        {
            return true;
        }

        Error(path, "policy.json.type", "The JSON value has an invalid type.");
        return false;
    }

    public void Error(string path, string code, string message)
    {
        _errors.Add(new PolicyValidationError(
            path,
            code,
            PolicyValidationSeverity.Error,
            message));
    }

    public static JsonElement? Get(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        return properties.TryGetValue(name, out JsonElement value) ? value : null;
    }

    private static string Append(string path, string propertyName)
    {
        return propertyName.All(character =>
                character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_' or '-')
            ? $"{path}.{propertyName}"
            : path;
    }
}
