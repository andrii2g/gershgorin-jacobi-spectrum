using Spectrum.Core;
namespace Spectrum.Tests;
public sealed class FoundationTests
{
    [Fact] public void ValidationAndOwnership()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DenseMatrix.FromRowMajor(0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => DenseMatrix.FromRowMajor(int.MaxValue, []));
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRowMajor(2, [1]));
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRows([1, 2], [3, 4]));
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRows([double.NaN]));
        Assert.Throws<ArgumentException>(() => DenseMatrix.FromRows([double.PositiveInfinity]));
        double[] values = [1, 0, 0, 2]; var a = DenseMatrix.FromRowMajor(2, values); values[0] = 99;
        var copy = a.CopyData(); copy[0] = 88; Assert.Equal(1, a[0, 0]);
    }
    [Theory] [InlineData(1e-200)] [InlineData(1e200)] public void StableNormExtremes(double scale)
    {
        Assert.InRange(Math.Abs(StableNorm.Vector([3 * scale, 4 * scale]) / scale - 5), 0, 2e-15);
        Assert.Equal(0, StableNorm.OffDiagonal(DenseMatrix.Identity(4)));
    }
    [Fact] public void KnownBoundsAndUnions()
    {
        var b = GershgorinBounds.Analyze(DenseMatrix.FromRows([4, -1, 2], [-1, 3, 0], [2, 0, 5]));
        Assert.Equal(new double?[] { 3, 1, 2 }, b.Disks.Select(x => x.RadiusEstimate));
        Assert.Equal(new Interval(1, 7), Assert.Single(b.MergedIntervals)); Assert.Equal(6, b.UnionLength);
        Assert.Equal(2, b.RadiusMean); Assert.True(b.CertifiedFinite);
        Assert.All(b.Disks, d => { Assert.True(d.OutwardLeft <= d.Left); Assert.True(d.OutwardRight >= d.Right); });
        Assert.Equal(2, GershgorinBounds.MergeIntervals([new(0, 1), new(3, 4)]).Count);
        Assert.Equal(new Interval(0, 2), Assert.Single(GershgorinBounds.MergeIntervals([new(0, 1), new(1, 2)])));
        Assert.Equal(1, GershgorinBounds.DistanceToUnion(2, [new(0, 1), new(3, 4)]));
    }
    [Fact] public void OutwardOverflowIsExplicit()
    {
        var b = GershgorinBounds.Analyze(DenseMatrix.FromRows([double.MaxValue, double.MaxValue], [double.MaxValue, double.MaxValue]));
        Assert.False(b.CertifiedFinite); Assert.Null(b.Disks[0].Right); Assert.NotEmpty(b.Warnings);
        var m = GershgorinBounds.Analyze(DenseMatrix.FromRows([0, 1, 1e-16], [1, 0, 0], [1e-16, 0, 0]));
        Assert.True(m.Disks[0].RadiusUpper > 1);
    }
    [Fact] public void SplitMixReferenceSequence()
    {
        var r = new DeterministicRandom(0);
        Assert.Equal(0xe220a8397b1dcdafUL, r.NextUInt64()); Assert.Equal(0x6e789e6aa1b965f4UL, r.NextUInt64());
        var a = new DeterministicRandom(42); var b = new DeterministicRandom(42);
        for (int i = 0; i < 100; i++) { double x = a.Uniform01(); Assert.Equal(x, b.Uniform01()); Assert.InRange(x, 0, Math.BitDecrement(1)); }
    }
}
