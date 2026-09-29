using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Spectrum.Cli;
namespace Spectrum.Tests;

public sealed class CliIntegrationTests
{
    private static string Temp() { string path = Path.Combine(Path.GetTempPath(), "spectrum-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static int Run(params string[] args) => CliApplication.Run(args, new StringWriter(), new StringWriter());
    [Fact] public void HelpAndInvalidOptions()
    {
        Assert.Equal(0, Run("help")); Assert.Equal(0, Run("solve", "--help"));
        foreach (string[] args in new[] {
            new[] { "solve", "--wat" }, ["solve", "--size"], ["solve", "--size", "2", "--size", "3"],
            ["solve", "--size", "0"], ["solve", "--rtol", "NaN"], ["solve", "--max-sweeps", "2"],
            ["solve", "--family", "random", "--epsilon", "1"], ["solve", "--input", "x", "--seed", "2"],
            ["solve", "--size", "513"], ["solve", "--max-rotations", "0"], ["solve", "--seed", "18446744073709551616"],
            ["demo", "--pivot", "max"], ["compare", "--sizes", "8,4"], ["solve", "--pivot", "other"] }) Assert.Equal(2, Run(args));
    }
    [Fact] public void DemoReplayAndAllSvgClasses()
    {
        string dir = Temp(); Assert.Equal(0, Run("demo", "--out", dir));
        Assert.True(File.Exists(Path.Combine(dir, "index.md")));
        foreach (string sub in Directory.GetDirectories(dir))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(sub, "summary.json")));
            Assert.Equal("Converged", json.RootElement.GetProperty("solver").GetProperty("status").GetString());
            Assert.Equal(6, File.ReadAllLines(Path.Combine(sub, "eigenpairs.csv")).Length - 1);
            Assert.Equal(37, File.ReadAllLines(Path.Combine(sub, "eigenvectors.csv")).Length);
            if (Path.GetFileName(sub).StartsWith("analytic", StringComparison.Ordinal))
                Assert.Equal(new double[] { -2, 1, 3, 4, 5, 7 }, json.RootElement.GetProperty("eigenvalues").EnumerateArray().Select(e => e.GetDouble()));
            foreach (string svg in Directory.GetFiles(sub, "*.svg")) ValidateSvg(svg);
        }
        string replay = Path.Combine(dir, "replay with spaces");
        Assert.Equal(0, Run("solve", "--input", Path.Combine(dir, "analytic-max", "matrix.json"), "--out", replay));
        Assert.Equal(6, Run("solve", "--out", replay));
        File.WriteAllText(Path.Combine(replay, "keep.txt"), "user data");
        Assert.Equal(0, Run("solve", "--family", "near-diagonal", "--epsilon", "0", "--out", replay, "--overwrite"));
        Assert.Equal("user data", File.ReadAllText(Path.Combine(replay, "keep.txt")));
        var convergence = XDocument.Load(Path.Combine(replay, "convergence.svg")); Assert.Contains("zero", convergence.ToString());
        var disks = XDocument.Load(Path.Combine(replay, "gershgorin.svg")); Assert.Contains("zero-radius", disks.ToString());
        Assert.DoesNotContain("class=\"disk\"", disks.ToString());
        Assert.Equal(0, Run("solve", "--snapshots", "0", "--out", replay, "--overwrite")); Assert.False(File.Exists(Path.Combine(replay, "snapshots.svg")));
    }
    internal static void ValidateSvg(string path)
    {
        var doc = XDocument.Load(path); Assert.Equal("svg", doc.Root!.Name.LocalName);
        foreach (var a in doc.Descendants().Attributes().Where(a => new[] { "x", "y", "cx", "cy", "r", "x1", "x2", "y1", "y2" }.Contains(a.Name.LocalName)))
            Assert.True(double.IsFinite(double.Parse(a.Value, CultureInfo.InvariantCulture)), a.ToString());
        foreach (var line in doc.Descendants().Where(e => (string?)e.Attribute("class") == "eigenvalue"))
            Assert.Equal(14, double.Parse(line.Attribute("y2")!.Value, CultureInfo.InvariantCulture) - double.Parse(line.Attribute("y1")!.Value, CultureInfo.InvariantCulture), 10);
    }
    [Fact] public void CultureAndRepeatedRuns()
    {
        string dir = Temp(); var prior = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(0, Run("solve", "--family", "toeplitz", "--size", "8", "--diagonal", "2.5", "--seed", "18446744073709551615", "--out", Path.Combine(dir, culture)));
            }
            foreach (string filename in Directory.GetFiles(Path.Combine(dir, "en-US")).Select(Path.GetFileName).OfType<string>())
            {
                string a = File.ReadAllText(Path.Combine(dir, "en-US", filename)), b = File.ReadAllText(Path.Combine(dir, "de-DE", filename));
                if (filename == "summary.json")
                {
                    var ja = JsonNode.Parse(a)!.AsObject(); var jb = JsonNode.Parse(b)!.AsObject();
                    ja.Remove("timings"); ja.Remove("environment"); jb.Remove("timings"); jb.Remove("environment");
                    Assert.Equal(ja.ToJsonString(), jb.ToJsonString());
                    Assert.Equal("18446744073709551615", ja["case"]!["seed"]!.GetValue<string>());
                }
                else Assert.Equal(a, b);
                Assert.DoesNotContain('\r', a);
            }
        }
        finally { CultureInfo.CurrentCulture = prior; }
        Assert.Equal("\"a,b\"", CsvExporter.Field("a,b")); Assert.Equal("\"a\"\"b\"", CsvExporter.Field("a\"b"));
    }
    [Fact] public void ErrorCodesAndFiniteFailureJson()
    {
        string dir = Temp(); Assert.Equal(4, Run("solve", "--size", "5", "--max-rotations", "1", "--out", Path.Combine(dir, "limit")));
        Assert.Equal(130, CliApplication.Run(["solve", "--out", Path.Combine(dir, "cancel")], new StringWriter(), new StringWriter(), new CancellationToken(true)));
        string input = Path.Combine(dir, "huge.json"); File.WriteAllText(input, "{\"schemaVersion\":1,\"name\":\"huge <&>\",\"matrix\":[[1.7976931348623157e308,1.7976931348623157e308],[1.7976931348623157e308,1.7976931348623157e308]]}");
        Assert.Equal(5, Run("solve", "--input", input, "--out", Path.Combine(dir, "failure")));
        string json = File.ReadAllText(Path.Combine(dir, "failure", "summary.json")); using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("eigenvalues").ValueKind);
        Assert.DoesNotContain("NaN", json); Assert.DoesNotContain("Infinity", json);
        foreach (string svg in Directory.GetFiles(Path.Combine(dir, "failure"), "*.svg")) ValidateSvg(svg);
        foreach (string bad in new[] { "[]", "{\"schemaVersion\":\"1\",\"name\":\"a\",\"matrix\":[[1]]}", "{\"schemaVersion\":1,\"name\":\"a\",\"matrix\":[]}", "{\"schemaVersion\":1,\"name\":\"a\",\"matrix\":[[1,2],[3,4]]}", "{\"schemaVersion\":1,\"name\":\"a\",\"matrix\":[[1e999]]}" })
        { File.WriteAllText(input, bad); Assert.Equal(2, Run("solve", "--input", input, "--out", Path.Combine(dir, "invalid"))); Assert.False(Directory.Exists(Path.Combine(dir, "invalid"))); }
    }
}

