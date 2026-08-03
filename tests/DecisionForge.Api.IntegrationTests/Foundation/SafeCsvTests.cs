using System.Text;
using DecisionForge.Api.Exports;

namespace DecisionForge.Api.IntegrationTests.Foundation;

public sealed class SafeCsvTests
{
    [Theory]
    [InlineData("=2+2", "'=2+2")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-10+20", "'-10+20")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    [InlineData("  =hidden", "'  =hidden")]
    [InlineData("ordinary", "ordinary")]
    [InlineData(null, "")]
    public void EncodeFieldNeutralizesFormulaInjection(string? input, string expected)
    {
        Assert.Equal(expected, SafeCsv.EncodeField(input));
    }

    [Fact]
    public void CreateRowEscapesQuotesCommasAndNewlines()
    {
        Assert.Equal(
            "\"a,b\",\"quote\"\"value\",\"line\r\nbreak\"\r\n",
            SafeCsv.CreateRow(["a,b", "quote\"value", "line\r\nbreak"]));
    }

    [Fact]
    public async Task WriteRowPropagatesCancellation()
    {
        await using MemoryStream destination = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SafeCsv.WriteRowAsync(destination, ["value"], cancellation.Token));
    }
}
