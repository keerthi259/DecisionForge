using System.Globalization;
using DecisionForge.Domain.Audit;
using FsCheck;
using FsCheck.Xunit;

namespace DecisionForge.Domain.UnitTests.Audit;

public sealed class AuditPayloadProperties
{
    [Property(MaxTest = 100)]
    public bool CanonicalPayloadDoesNotDependOnInputOrder(PositiveInt input)
    {
        int count = input.Get % AuditPayload.MaximumFieldCount + 1;
        KeyValuePair<string, string>[] fields = Enumerable.Range(0, count)
            .Select(index => new KeyValuePair<string, string>(
                $"field{index:D2}",
                ((long)input.Get + index).ToString(CultureInfo.InvariantCulture)))
            .ToArray();

        string forward = AuditPayload.Create(fields).CanonicalJson;
        string reversed = AuditPayload.Create(fields.Reverse()).CanonicalJson;

        return string.Equals(forward, reversed, StringComparison.Ordinal);
    }
}
