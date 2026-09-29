namespace Spectrum.Core;

public sealed record TraceSample(long Rotation, long PivotVisit, int? CompletedSweeps, double OffNormScaled,
    double? OffNormOriginal, double RelativeOffNorm, bool Direct = true);
public sealed class MatrixSnapshot
{
    private readonly DenseMatrix matrix;
    public long Rotation { get; }
    public int Order => matrix.Order;
    public double Scale { get; }
    public string Label { get; }
    public DenseMatrix Matrix => matrix.Clone();
    public MatrixSnapshot(long rotation, DenseMatrix scaledMatrix, double scale, string label)
    { Rotation = rotation; matrix = scaledMatrix.Clone(); Scale = scale; Label = label; }
}
/// <summary>All collections are read-only; matrix getters return independent copies.</summary>
public sealed class JacobiResult
{
    private readonly DenseMatrix? vectors;
    private readonly DenseMatrix transformed;
    public SolverStatus Status { get; }
    public bool Converged => Status == SolverStatus.Converged;
    public IReadOnlyList<double>? Eigenvalues { get; }
    public IReadOnlyList<double>? EigenvaluesScaled { get; }
    public DenseMatrix? Eigenvectors => vectors?.Clone();
    public DenseMatrix Transformed => transformed.Clone();
    public long Rotations { get; }
    public long PivotVisits { get; }
    public long SearchComparisons { get; }
    public int? CompletedSweeps { get; }
    public double EquivalentSweeps { get; }
    public double Scale { get; }
    public int ScalingUnderflowCount { get; }
    public double InitialNormScaled { get; }
    public double InitialOffNormScaled { get; }
    public double FinalOffNormScaled { get; }
    public double RelativeOffNorm => InitialNormScaled == 0 ? 0 : FinalOffNormScaled / InitialNormScaled;
    public double? ThresholdScaled { get; }
    public IReadOnlyList<TraceSample> Trace { get; }
    public IReadOnlyList<MatrixSnapshot> Snapshots { get; }
    public long EffectiveTraceStride { get; }
    public IReadOnlyList<string> Warnings { get; }
    internal JacobiResult(SolverStatus status, double[]? eigenvalues, double[]? scaledValues, DenseMatrix? eigenvectors,
        DenseMatrix finalMatrix, long rotations, long visits, long comparisons, int? sweeps, double scale, int underflows,
        double norm, double initialOff, double finalOff, double threshold, TraceRecorder recorder, List<string> warnings)
    {
        Status = status; Eigenvalues = eigenvalues is null ? null : Array.AsReadOnly(eigenvalues);
        EigenvaluesScaled = scaledValues is null ? null : Array.AsReadOnly(scaledValues);
        vectors = eigenvectors; transformed = finalMatrix; Rotations = rotations; PivotVisits = visits;
        SearchComparisons = comparisons; CompletedSweeps = sweeps; Scale = scale; ScalingUnderflowCount = underflows;
        InitialNormScaled = norm; InitialOffNormScaled = initialOff; FinalOffNormScaled = finalOff;
        ThresholdScaled = GershgorinBounds.Finite(threshold); Trace = recorder.Samples.AsReadOnly();
        Snapshots = recorder.Snapshots.AsReadOnly(); EffectiveTraceStride = recorder.Stride; Warnings = warnings.AsReadOnly();
        long pairs = (long)finalMatrix.Order * (finalMatrix.Order - 1) / 2;
        EquivalentSweeps = pairs == 0 ? 0 : (double)rotations / pairs;
    }
}
