using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectrum.Core;
namespace Spectrum.Cli;
public static class JsonExporter
{
    public static JsonSerializerOptions Settings { get; } = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };
    public static string Serialize(object value) => JsonSerializer.Serialize(value, Settings);
    public static object Case(FixtureResult f) => new { f.Name, family = f.Family, input = f.Family is null, size = f.Matrix.Order,
        seed = f.Seed?.ToString(CultureInfo.InvariantCulture), f.GeneratorVersion, f.MatrixSha256, f.Parameters };
    public static object EnvironmentInfo() => new { RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
        processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessorCount = Environment.ProcessorCount,
#if DEBUG
        configuration = "Debug"
#else
        configuration = "Release"
#endif
    };
    public static string Summary(Experiment run)
    {
        var r = run.Solver; var o = run.Options; var b = run.Bounds;
        var warnings = b.Warnings.Concat(r.Warnings).Concat(run.Accuracy?.Warnings ?? []).ToList();
        if (run.Accuracy is null) warnings.Add("Accuracy diagnostics are null because finite eigenpairs are unavailable.");
        if (run.Timings.RenderingMs is null) warnings.Add("Rendering time is null because rendering was not measured.");
        if (r.Snapshots.Any(snapshot => GershgorinBounds.Analyze(snapshot.Matrix).Disks.Any(d =>
            !d.CertifiedFinite || !double.IsFinite((d.OutwardLeft ?? double.NaN) * snapshot.Scale) ||
            !double.IsFinite((d.OutwardRight ?? double.NaN) * snapshot.Scale))))
            warnings.Add("Some snapshot bounds overflow in original units; CSV fields are empty and certifiedFinite=false.");
        return Serialize(new
        {
            schemaVersion = 1, @case = Case(run.Fixture),
            options = new { pivotPolicy = o.Policy, rtol = o.RelativeTolerance, atol = o.AbsoluteTolerance,
                maxRotations = o.MaxRotations ?? 100 * Math.Max(1, (long)run.Fixture.Matrix.Order * (run.Fixture.Matrix.Order - 1) / 2),
                maxSweeps = o.Policy == PivotPolicy.Cyclic ? (int?)o.MaxSweeps : null, o.TraceStride, o.MaxTraceSamples,
                maxSnapshots = o.MaxSnapshots ?? (run.Fixture.Matrix.Order <= 64 ? 6 : 0) },
            solver = new { r.Status, r.Converged, r.Rotations, r.PivotVisits, r.SearchComparisons, r.CompletedSweeps, r.EquivalentSweeps,
                r.Scale, r.ScalingUnderflowCount, r.InitialNormScaled, r.InitialOffNormScaled, r.FinalOffNormScaled, r.RelativeOffNorm, r.ThresholdScaled, r.EffectiveTraceStride },
            bounds = new { centerMin = b.Disks.Min(d => d.Center), centerMax = b.Disks.Max(d => d.Center), b.RadiusMin, b.RadiusMean, b.RadiusMax,
                estimateMergedIntervals = b.MergedIntervals, outwardMergedIntervals = b.MergedOutwardIntervals, b.EnvelopeWidth, b.UnionLength, b.CertifiedFinite,
                floatingPointPolicy = "Compensated radius estimates; outward scalar IEEE binary64 enclosures. Computed eigenvalues are not certified.",
                estimateComplete = b.Disks.All(d => d.Left.HasValue && d.Right.HasValue) },
            accuracy = run.Accuracy, eigenvalues = r.Eigenvalues, timings = run.Timings, environment = EnvironmentInfo(),
            numericPayloadBytes = checked(8L * run.Fixture.Matrix.Order * run.Fixture.Matrix.Order * (3 + r.Snapshots.Count)),
            approximatePeakMatrixPayloadBytes = checked(8L * run.Fixture.Matrix.Order * run.Fixture.Matrix.Order * (7 + r.Snapshots.Count)),
            timingScope = "Instrumented educational solve; generation, diagnostics and rendering excluded from solverMs.", warnings
        });
    }
    public static string Matrix(FixtureResult fixture) => Serialize(new { schemaVersion = 1, fixture.Name,
        matrix = Enumerable.Range(0, fixture.Matrix.Order).Select(i => Enumerable.Range(0, fixture.Matrix.Order).Select(j => fixture.Matrix[i, j]).ToArray()).ToArray() });
}
