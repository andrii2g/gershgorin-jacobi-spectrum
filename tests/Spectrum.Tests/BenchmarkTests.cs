using System.Text.Json;
using Spectrum.Cli;
namespace Spectrum.Tests;
public sealed class BenchmarkTests
{
    [Fact] public void SeparateUntracedRunsAndSharedHashes()
    {
        string dir = Path.Combine(Path.GetTempPath(), "spectrum-tests", Guid.NewGuid().ToString("N"));
        int code = CliApplication.Run(["compare", "--sizes", "4,8", "--families", "toeplitz,random", "--warmups", "1", "--repetitions", "2", "--out", dir], new StringWriter(), new StringWriter());
        Assert.Equal(0, code); using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "comparison.json")));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray(); Assert.Equal(8, rows.Length);
        for (int i = 0; i < rows.Length; i += 2)
        {
            Assert.Equal(rows[i].GetProperty("case").GetProperty("matrixSha256").GetString(), rows[i + 1].GetProperty("case").GetProperty("matrixSha256").GetString());
            foreach (var row in rows.Skip(i).Take(2))
            {
                Assert.Equal(0, row.GetProperty("timedOptions").GetProperty("traceSamples").GetInt32());
                Assert.Equal(0, row.GetProperty("timedOptions").GetProperty("snapshots").GetInt32());
                Assert.Equal(2, row.GetProperty("rawSolverSamples").GetArrayLength());
                foreach (var sample in row.GetProperty("rawSolverSamples").EnumerateArray())
                { Assert.Equal("Converged", sample.GetProperty("status").GetString()); Assert.Equal(row.GetProperty("rotations").GetInt64(), sample.GetProperty("rotations").GetInt64()); }
            }
        }
        Assert.Equal(9, File.ReadAllLines(Path.Combine(dir, "comparison.csv")).Length);
        CliIntegrationTests.ValidateSvg(Path.Combine(dir, "toeplitz-4", "convergence.svg"));
    }
    [Fact] public void MedianUsesBothMiddleValues()
    {
        Assert.Equal(new TimingStatistics(2.5, 1, 4), TimingStatistics.From([4, 1, 3, 2]));
        Assert.Equal(new TimingStatistics(2, 1, 4), TimingStatistics.From([4, 1, 2]));
    }
}
