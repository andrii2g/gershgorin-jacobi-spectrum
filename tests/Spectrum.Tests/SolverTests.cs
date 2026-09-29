using Spectrum.Core;
namespace Spectrum.Tests;

public sealed class SolverTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var policy in Enum.GetValues<PivotPolicy>())
        {
            yield return [policy, DenseMatrix.FromRows([5]), new double[] { 5 }];
            yield return [policy, DenseMatrix.FromRows([0, 0], [0, 0]), new double[] { 0, 0 }];
            yield return [policy, DenseMatrix.FromRows([3, 0, 0, 0], [0, -2, 0, 0], [0, 0, 3, 0], [0, 0, 0, 0]), new double[] { -2, 0, 3, 3 }];
            yield return [policy, DenseMatrix.FromRows([2, 1], [1, 2]), new double[] { 1, 3 }];
            yield return [policy, DenseMatrix.FromRows([2, -1], [-1, 2]), new double[] { 1, 3 }];
            yield return [policy, DenseMatrix.FromRows([1, 2], [2, 4]), new double[] { 0, 5 }];
            yield return [policy, DenseMatrix.FromRows([1, 1e-150], [1e-150, 2]), new double[] { 1, 2 }];
            yield return [policy, DenseMatrix.FromRows([2, 1, 0, 0, 0, 0], [1, 2, 0, 0, 0, 0], [0, 0, 6, -1, 0, 0], [0, 0, -1, 6, 0, 0], [0, 0, 0, 0, -2, 0], [0, 0, 0, 0, 0, 4]), new double[] { -2, 1, 3, 4, 5, 7 }];
            foreach (int n in new[] { 2, 3, 8, 32 })
            {
                var data = new double[n * n]; for (int i = 0; i < n; i++) { data[i * n + i] = 2; if (i + 1 < n) data[i * n + i + 1] = data[(i + 1) * n + i] = -1; }
                yield return [policy, DenseMatrix.FromRowMajor(n, data), Enumerable.Range(1, n).Select(k => 2 - 2 * Math.Cos(k * Math.PI / (n + 1))).ToArray()];
            }
        }
    }
    [Theory] [MemberData(nameof(Cases))] public void Analytical(PivotPolicy policy, DenseMatrix a, double[] expected)
    {
        var before = a.CopyData(); var r = JacobiSolver.Solve(a, new() { Policy = policy });
        Assert.Equal(SolverStatus.Converged, r.Status); Assert.Equal(before, a.CopyData());
        Assert.True(r.FinalOffNormScaled <= r.ThresholdScaled);
        var v = r.Eigenvectors!; double norm = StableNorm.Frobenius(a);
        for (int k = 0; k < a.Order; k++)
        {
            Assert.InRange(Math.Abs(r.Eigenvalues![k] - expected[k]), 0, a.Order <= 2 ? 1e-14 : 5e-10 * Math.Max(1, norm));
            for (int i = 0; i < a.Order; i++)
            {
                double av = Enumerable.Range(0, a.Order).Sum(j => a[i, j] * v[j, k]);
                Assert.InRange(Math.Abs(av - r.Eigenvalues[k] * v[i, k]), 0, 5e-11 * Math.Max(1, norm));
            }
            int largest = Enumerable.Range(0, a.Order).OrderByDescending(i => Math.Abs(v[i, k])).First(); Assert.True(v[largest, k] >= 0);
        }
    }
    public static DenseMatrix Dense(int n)
    {
        var random = new DeterministicRandom(42); var data = new double[n * n];
        for (int i = 0; i < n; i++) for (int j = i; j < n; j++) data[i * n + j] = data[j * n + i] = random.UniformSigned();
        return DenseMatrix.FromRowMajor(n, data);
    }
    [Fact] public void BudgetsAndCounts()
    {
        var a = Dense(5); var r = JacobiSolver.Solve(a, new() { MaxRotations = 1 });
        Assert.Equal(SolverStatus.RotationLimit, r.Status); Assert.Equal(1, r.Rotations); Assert.Equal(10, r.SearchComparisons);
        r = JacobiSolver.Solve(a, new() { Policy = PivotPolicy.Cyclic, MaxSweeps = 1 });
        Assert.Equal(SolverStatus.SweepLimit, r.Status); Assert.Equal(1, r.CompletedSweeps); Assert.Equal(10, r.PivotVisits);
        r = JacobiSolver.Solve(a, new() { Policy = PivotPolicy.Cyclic, MaxRotations = 1 }); Assert.Equal(0, r.CompletedSweeps);
        foreach (var policy in Enum.GetValues<PivotPolicy>())
        {
            r = JacobiSolver.Solve(DenseMatrix.FromRows([2, 1], [1, 2]), new() { Policy = policy, MaxRotations = 1 });
            Assert.Equal(SolverStatus.Converged, r.Status); Assert.Equal(1, r.Rotations);
        }
        var tie = PivotSearch.FindMaximum(DenseMatrix.FromRows([0, -2, 2], [-2, 0, 1], [2, 1, 0]));
        Assert.Equal(new Pivot(0, 1, 2, 3), tie);
    }
    [Fact] public void CancellationStagnationAndValidation()
    {
        var r = JacobiSolver.Solve(Dense(5), new(), new CancellationToken(true)); Assert.Equal(SolverStatus.Cancelled, r.Status); Assert.Equal(0, r.Rotations);
        var m = new StagnationMonitor(1, 2); Assert.False(m.Observe(1)); Assert.False(m.Observe(1)); Assert.True(m.Observe(1));
        Assert.False(m.Observe(0.5));
        Assert.Throws<ArgumentException>(() => JacobiSolver.Solve(Dense(2), new() { RelativeTolerance = double.NaN }));
        Assert.Throws<ArgumentException>(() => JacobiSolver.Solve(Dense(2), new() { MaxRotations = -1 }));
        Assert.Throws<ArgumentException>(() => JacobiSolver.Solve(Dense(2), new() { RelativeTolerance = 0 }));
    }
    [Fact] public void TraceCapsAndDirectGate()
    {
        var r = JacobiSolver.Solve(Dense(16), new() { MaxTraceSamples = 8, MaxSnapshots = 4 });
        Assert.InRange(r.Trace.Count, 2, 8); Assert.Equal(0, r.Trace[0].Rotation); Assert.Equal(r.Rotations, r.Trace[^1].Rotation);
        Assert.Equal(r.FinalOffNormScaled, StableNorm.OffDiagonal(r.Transformed)); Assert.True(r.FinalOffNormScaled <= r.ThresholdScaled);
        Assert.Equal(4, r.Snapshots.Count); Assert.Equal(r.Rotations, r.Snapshots[^1].Rotation); Assert.True(r.EffectiveTraceStride > 1);
        r = JacobiSolver.Solve(DenseMatrix.Identity(65), new()); Assert.Empty(r.Snapshots); Assert.Single(r.Trace);
    }
    [Fact] public void UnderflowAndOverflow()
    {
        var r = JacobiSolver.Solve(DenseMatrix.FromRows([1e300, 0], [0, 1e-300]), new()); Assert.Equal(1, r.ScalingUnderflowCount);
        r = JacobiSolver.Solve(DenseMatrix.FromRows([double.MaxValue, double.MaxValue], [double.MaxValue, double.MaxValue]), new());
        Assert.Equal(SolverStatus.NumericFailure, r.Status); Assert.Null(r.Eigenvalues);
        r = JacobiSolver.Solve(DenseMatrix.FromRows([1e-300, 1e-300], [1e-300, 1e-300]), new() { AbsoluteTolerance = 1e300 });
        Assert.Equal(SolverStatus.Converged, r.Status); Assert.Null(r.ThresholdScaled);
    }
}
