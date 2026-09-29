using Spectrum.Core;
namespace Spectrum.Tests;

public sealed class RotationTests
{
    [Theory] [InlineData(3)] [InlineData(5)] public void IndependentSimilarity(int n)
    {
        var rng = new DeterministicRandom(71); var rows = new double[n][];
        for (int i = 0; i < n; i++) rows[i] = new double[n];
        for (int i = 0; i < n; i++) for (int k = i; k < n; k++) rows[i][k] = rows[k][i] = rng.UniformSigned();
        var a = DenseMatrix.FromRows(rows); var before = a.Clone(); var v = DenseMatrix.Identity(n);
        var (c, s, _) = JacobiRotation.Compute(a[0, 0], a[2, 2], a[0, 2]);
        var j = new double[n, n]; for (int i = 0; i < n; i++) j[i, i] = 1;
        j[0, 0] = j[2, 2] = c; j[0, 2] = s; j[2, 0] = -s;
        var expected = new double[n, n];
        for (int i = 0; i < n; i++) for (int k = 0; k < n; k++)
            for (int l = 0; l < n; l++) for (int m = 0; m < n; m++) expected[i, k] += j[l, i] * a[l, m] * j[m, k];
        double off = StableNorm.OffDiagonal(a), b = a[0, 2];
        JacobiRotation.Apply(a, v, 0, 2);
        for (int i = 0; i < n; i++) for (int k = 0; k < n; k++)
        {
            Assert.InRange(Math.Abs(a[i, k] - expected[i, k]), 0, 2e-15);
            Assert.Equal(a[i, k], a[k, i]); Assert.Equal(j[i, k], v[i, k]);
        }
        Assert.InRange(Math.Abs(StableNorm.Frobenius(a) - StableNorm.Frobenius(before)), 0, 2e-15);
        Assert.InRange(Math.Abs(Enumerable.Range(0, n).Sum(i => a[i, i] - before[i, i])), 0, 2e-15);
        Assert.InRange(Math.Abs(Math.Pow(StableNorm.OffDiagonal(a), 2) - (off * off - 2 * b * b)), 0, 8e-15);
    }
    [Theory] [InlineData(1)] [InlineData(-1)] public void EqualDiagonalSigns(double b)
    {
        var a = DenseMatrix.FromRows([2, b], [b, 2]); var v = DenseMatrix.Identity(2);
        Assert.Equal(b, JacobiRotation.Compute(2, 2, b).T); JacobiRotation.Apply(a, v, 0, 1);
        Assert.Equal(1, a[0, 0]); Assert.Equal(3, a[1, 1]); Assert.Equal(0, a[0, 1]);
    }
    [Theory] [InlineData(1e-150)] [InlineData(1e-310)] public void TinyPivot(double b)
    {
        var r = JacobiRotation.Compute(1, 2, b); Assert.True(double.IsFinite(r.T)); Assert.InRange(r.T / b, 0.99999999999999, 1.00000000000001);
    }
}
