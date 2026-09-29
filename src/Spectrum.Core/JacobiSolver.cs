namespace Spectrum.Core;

public static class JacobiSolver
{
    public static void ValidateOptions(JacobiOptions options, int order)
    {
        ArgumentNullException.ThrowIfNull(options); DenseMatrix.CheckedLength(order);
        if (!Enum.IsDefined(options.Policy)) throw new ArgumentException("Unknown policy.", nameof(options));
        if (!double.IsFinite(options.RelativeTolerance) || options.RelativeTolerance < 0 ||
            !double.IsFinite(options.AbsoluteTolerance) || options.AbsoluteTolerance < 0 ||
            (options.RelativeTolerance == 0 && options.AbsoluteTolerance == 0)) throw new ArgumentException("Finite nonnegative tolerances, at least one positive, required.", nameof(options));
        if (options.MaxRotations is <= 0 || options.MaxSweeps <= 0 || options.TraceStride <= 0 ||
            options.MaxTraceSamples < 0 || options.MaxTraceSamples == 1 || options.MaxTraceSamples > 4096 ||
            options.MaxSnapshots < 0 || options.MaxSnapshots == 1 || options.MaxSnapshots > 6)
            throw new ArgumentException("Invalid budgets or observation caps (trace 0 or 2..4096; snapshots 0 or 2..6).", nameof(options));
    }
    public static JacobiResult Solve(DenseMatrix input, JacobiOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(input); ValidateOptions(options, input.Order);
        int n = input.Order; long pairs = (long)n * (n - 1) / 2, cadence = Math.Max(1, pairs);
        long limit = options.MaxRotations ?? checked(100 * Math.Max(1, pairs));
        var b = input.Clone(); var v = DenseMatrix.Identity(n); double scale = 0;
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) scale = Math.Max(scale, Math.Abs(b[i, j]));
        int underflows = 0;
        if (scale > 0) for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        { double original = b[i, j]; b[i, j] /= scale; if (original != 0 && b[i, j] == 0) underflows++; }
        double norm = StableNorm.Frobenius(b), initialOff = StableNorm.OffDiagonal(b), off = initialOff;
        double threshold = scale == 0 ? options.AbsoluteTolerance : options.AbsoluteTolerance / scale + options.RelativeTolerance * norm;
        var warnings = new List<string>();
        if (options.RelativeTolerance < 16 * StableNorm.UnitRoundoff) warnings.Add("Relative tolerance is demanding (<16u); convergence is not guaranteed.");
        if (!double.IsFinite(threshold)) warnings.Add("Scaled threshold is unrepresentable (null); the requested tolerance permits immediate convergence.");
        if (underflows > 0) warnings.Add("Nonzero entries underflowed during scaling; tiny eigenvalues may lose relative accuracy.");
        var recorder = new TraceRecorder(options, n, scale, norm);
        long rotations = 0, visits = 0, comparisons = 0; int sweeps = 0;
        int? Completed() => options.Policy == PivotPolicy.Cyclic ? sweeps : null;
        recorder.Record(b, 0, 0, Completed(), initial: true);
        var monitor = new StagnationMonitor(off, norm); double estimate = off * off;
        SolverStatus status = SolverStatus.RotationLimit;
        bool Refresh(bool checkStagnation)
        {
            off = StableNorm.OffDiagonal(b); estimate = off * off;
            if (!double.IsFinite(off)) { status = SolverStatus.NumericFailure; return true; }
            if (off <= threshold) { status = SolverStatus.Converged; return true; }
            if (checkStagnation && monitor.Observe(off)) { status = SolverStatus.Stagnated; return true; }
            return false;
        }
        bool Rotate(int p, int q)
        {
            double pivot = b[p, q];
            if (!JacobiRotation.Apply(b, v, p, q)) return false;
            rotations++; estimate -= 2 * pivot * pivot;
            recorder.Record(b, rotations, visits, Completed());
            return true;
        }
        if (token.IsCancellationRequested) status = SolverStatus.Cancelled;
        else if (!Refresh(false))
        {
            if (options.Policy == PivotPolicy.MaxAbsolute)
            {
                while (true)
                {
                    if (token.IsCancellationRequested) { status = SolverStatus.Cancelled; break; }
                    if (rotations >= limit) { status = SolverStatus.RotationLimit; break; }
                    var pivot = PivotSearch.FindMaximum(b); comparisons += pivot.Inspected; visits++;
                    if (pivot.Magnitude == 0) { Refresh(false); break; }
                    Rotate(pivot.P, pivot.Q);
                    bool fullCadence = rotations % cadence == 0;
                    if ((fullCadence || estimate < 0 || Math.Sqrt(estimate) <= threshold) && Refresh(fullCadence)) break;
                }
            }
            else
            {
                bool stop = false;
                while (!stop)
                {
                    for (int p = 0; p < n - 1 && !stop; p++) for (int q = p + 1; q < n; q++)
                    {
                        if (token.IsCancellationRequested) { status = SolverStatus.Cancelled; stop = true; break; }
                        if (rotations >= limit) { status = SolverStatus.RotationLimit; stop = true; break; }
                        visits++; bool applied = Rotate(p, q);
                        // Refresh estimates during a sweep, but converge/stagnate only at its boundary.
                        if (applied && (rotations % cadence == 0 || estimate < 0 || Math.Sqrt(estimate) <= threshold))
                        { off = StableNorm.OffDiagonal(b); estimate = off * off; }
                    }
                    if (stop) break;
                    sweeps++;
                    if (Refresh(true)) break;
                    if (sweeps >= options.MaxSweeps) { status = SolverStatus.SweepLimit; break; }
                }
            }
        }
        off = StableNorm.OffDiagonal(b);
        if (status != SolverStatus.Cancelled && status != SolverStatus.NumericFailure && off <= threshold) status = SolverStatus.Converged;
        recorder.Record(b, rotations, visits, Completed(), final: true);
        int[] permutation = Enumerable.Range(0, n).OrderBy(i => b[i, i]).ThenBy(i => i).ToArray();
        var values = new double[n]; var scaledValues = new double[n]; var sorted = new DenseMatrix(n);
        for (int col = 0; col < n; col++)
        {
            int source = permutation[col], largest = 0; scaledValues[col] = b[source, source]; values[col] = scaledValues[col] * scale;
            for (int row = 1; row < n; row++) if (Math.Abs(v[row, source]) > Math.Abs(v[largest, source])) largest = row;
            double sign = v[largest, source] < 0 ? -1 : 1;
            for (int row = 0; row < n; row++) sorted[row, col] = sign * v[row, source];
        }
        if (values.Any(x => !double.IsFinite(x))) { status = SolverStatus.NumericFailure; warnings.Add("Eigenvalues are unrepresentable in original units; eigenpairs omitted."); }
        if (scale != 0 && Enumerable.Range(0, n).Any(i => scaledValues[i] != 0 && values[i] == 0))
            warnings.Add("Eigenvalue rescaling underflowed to zero; reported eigenvalues lose tiny-scale accuracy.");
        if (recorder.Samples.Any(s => s.OffNormOriginal is null)) warnings.Add("Original-unit off-norm is unrepresentable and null.");
        bool failed = status == SolverStatus.NumericFailure;
        return new(status, failed ? null : values, failed ? null : scaledValues, failed ? null : sorted, b,
            rotations, visits, comparisons, Completed(), scale, underflows, norm, initialOff, off, threshold, recorder, warnings);
    }
}
