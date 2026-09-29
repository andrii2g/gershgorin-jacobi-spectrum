using System.Diagnostics;
using System.Text;
using Spectrum.Core;
namespace Spectrum.Cli;

public sealed record TimingStatistics(double Median, double Min, double Max)
{
    public static TimingStatistics From(IReadOnlyList<double> samples)
    {
        var sorted = samples.Order().ToArray(); int mid = sorted.Length / 2;
        return new(sorted.Length % 2 == 1 ? sorted[mid] : sorted[mid - 1] / 2 + sorted[mid] / 2, sorted[0], sorted[^1]);
    }
}
public sealed record TimedSolve(double Milliseconds, SolverStatus Status, long Rotations, long PivotVisits, long SearchComparisons, double? AggregateRelativeResidual);
public static class BenchmarkRunner
{
    public static int Run(CommandLine command, TextWriter stdout, CancellationToken token)
    {
        OutputDirectory.Prepare(command.Output, command.Has("overwrite"));
        int repetitions = command.Integer("repetitions", 5), warmups = command.Integer("warmups", 1), exit = 0;
        var rows = new List<object>(); var links = new List<string>();
        var csv = new StringBuilder("family,size,seed,matrixSha256,pivot,status,rotations,pivotVisits,searchComparisons,completedSweeps,equivalentSweeps,finalRelativeOffNorm,maxNormalizedResidual,orthogonalityFrobenius,solverMedianMs,solverMinMs,solverMaxMs,gershgorinMedianMs,repetitions\n");
        foreach (string family in command.Families) foreach (int size in command.Sizes)
        {
            token.ThrowIfCancellationRequested(); var fixture = MatrixFixtures.Create(family, size, command.Seed);
            var educational = new Experiment[2]; var samples = new List<TimedSolve>[] { [], [] }; var disks = new List<double>();
            for (int policy = 0; policy < 2; policy++)
            {
                educational[policy] = ExperimentRunner.Run(fixture, new() { Policy = (PivotPolicy)policy }, token);
                var run = educational[policy]; int code = ExperimentRunner.ExitCode(run.Solver.Status); if (code == 130) return code; exit = Math.Max(exit, code);
                string id = $"{family}-{size}-{ExperimentRunner.PolicyName((PivotPolicy)policy)}";
                ExperimentRunner.Export(run, Path.Combine(command.Output, id), false);
                links.Add($"- [{id}]({id}/summary.json) · [disks]({id}/gershgorin.svg) · [trace]({id}/convergence.svg)");
            }
            for (int repetition = -warmups; repetition < repetitions; repetition++)
            {
                for (int offset = 0; offset < 2; offset++)
                {
                    token.ThrowIfCancellationRequested(); int policy = (Math.Abs(repetition) + offset) % 2;
                    var options = new JacobiOptions { Policy = (PivotPolicy)policy, MaxTraceSamples = 0, MaxSnapshots = 0 };
                    var clock = Stopwatch.StartNew(); var result = JacobiSolver.Solve(fixture.Matrix, options, token); double elapsed = clock.Elapsed.TotalMilliseconds;
                    int code = ExperimentRunner.ExitCode(result.Status); if (code == 130) return code; exit = Math.Max(exit, code);
                    var accuracy = SpectrumDiagnostics.Analyze(fixture.Matrix, result, options);
                    if (repetition >= 0) samples[policy].Add(new(elapsed, result.Status, result.Rotations, result.PivotVisits, result.SearchComparisons, accuracy?.AggregateRelativeResidual));
                }
                if (repetition >= 0) disks.Add(MeasureBounds(fixture.Matrix, token));
            }
            for (int policy = 0; policy < 2; policy++)
            {
                var run = educational[policy]; var r = run.Solver; var accuracy = run.Accuracy;
                var solverStats = TimingStatistics.From(samples[policy].Select(x => x.Milliseconds).ToArray()); var diskStats = TimingStatistics.From(disks);
                csv.Append(CsvExporter.Row(family, size, command.Seed, fixture.MatrixSha256, ExperimentRunner.PolicyName(run.Options.Policy), r.Status,
                    r.Rotations, r.PivotVisits, r.SearchComparisons, r.CompletedSweeps, r.EquivalentSweeps, r.RelativeOffNorm, accuracy?.NormalizedResidualMax,
                    accuracy?.OrthogonalityFrobenius, solverStats.Median, solverStats.Min, solverStats.Max, diskStats.Median, repetitions));
                rows.Add(new { @case = JsonExporter.Case(fixture), pivot = run.Options.Policy, r.Status, r.Rotations, r.PivotVisits, r.SearchComparisons, r.CompletedSweeps,
                    r.EquivalentSweeps, r.RelativeOffNorm, accuracy, solverStatisticsMs = solverStats, gershgorinStatisticsMs = diskStats,
                    rawSolverSamples = samples[policy], rawGershgorinMs = disks, repetitions, warmups,
                    timedOptions = new { traceSamples = 0, snapshots = 0, rtol = 1e-12, atol = 0 } });
            }
            string caseDir = Path.Combine(command.Output, $"{family}-{size}"); Directory.CreateDirectory(caseDir);
            OutputDirectory.Write(caseDir, "convergence.svg", SvgReportWriter.Convergence(educational));
            links.Add($"- [{family}-{size}: both policies]({family}-{size}/convergence.svg)");
            stdout.WriteLine($"Compared {family} n={size}: max {educational[0].Solver.Rotations}, cyclic {educational[1].Solver.Rotations} rotations.");
        }
        OutputDirectory.Write(command.Output, "comparison.csv", csv.ToString());
        OutputDirectory.Write(command.Output, "comparison.json", JsonExporter.Serialize(new { schemaVersion = 1,
            protocol = "Educational traces are separate. Timed Solve includes matrix clone and V allocation; excludes fixtures, diagnostics and output. Alternating policy order; no forced GC. Gershgorin batches include interval merging (20ms or 100000 calls). Single-threaded.",
            environment = JsonExporter.EnvironmentInfo(), rows }));
        OutputDirectory.Write(command.Output, "index.md", "# Comparison\n\n[CSV](comparison.csv) · [JSON with raw timing samples](comparison.json)\n\nBounds cost O(n²) plus O(n log n) interval merging. Maximum pivot scans inspect n(n−1)/2 candidates per rotation, so n² rotations can cost O(n⁴) search work. Cyclic sweeps cost O(n³). Equal rotation counts do not imply equal elapsed time. Timings are separate untraced repetitions, with warmups and alternating order; educational solverMs is instrumented. Residuals validate the original matrix.\n\n" + string.Join('\n', links));
        return exit;
    }
    private static double MeasureBounds(DenseMatrix matrix, CancellationToken token)
    {
        var clock = Stopwatch.StartNew(); int iterations = 0; GershgorinResult? consumed = null;
        do { token.ThrowIfCancellationRequested(); consumed = GershgorinBounds.Analyze(matrix); iterations++; }
        while (clock.Elapsed.TotalMilliseconds < 20 && iterations < 100000);
        double average = clock.Elapsed.TotalMilliseconds / iterations; GC.KeepAlive(consumed); return average;
    }
}
