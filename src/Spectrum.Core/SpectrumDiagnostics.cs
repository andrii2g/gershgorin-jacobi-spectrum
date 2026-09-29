namespace Spectrum.Core;

public sealed record EigenpairDiagnostic(int Index, double? ResidualAbsolute, double ResidualNormalized,
    double VectorNorm, double DistanceToOriginalUnionScaled, bool ContainedWithTolerance);
public sealed record AccuracyResult(IReadOnlyList<EigenpairDiagnostic> Eigenpairs, double? AbsoluteResidualMax,
    double NormalizedResidualMax, double AggregateRelativeResidual, double OrthogonalityFrobenius,
    double RelativeReconstruction, double TraceDriftScaled, double TraceDriftRelative,
    double FrobeniusDriftScaled, double FrobeniusDriftRelative, double? ContainmentToleranceScaled,
    int StrictContainedCount, int ToleranceContainedCount, double MaxDistanceScaled, IReadOnlyList<string> Warnings);
public static class SpectrumDiagnostics
{
    public static double Orthogonality(DenseMatrix vectors)
    {
        int n = vectors.Order; var norm = new StableNorm.Accumulator();
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            norm.Add(StableNorm.Sum(Enumerable.Range(0, n).Select(k => vectors[k, i] * vectors[k, j])) - (i == j ? 1 : 0));
        return norm.Norm;
    }
    public static AccuracyResult? Analyze(DenseMatrix original, JacobiResult result, JacobiOptions options)
    {
        var v = result.Eigenvectors; if (v is null || result.EigenvaluesScaled is null) return null;
        int n = original.Order; var b = original.Clone(); var final = result.Transformed;
        // Validate the exported eigenvalues, including their final rescaling roundoff.
        var scaledValues = result.Eigenvalues!.Select(value => result.Scale == 0 ? 0 : value / result.Scale).ToArray();
        if (result.Scale != 0) for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) b[i, j] /= result.Scale;
        var bounds = GershgorinBounds.Analyze(b); var entries = new List<EigenpairDiagnostic>(); var warnings = new List<string>();
        var residual = new StableNorm.Accumulator(); var reconstruction = new StableNorm.Accumulator();
        double norm = StableNorm.Frobenius(b), divisor = norm == 0 ? 1 : norm;
        double tolerance = 10 * ((result.Scale == 0 ? options.AbsoluteTolerance : options.AbsoluteTolerance / result.Scale) + options.RelativeTolerance * norm) + 64 * StableNorm.UnitRoundoff * norm;
        if (!double.IsFinite(tolerance)) warnings.Add("Containment tolerance is unrepresentable (null); finite distances satisfy the requested tolerance.");
        int strict = 0, contained = 0;
        for (int col = 0; col < n; col++)
        {
            var ri = new StableNorm.Accumulator(); var vn = new StableNorm.Accumulator();
            for (int row = 0; row < n; row++)
            {
                double value = StableNorm.Sum(Enumerable.Range(0, n).Select(k => b[row, k] * v[k, col])) - scaledValues[col] * v[row, col];
                ri.Add(value); residual.Add(value); vn.Add(v[row, col]);
            }
            double distance = GershgorinBounds.DistanceToUnion(scaledValues[col], bounds.MergedIntervals);
            if (distance == 0) strict++; if (distance <= tolerance) contained++;
            double? absolute = GershgorinBounds.Finite(ri.Norm * result.Scale);
            if (absolute is null) warnings.Add($"Eigenpair {col}: absolute residual is unrepresentable and null.");
            entries.Add(new(col, absolute, ri.Norm / divisor, vn.Norm, distance, distance <= tolerance));
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            reconstruction.Add(b[i, j] - StableNorm.Sum(Enumerable.Range(0, n).Select(k => v[i, k] * scaledValues[k] * v[j, k])));
        double trace0 = StableNorm.Sum(Enumerable.Range(0, n).Select(i => b[i, i]));
        double trace1 = StableNorm.Sum(Enumerable.Range(0, n).Select(i => final[i, i]));
        double td = Math.Abs(trace1 - trace0), fd = Math.Abs(StableNorm.Frobenius(final) - norm);
        return new(entries.AsReadOnly(), entries.All(e => e.ResidualAbsolute.HasValue) ? entries.Max(e => e.ResidualAbsolute) : null,
            entries.Max(e => e.ResidualNormalized), residual.Norm / divisor, Orthogonality(v), reconstruction.Norm / divisor,
            td, td / (Math.Abs(trace0) > 0 ? Math.Abs(trace0) : divisor), fd, fd / divisor, GershgorinBounds.Finite(tolerance),
            strict, contained, entries.Max(e => e.DistanceToOriginalUnionScaled), warnings.AsReadOnly());
    }
}
