using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Evaluation;

internal static class PolicyEvaluationCanonicalSerializer
{
    public static PolicyChecksum CalculateInputChecksum(PolicyFactSet facts)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("facts");
            foreach (PolicyFact fact in facts.Facts)
            {
                writer.WriteStartObject();
                writer.WriteString("path", fact.Path);
                writer.WriteString("type", fact.ValueType.ToString());
                writer.WritePropertyName("value");
                WriteValue(writer, fact.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Checksum(buffer.WrittenSpan);
    }

    public static PolicyChecksum CalculateTraceChecksum(
        PolicyChecksum inputChecksum,
        PolicyOutcomeAggregate outcome,
        IReadOnlyList<PolicyRuleEvaluation> rules)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("inputChecksum", inputChecksum.Value);
            writer.WriteString("disposition", outcome.Disposition.ToString());
            writer.WriteBoolean("defaultOutcomeApplied", outcome.DefaultOutcomeApplied);
            writer.WriteStartArray("requiredApproverRoles");
            foreach (PolicyApproverRole role in outcome.RequiredApproverRoles)
            {
                writer.WriteStringValue(role.ToString());
            }

            writer.WriteEndArray();
            writer.WriteStartArray("reasons");
            foreach (PolicyEvaluationReason reason in outcome.Reasons)
            {
                writer.WriteStartObject();
                writer.WriteString("code", reason.Code.Value);
                writer.WriteString("message", reason.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("rules");
            foreach (PolicyRuleEvaluation rule in rules)
            {
                WriteRule(writer, rule);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Checksum(buffer.WrittenSpan);
    }

    private static void WriteRule(Utf8JsonWriter writer, PolicyRuleEvaluation rule)
    {
        writer.WriteStartObject();
        writer.WriteString("ruleId", rule.RuleId);
        writer.WriteNumber("priority", rule.Priority);
        writer.WriteBoolean("matched", rule.Matched);
        writer.WritePropertyName("condition");
        WriteCondition(writer, rule.Condition);
        if (rule.MatchedOutcome is not null)
        {
            writer.WriteStartObject("outcome");
            writer.WriteString("disposition", rule.MatchedOutcome.Disposition.ToString());
            writer.WriteString("reasonCode", rule.MatchedOutcome.ReasonCode.Value);
            writer.WriteString("message", rule.MatchedOutcome.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteCondition(
        Utf8JsonWriter writer,
        PolicyConditionEvaluation condition)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", condition.Kind.ToString());
        if (condition.Operator is PolicyOperator @operator)
        {
            writer.WriteString("operator", PolicyOperatorNames.ToJsonName(@operator));
        }

        writer.WriteBoolean("result", condition.Result);
        writer.WriteStartArray("factAccesses");
        foreach (PolicyFactAccess access in condition.FactAccesses)
        {
            writer.WriteStartObject();
            writer.WriteString("path", access.Path);
            writer.WriteString("type", access.ValueType.ToString());
            writer.WriteBoolean("exists", access.Exists);
            if (access.RawValue is not null)
            {
                writer.WritePropertyName("value");
                WriteValue(writer, access.RawValue);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("children");
        foreach (PolicyConditionEvaluation child in condition.Children)
        {
            WriteCondition(writer, child);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, PolicyValue value)
    {
        switch (value)
        {
            case PolicyStringValue text:
                writer.WriteStringValue(text.Value);
                break;
            case PolicyNumberValue number:
                writer.WriteRawValue(
                    number.Value.ToString("G29", CultureInfo.InvariantCulture),
                    skipInputValidation: true);
                break;
            case PolicyBooleanValue boolean:
                writer.WriteBooleanValue(boolean.Value);
                break;
            default:
                throw new PolicyEvaluationException(
                    PolicyEvaluationErrorCodes.FactTypeMismatch,
                    "$",
                    "The evaluation contains an unsupported value type.");
        }
    }

    private static PolicyChecksum Checksum(ReadOnlySpan<byte> canonical)
    {
        return PolicyChecksum.Parse(Convert.ToHexStringLower(SHA256.HashData(canonical)));
    }
}
