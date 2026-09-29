using System.Globalization;
using Spectrum.Core;
namespace Spectrum.Cli;

public sealed record CommandLine(string Command, IReadOnlyDictionary<string, string> Values)
{
    public bool Has(string key) => Values.ContainsKey(key);
    public string Text(string key, string fallback) => Values.TryGetValue(key, out string? value) ? value : fallback;
    public int Integer(string key, int fallback) => Has(key) ? int.Parse(Values[key], NumberStyles.Integer, CultureInfo.InvariantCulture) : fallback;
    public long Long(string key, long fallback) => Has(key) ? long.Parse(Values[key], NumberStyles.Integer, CultureInfo.InvariantCulture) : fallback;
    public ulong Seed => ulong.Parse(Text("seed", "42"), NumberStyles.None, CultureInfo.InvariantCulture);
    public double Number(string key, double fallback) => Has(key) ? double.Parse(Values[key], NumberStyles.Float, CultureInfo.InvariantCulture) : fallback;
    public string Output => Text("out", Command == "demo" ? "artifacts/demo" : Command == "compare" ? "artifacts/comparison" : "artifacts/run");
    public int[] Sizes => Text("sizes", "8,16,32,64").Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
    public string[] Families => Text("families", string.Join(',', MatrixFixtures.Families)).Split(',');
    public JacobiOptions Options => new()
    {
        Policy = Text("pivot", "max") == "max" ? PivotPolicy.MaxAbsolute : PivotPolicy.Cyclic,
        RelativeTolerance = Number("rtol", 1e-12), AbsoluteTolerance = Number("atol", 0),
        MaxRotations = Has("max-rotations") ? Long("max-rotations", 1) : null, MaxSweeps = Integer("max-sweeps", 100),
        TraceStride = Long("trace-stride", 1), MaxTraceSamples = Integer("max-trace-samples", 4096),
        MaxSnapshots = Has("snapshots") ? Integer("snapshots", 6) : null
    };
    public static CommandLine Parse(string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && args[0] is "help" or "--help" or "-h")) return new("help", new Dictionary<string, string>());
        string command = args[0];
        if (args.Length == 2 && args[1] == "--help" && command is "solve" or "demo" or "compare") return new("help", new Dictionary<string, string>());
        string allowed = command switch
        {
            "solve" => "family input size seed pivot rtol atol max-rotations max-sweeps trace-stride max-trace-samples snapshots out overwrite allow-large diagonal off-diagonal cluster-delta epsilon",
            "demo" => "out overwrite",
            "compare" => "sizes seed families repetitions warmups out overwrite allow-large",
            _ => throw new ArgumentException("Unknown command. Use help.")
        };
        var keys = allowed.Split(' ').ToHashSet(StringComparer.Ordinal); var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || !keys.Contains(args[i][2..])) throw new ArgumentException($"Unknown option: {args[i]}");
            string key = args[i][2..]; if (values.ContainsKey(key)) throw new ArgumentException($"Duplicate option: --{key}");
            string value = "true";
            if (key is not "overwrite" and not "allow-large")
            { if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Missing value: --{key}"); value = args[i]; }
            values.Add(key, value);
        }
        var result = new CommandLine(command, values); result.Validate(); return result;
    }
    public void CheckSize(int n)
    {
        DenseMatrix.CheckedLength(n);
        if (n > 512 && !Has("allow-large")) throw new ArgumentException("Orders above 512 require --allow-large.");
    }
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Output)) throw new ArgumentException("Output path cannot be empty.");
        if (Command == "demo") return;
        _ = Seed;
        if (Command == "compare")
        {
            int[] sizes = Sizes; foreach (int n in sizes) CheckSize(n);
            if (!sizes.SequenceEqual(sizes.Distinct().Order())) throw new ArgumentException("Sizes must be ascending and distinct.");
            if (Families.Length != Families.Distinct().Count() || Families.Any(f => !MatrixFixtures.Families.Contains(f))) throw new ArgumentException("Families must be known and distinct.");
            if (Integer("repetitions", 5) < 1 || Integer("warmups", 1) < 0) throw new ArgumentException("Repetitions must be positive and warmups nonnegative.");
            return;
        }
        if (Text("pivot", "max") is not "max" and not "cyclic") throw new ArgumentException("Pivot must be max or cyclic.");
        if (Has("max-sweeps") && Options.Policy == PivotPolicy.MaxAbsolute) throw new ArgumentException("--max-sweeps is cyclic-only.");
        if (Has("input"))
        {
            if (new[] { "family", "size", "seed", "diagonal", "off-diagonal", "cluster-delta", "epsilon" }.Any(Has)) throw new ArgumentException("Input cannot be combined with fixture options.");
        }
        else
        {
            CheckSize(Integer("size", 16)); string family = Text("family", "random");
            if (!MatrixFixtures.Families.Contains(family)) throw new ArgumentException("Unknown family.");
            foreach (var (key, owner) in new[] { ("diagonal", "toeplitz"), ("off-diagonal", "toeplitz"), ("cluster-delta", "clustered"), ("epsilon", "near-diagonal") })
                if (Has(key) && owner != family) throw new ArgumentException($"--{key} only applies to {owner}.");
            foreach (string key in new[] { "diagonal", "off-diagonal", "cluster-delta", "epsilon" })
                if (Has(key) && (!double.IsFinite(Number(key, 0)) || ((key is "epsilon" or "cluster-delta") && Number(key, 0) < 0))) throw new ArgumentException($"Invalid --{key}.");
        }
        JacobiSolver.ValidateOptions(Options, Integer("size", 16));
    }
    public const string Help = """
gershgorin-jacobi-spectrum (.NET 10)
Usage: spectrum help | demo | solve | compare [options]

solve: --family dominant|toeplitz|clustered|near-diagonal|random (random)
       --size N (16) --seed UInt64 (42), OR --input path.json
       --pivot max|cyclic (max) --rtol 1e-12 --atol 0
       --max-rotations N (100*n*(n-1)/2; scalar 100)
       --max-sweeps N (100, cyclic only)
       --trace-stride N (1) --max-trace-samples N (4096; 0 or 2..4096)
       --snapshots N (6 for n<=64, otherwise 0; explicit 0 or 2..6)
       --diagonal 2 --off-diagonal -1 (toeplitz only)
       --cluster-delta 1e-6 (clustered only) --epsilon 1e-6 (near-diagonal only)
       --out path (artifacts/run) --overwrite --allow-large
demo:  analytic and random 6x6, both pivots
       --out path (artifacts/demo) --overwrite
compare: --sizes 8,16,32,64 --families dominant,toeplitz,clustered,near-diagonal,random
         --seed 42 --repetitions 5 --warmups 1
         --out path (artifacts/comparison) --overwrite --allow-large

Input: schemaVersion=1, name string, matrix finite exactly symmetric square rows.
Numbers use invariant decimal notation. Orders >512 require --allow-large.
Existing nonempty output requires --overwrite; unrelated files are preserved.
Files: summary.json, eigenpairs.csv, disks.csv, convergence.csv,
       gershgorin.svg, convergence.svg, snapshots.svg when enabled.
Demo/input additionally export matrix.json and eigenvectors.csv.
Exit: 0 converged/help; 2 invalid input; 4 unconverged; 5 numeric failure;
      6 IO failure; 130 cancelled. Ctrl+C cancels at rotation boundaries.
Timings in solve/demo include observation; compare separately times untraced solves.
Floating-point disks and numerical containment are not eigenvalue certification.
""";
}
