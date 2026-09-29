namespace Spectrum.Core;

public enum PivotPolicy { MaxAbsolute, Cyclic }
public enum SolverStatus { Converged, RotationLimit, SweepLimit, Stagnated, Cancelled, NumericFailure }

// Numerical options; null snapshot cap selects six for n<=64, otherwise zero.
public sealed record JacobiOptions
{
    public PivotPolicy Policy { get; init; } = PivotPolicy.MaxAbsolute;
    public double RelativeTolerance { get; init; } = 1e-12;
    public double AbsoluteTolerance { get; init; }
    public long? MaxRotations { get; init; }
    public int MaxSweeps { get; init; } = 100;
    public long TraceStride { get; init; } = 1;
    public int MaxTraceSamples { get; init; } = 4096;
    public int? MaxSnapshots { get; init; }
}

