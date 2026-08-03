using System.Text;

namespace DecisionForge.Api.Exports;

public static class SafeCsv
{
    private static readonly char[] _formulaPrefixes = ['=', '+', '-', '@'];

    public static string EncodeField(string? value)
    {
        string safe = NeutralizeFormula(value ?? string.Empty);
        if (!safe.Contains(',', StringComparison.Ordinal)
            && !safe.Contains('"', StringComparison.Ordinal)
            && !safe.Contains('\r', StringComparison.Ordinal)
            && !safe.Contains('\n', StringComparison.Ordinal))
        {
            return safe;
        }

        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public static string CreateRow(IEnumerable<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return string.Join(',', fields.Select(EncodeField)) + "\r\n";
    }

    public static async Task WriteRowAsync(
        Stream destination,
        IEnumerable<string?> fields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        byte[] bytes = Encoding.UTF8.GetBytes(CreateRow(fields));
        await destination.WriteAsync(bytes, cancellationToken);
    }

    private static string NeutralizeFormula(string value)
    {
        int firstContent = 0;
        while (firstContent < value.Length && char.IsWhiteSpace(value[firstContent]))
        {
            firstContent++;
        }

        if (firstContent < value.Length
            && (_formulaPrefixes.Contains(value[firstContent])
                || value[firstContent] is '\t' or '\r'))
        {
            return "'" + value;
        }

        return value;
    }
}
