using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Serialization;

public static class PolicyCanonicalSerializer
{
    public static string Serialize(PolicyDefinition definition)
    {
        return Encoding.UTF8.GetString(SerializeToUtf8Bytes(definition));
    }

    public static PolicyChecksum CalculateChecksum(PolicyDefinition definition)
    {
        byte[] canonical = SerializeToUtf8Bytes(definition);
        byte[] hash = SHA256.HashData(canonical);
        return PolicyChecksum.Parse(Convert.ToHexStringLower(hash));
    }

    public static byte[] SerializeToUtf8Bytes(PolicyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            WriteDefinition(writer, definition);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteDefinition(Utf8JsonWriter writer, PolicyDefinition definition)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", definition.SchemaVersion);
        writer.WriteString("policyCode", definition.PolicyCode);
        writer.WriteString("name", definition.Name);
        writer.WritePropertyName("defaultOutcome");
        WriteOutcome(writer, definition.DefaultOutcome);
        writer.WriteStartArray("rules");
        foreach (PolicyRule rule in definition.Rules)
        {
            WriteRule(writer, rule);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRule(Utf8JsonWriter writer, PolicyRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("id", rule.Id);
        writer.WriteNumber("priority", rule.Priority);
        writer.WritePropertyName("when");
        WriteCondition(writer, rule.When);
        writer.WritePropertyName("then");
        WriteOutcome(writer, rule.Then);
        writer.WriteEndObject();
    }

    private static void WriteOutcome(Utf8JsonWriter writer, PolicyOutcome outcome)
    {
        writer.WriteStartObject();
        writer.WriteString("disposition", outcome.Disposition.ToString());
        if (outcome.RequiredApproverRoles.Count > 0)
        {
            writer.WriteStartArray("requiredApproverRoles");
            foreach (PolicyApproverRole role in outcome.RequiredApproverRoles)
            {
                writer.WriteStringValue(role.ToString());
            }

            writer.WriteEndArray();
        }

        writer.WriteString("reasonCode", outcome.ReasonCode.Value);
        writer.WriteString("message", outcome.Message);
        writer.WriteEndObject();
    }

    private static void WriteCondition(Utf8JsonWriter writer, PolicyCondition condition)
    {
        writer.WriteStartObject();
        switch (condition)
        {
            case PolicyComparisonCondition comparison:
                WriteLeafStart(writer, comparison.Fact, comparison.Operator);
                writer.WritePropertyName("value");
                WriteValue(writer, comparison.Value);
                break;
            case PolicyMembershipCondition membership:
                WriteLeafStart(writer, membership.Fact, membership.Operator);
                writer.WriteStartArray("value");
                foreach (PolicyValue value in membership.Values)
                {
                    WriteValue(writer, value);
                }

                writer.WriteEndArray();
                break;
            case PolicyExistenceCondition existence:
                WriteLeafStart(writer, existence.Fact, existence.Operator);
                break;
            case PolicyAllCondition all:
                WriteChildren(writer, "all", all.Children);
                break;
            case PolicyAnyCondition any:
                WriteChildren(writer, "any", any.Children);
                break;
            case PolicyNotCondition not:
                writer.WritePropertyName("not");
                WriteCondition(writer, not.Child);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(condition),
                    condition,
                    "Unsupported policy condition.");
        }

        writer.WriteEndObject();
    }

    private static void WriteLeafStart(
        Utf8JsonWriter writer,
        string fact,
        PolicyOperator @operator)
    {
        writer.WriteString("fact", fact);
        writer.WriteString("operator", PolicyOperatorNames.ToJsonName(@operator));
    }

    private static void WriteChildren(
        Utf8JsonWriter writer,
        string name,
        IEnumerable<PolicyCondition> children)
    {
        writer.WriteStartArray(name);
        foreach (PolicyCondition child in children)
        {
            WriteCondition(writer, child);
        }

        writer.WriteEndArray();
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
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unsupported policy value.");
        }
    }
}
