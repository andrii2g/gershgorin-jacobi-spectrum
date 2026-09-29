using Spectrum.Core;
namespace Spectrum.Tests;
public sealed class FixtureDiagnosticsTests
{
    public static IEnumerable<object[]> Grid() => from family in MatrixFixtures.Families from n in new[] { 4, 8, 16, 32 }
        from policy in Enum.GetValues<PivotPolicy>() select new object[] { family, n, policy };
    [Theory] [MemberData(nameof(Grid))] public void FamilyGrid(string family, int n, PivotPolicy policy)
    {
        var f = MatrixFixtures.Create(family, n); var options = new JacobiOptions { Policy = policy }; var r = JacobiSolver.Solve(f.Matrix, options);
        Assert.Equal(SolverStatus.Converged, r.Status); Assert.Equal(0, r.ScalingUnderflowCount);
        var d = SpectrumDiagnostics.Analyze(f.Matrix, r, options)!;
        Assert.InRange(d.AggregateRelativeResidual, 0, 5e-11); Assert.InRange(d.OrthogonalityFrobenius, 0, 5e-11);
        Assert.InRange(d.RelativeReconstruction, 0, 1e-10); Assert.Equal(n, d.ToleranceContainedCount);
        Assert.InRange(d.TraceDriftScaled, 0, 128 * StableNorm.UnitRoundoff * n * Math.Max(1, r.InitialNormScaled));
        if (f.AnalyticalSpectrum is not null) for (int i = 0; i < n; i++) Assert.InRange(Math.Abs(r.Eigenvalues![i] - f.AnalyticalSpectrum[i]), 0, 5e-10 * Math.Max(1, StableNorm.Frobenius(f.Matrix)));
        Assert.Equal(f.MatrixSha256, MatrixFixtures.Create(family, n).MatrixSha256);
        if (family == "dominant") Assert.All(GershgorinBounds.Analyze(f.Matrix).Disks, disk => Assert.True(disk.Left > 0));
    }
    [Theory] [InlineData(1e-200)] [InlineData(1e200)] public void CrossScale(double scale)
    {
        var f = MatrixFixtures.Create("toeplitz", 8); var a = DenseMatrix.FromRowMajor(8, f.Matrix.CopyData().Select(x => x * scale).ToArray());
        foreach (var policy in Enum.GetValues<PivotPolicy>())
        {
            var options = new JacobiOptions { Policy = policy }; var r = JacobiSolver.Solve(a, options); var d = SpectrumDiagnostics.Analyze(a, r, options)!;
            Assert.Equal(SolverStatus.Converged, r.Status); Assert.InRange(d.AggregateRelativeResidual, 0, 5e-11);
            for (int i = 0; i < 8; i++) Assert.InRange(Math.Abs(r.Eigenvalues![i] / scale - f.AnalyticalSpectrum![i]), 0, 5e-10);
        }
    }
    [Theory] [InlineData(PivotPolicy.MaxAbsolute)] [InlineData(PivotPolicy.Cyclic)] public void RepeatedSpectrum(PivotPolicy policy)
    {
        var f = MatrixFixtures.Create("clustered", 32, clusterDelta: 0); var o = new JacobiOptions { Policy = policy }; var r = JacobiSolver.Solve(f.Matrix, o);
        var d = SpectrumDiagnostics.Analyze(f.Matrix, r, o)!; Assert.Equal(SolverStatus.Converged, r.Status);
        Assert.InRange(f.ConstructionOrthogonality!.Value, 0, 5e-11); Assert.InRange(d.AggregateRelativeResidual, 0, 5e-11); Assert.InRange(d.OrthogonalityFrobenius, 0, 5e-11);
    }
    [Fact] public void ContainmentAloneDoesNotValidateEigenpairs()
    {
        var a = DenseMatrix.FromRows([2, 1], [1, 2]); var o = new JacobiOptions { AbsoluteTolerance = 100 };
        var r = JacobiSolver.Solve(a, o); var d = SpectrumDiagnostics.Analyze(a, r, o)!;
        Assert.Equal(2, d.StrictContainedCount); Assert.True(d.AggregateRelativeResidual > 0.4);
    }
    [Fact] public void ZeroAndHashSignedZero()
    {
        var a = DenseMatrix.FromRows([0, -0.0], [-0.0, 0]); var r = JacobiSolver.Solve(a, new()); var d = SpectrumDiagnostics.Analyze(a, r, new())!;
        Assert.Equal(0, d.AggregateRelativeResidual); Assert.Equal(0, d.RelativeReconstruction); Assert.Equal(0, d.OrthogonalityFrobenius);
        Assert.Equal(MatrixFixtures.Hash(a), MatrixFixtures.Hash(DenseMatrix.FromRows([0, 0], [0, 0])));
        Assert.Equal(new double[] { -2, 1, 3, 4, 5, 7 }, JacobiSolver.Solve(MatrixFixtures.Demo().Matrix, new()).Eigenvalues!);
    }
}
