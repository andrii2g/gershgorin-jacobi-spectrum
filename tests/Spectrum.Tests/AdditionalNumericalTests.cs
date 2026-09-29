using Spectrum.Core;
namespace Spectrum.Tests;
public sealed class AdditionalNumericalTests
{
    [Theory] [InlineData(PivotPolicy.MaxAbsolute)] [InlineData(PivotPolicy.Cyclic)] public async Task CancellationDuringSolveKeepsCompleteRotations(PivotPolicy policy)
    {
        var matrix = MatrixFixtures.Create("random", 96).Matrix; var original = matrix.CopyData();
        using var cancellation = new CancellationTokenSource(); using var started = new ManualResetEventSlim();
        var task = Task.Run(() => { started.Set(); return JacobiSolver.Solve(matrix, new() { Policy = policy, RelativeTolerance = 1e-30, MaxTraceSamples = 0, MaxSnapshots = 0, MaxSweeps = int.MaxValue }, cancellation.Token); });
        started.Wait(); await Task.Delay(20); cancellation.Cancel(); var result = await task;
        Assert.Equal(SolverStatus.Cancelled, result.Status); Assert.True(result.Rotations > 0);
        var final = result.Transformed;
        for (int i = 0; i < matrix.Order; i++) for (int j = 0; j < matrix.Order; j++) Assert.Equal(final[i, j], final[j, i]);
        Assert.InRange(SpectrumDiagnostics.Orthogonality(result.Eigenvectors!), 0, 5e-11); Assert.Equal(original, matrix.CopyData());
        Assert.Equal(StableNorm.OffDiagonal(final), result.FinalOffNormScaled);
    }
    [Theory] [InlineData(PivotPolicy.MaxAbsolute)] [InlineData(PivotPolicy.Cyclic)] public void Order64Ceilings(PivotPolicy policy)
    {
        foreach (string family in MatrixFixtures.Families)
        {
            var fixture = MatrixFixtures.Create(family, 64); var options = new JacobiOptions { Policy = policy, MaxTraceSamples = 0, MaxSnapshots = 0 };
            var result = JacobiSolver.Solve(fixture.Matrix, options); var d = SpectrumDiagnostics.Analyze(fixture.Matrix, result, options)!;
            Assert.Equal(SolverStatus.Converged, result.Status); Assert.InRange(d.AggregateRelativeResidual, 0, 5e-11);
            Assert.InRange(d.OrthogonalityFrobenius, 0, 5e-11); Assert.InRange(d.RelativeReconstruction, 0, 1e-10);
        }
    }
    [Fact] public void SnapshotCopiesAndObservationDoNotChangeSolve()
    {
        var a = MatrixFixtures.Create("random", 16).Matrix;
        var observed = JacobiSolver.Solve(a, new() { MaxTraceSamples = 4 }); var unobserved = JacobiSolver.Solve(a, new() { MaxTraceSamples = 0, MaxSnapshots = 0 });
        Assert.Equal(observed.Eigenvalues, unobserved.Eigenvalues); Assert.Equal(observed.Rotations, unobserved.Rotations);
        foreach (var snapshot in observed.Snapshots)
        {
            var matching = observed.Trace.FirstOrDefault(s => s.Rotation == snapshot.Rotation);
            if (matching is not null) Assert.Equal(matching.OffNormScaled, StableNorm.OffDiagonal(snapshot.Matrix));
            Assert.NotSame(snapshot.Matrix, snapshot.Matrix);
        }
        Assert.NotSame(observed.Eigenvectors, observed.Eigenvectors);
    }
    [Fact] public void ExactDyadicEnclosureAndSeparatedDisks()
    {
        var matrix = DenseMatrix.FromRows([1, 0.125, 0.25], [0.125, 4, 0.5], [0.25, 0.5, 8]);
        var bounds = GershgorinBounds.Analyze(matrix); Assert.Equal(3, bounds.MergedIntervals.Count);
        double[] radii = [0.375, 0.625, 0.75];
        for (int i = 0; i < 3; i++) { Assert.Equal(radii[i], bounds.Disks[i].RadiusEstimate); Assert.True(bounds.Disks[i].RadiusUpper >= radii[i]); }
        Assert.Equal(3.5, bounds.UnionLength); Assert.Equal(8.125, bounds.EnvelopeWidth);
    }
}
