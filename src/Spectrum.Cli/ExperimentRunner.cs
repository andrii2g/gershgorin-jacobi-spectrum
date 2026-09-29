using System.Diagnostics;
using Spectrum.Core;
namespace Spectrum.Cli;
public sealed record RunTimings(double? GershgorinMs, double? SolverMs, double? DiagnosticsMs, double? RenderingMs);
public sealed record Experiment(FixtureResult Fixture, JacobiOptions Options, GershgorinResult Bounds, JacobiResult Solver, AccuracyResult? Accuracy, RunTimings Timings);
public static class ExperimentRunner
{
    public static string PolicyName(PivotPolicy policy) => policy == PivotPolicy.MaxAbsolute ? "max" : "cyclic";
    public static Experiment Run(FixtureResult fixture, JacobiOptions options, CancellationToken token)
    {
        var watch = Stopwatch.StartNew(); var bounds = GershgorinBounds.Analyze(fixture.Matrix); double boundsMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var result = JacobiSolver.Solve(fixture.Matrix, options, token); double solverMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); var accuracy = SpectrumDiagnostics.Analyze(fixture.Matrix, result, options); double accuracyMs = watch.Elapsed.TotalMilliseconds;
        return new(fixture, options, bounds, result, accuracy, new(boundsMs, solverMs, accuracyMs, null));
    }
    public static void Export(Experiment run, string output, bool replay)
    {
        Directory.CreateDirectory(output); var watch = Stopwatch.StartNew();
        OutputDirectory.Write(output, "eigenpairs.csv", CsvExporter.Eigenpairs(run));
        OutputDirectory.Write(output, "disks.csv", CsvExporter.Disks(run));
        OutputDirectory.Write(output, "convergence.csv", CsvExporter.Convergence(run.Solver));
        OutputDirectory.Write(output, "gershgorin.svg", SvgReportWriter.Disks(run));
        OutputDirectory.Write(output, "convergence.svg", SvgReportWriter.Convergence(run));
        if (run.Solver.Snapshots.Count > 0) OutputDirectory.Write(output, "snapshots.svg", SvgReportWriter.Snapshots(run));
        else OutputDirectory.RemoveGenerated(output, "snapshots.svg");
        if (replay)
        {
            OutputDirectory.Write(output, "matrix.json", JsonExporter.Matrix(run.Fixture));
            OutputDirectory.Write(output, "eigenvectors.csv", CsvExporter.Eigenvectors(run.Solver));
        }
        else { OutputDirectory.RemoveGenerated(output, "matrix.json"); OutputDirectory.RemoveGenerated(output, "eigenvectors.csv"); }
        run = run with { Timings = run.Timings with { RenderingMs = watch.Elapsed.TotalMilliseconds } };
        OutputDirectory.Write(output, "summary.json", JsonExporter.Summary(run));
    }
    public static int ExitCode(SolverStatus status) => status switch { SolverStatus.Converged => 0, SolverStatus.NumericFailure => 5, SolverStatus.Cancelled => 130, _ => 4 };
}
