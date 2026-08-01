using System.Text;
using System.Text.Json;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.Policies.Parsing;

public static class PolicyJsonParser
{
    public static PolicyParseResult Parse(string? json)
    {
        if (json is null)
        {
            return Failure(
                "$",
                "policy.json.required",
                "Policy JSON is required.");
        }

        if (Encoding.UTF8.GetByteCount(json) > PolicyContractLimits.MaximumJsonBytes)
        {
            return Failure(
                "$",
                "policy.limit.json-size",
                "Policy JSON exceeds the supported size.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
            PolicyDocumentReader reader = new();
            PolicyDefinition? definition = reader.Read(document.RootElement);
            if (definition is null || reader.Errors.Count > 0)
            {
                return new PolicyParseResult(null, reader.Errors);
            }

            IReadOnlyList<PolicyValidationError> semanticErrors =
                PolicyValidator.Validate(definition);
            return semanticErrors.Count == 0
                ? new PolicyParseResult(definition, [])
                : new PolicyParseResult(null, semanticErrors);
        }
        catch (JsonException)
        {
            return Failure(
                "$",
                "policy.json.malformed",
                "Policy JSON is malformed.");
        }
    }

    private static PolicyParseResult Failure(string path, string code, string message)
    {
        return new PolicyParseResult(
            null,
            [
                new PolicyValidationError(
                    path,
                    code,
                    PolicyValidationSeverity.Error,
                    message),
            ]);
    }
}
