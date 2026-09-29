namespace Spectrum.Core;
internal sealed class TraceRecorder(JacobiOptions options, int order, double scale, double norm)
{
    public List<TraceSample> Samples { get; } = [];
    public List<MatrixSnapshot> Snapshots { get; } = [];
    public long Stride { get; private set; } = options.TraceStride;
    private readonly int snapshotCap = options.MaxSnapshots ?? (order <= 64 ? 6 : 0);
    public void Record(DenseMatrix matrix, long rotation, long visits, int? sweeps, bool initial = false, bool final = false)
    {
        if (options.MaxTraceSamples > 0 && (initial || final || rotation % Stride == 0))
        {
            double off = StableNorm.OffDiagonal(matrix);
            var sample = new TraceSample(rotation, visits, sweeps, off, GershgorinBounds.Finite(off * scale), norm == 0 ? 0 : off / norm);
            if (Samples.Count > 0 && Samples[^1].Rotation == rotation) Samples[^1] = sample;
            else
            {
                if (Samples.Count >= options.MaxTraceSamples - 1 && !final)
                {
                    // Keep initial, alternate interior entries, and one reserved final slot.
                    var kept = Samples.Where((_, i) => i == 0 || i % 2 == 0).ToArray(); Samples.Clear(); Samples.AddRange(kept);
                    Stride = Stride <= long.MaxValue / 2 ? Stride * 2 : long.MaxValue;
                    
                }
                if (final && Samples.Count >= options.MaxTraceSamples) Samples.RemoveAt(Samples.Count - 1);
                if (final || Samples.Count < options.MaxTraceSamples - 1) Samples.Add(sample);
            }
        }
        if (snapshotCap == 0) return;
        bool powerOfTwo = rotation > 0 && (rotation & (rotation - 1)) == 0;
        if (initial || final || (powerOfTwo && Snapshots.Count < snapshotCap - 1))
        {
            string label = final ? "final (before eigenpair sorting)" : initial ? "initial" : "intermediate";
            if (final && Snapshots.Count >= snapshotCap) Snapshots.RemoveAt(Snapshots.Count - 1);
            Snapshots.Add(new(rotation, matrix, scale, label));
        }
    }
}

