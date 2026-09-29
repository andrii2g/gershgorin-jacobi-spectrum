namespace Spectrum.Core;

public sealed record Interval
{
    public double Left { get; }
    public double Right { get; }
    public Interval(double left, double right)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right) || left > right) throw new ArgumentException("Invalid interval.");
        Left = left; Right = right;
    }
}
public sealed record GershgorinDisk(int RowIndex, double Center, double? RadiusEstimate, double? RadiusUpper,
    double? Left, double? Right, double? OutwardLeft, double? OutwardRight, bool CertifiedFinite);
public sealed record GershgorinResult(IReadOnlyList<GershgorinDisk> Disks, IReadOnlyList<Interval> MergedIntervals,
    IReadOnlyList<Interval> MergedOutwardIntervals, double? RadiusMin, double? RadiusMean, double? RadiusMax,
    double? EnvelopeWidth, double? UnionLength, IReadOnlyList<string> Warnings)
{
    public bool CertifiedFinite => Disks.All(d => d.CertifiedFinite);
}
public static class GershgorinBounds
{
    public static double? Finite(double value) => double.IsFinite(value) ? value : null;
    public static GershgorinResult Analyze(DenseMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var disks = new List<GershgorinDisk>(); var estimates = new List<Interval>(); var outward = new List<Interval>();
        var warnings = new List<string>();
        for (int i = 0; i < matrix.Order; i++)
        {
            double radius = 0, correction = 0, upper = 0;
            for (int j = 0; j < matrix.Order; j++)
            {
                if (j == i) continue;
                double x = Math.Abs(matrix[i, j]);
                double y = x - correction, t = radius + y;
                correction = (t - radius) - y; radius = t;
                if (x != 0) upper = Math.BitIncrement(upper + x);
            }
            double center = matrix[i, i], left = center - radius, right = center + radius;
            double ol = Math.BitDecrement(center - upper), or = Math.BitIncrement(center + upper);
            bool finite = double.IsFinite(ol) && double.IsFinite(or);
            disks.Add(new(i, center, Finite(radius), Finite(upper), Finite(left), Finite(right), Finite(ol), Finite(or), finite));
            if (double.IsFinite(left) && double.IsFinite(right)) estimates.Add(new(left, right));
            if (finite) outward.Add(new(ol, or));
            if (!finite || !double.IsFinite(radius)) warnings.Add($"Row {i}: unrepresentable bound; null diagnostics are not finite enclosures.");
        }
        var merged = MergeIntervals(estimates); var mo = MergeIntervals(outward);
        bool complete = estimates.Count == matrix.Order;
        double? min = disks.All(d => d.RadiusEstimate.HasValue) ? disks.Min(d => d.RadiusEstimate) : null;
        double? max = disks.All(d => d.RadiusEstimate.HasValue) ? disks.Max(d => d.RadiusEstimate) : null;
        double? mean = max.HasValue ? Finite(StableNorm.Sum(disks.Select(d => d.RadiusEstimate!.Value / matrix.Order))) : null;
        double? envelope = complete ? Finite(merged[^1].Right - merged[0].Left) : null;
        double? union = complete ? Finite(StableNorm.Sum(merged.Select(x => x.Right - x.Left))) : null;
        if (min is null || mean is null || max is null || envelope is null || union is null) warnings.Add("One or more radius/width diagnostics are unrepresentable and null.");
        return new(disks.AsReadOnly(), merged, mo, min, mean, max, envelope, union, warnings.AsReadOnly());
    }
    public static IReadOnlyList<Interval> MergeIntervals(IEnumerable<Interval> intervals)
    {
        var result = new List<Interval>();
        foreach (var interval in intervals.OrderBy(i => i.Left).ThenBy(i => i.Right))
        {
            if (result.Count == 0 || interval.Left > result[^1].Right) result.Add(interval);
            else result[^1] = new(result[^1].Left, Math.Max(result[^1].Right, interval.Right));
        }
        return result.AsReadOnly();
    }
    public static double DistanceToUnion(double value, IReadOnlyList<Interval> intervals)
    {
        if (!double.IsFinite(value) || intervals.Count == 0) throw new ArgumentException("Finite value and nonempty union required.");
        return intervals.Min(i => value < i.Left ? i.Left - value : value > i.Right ? value - i.Right : 0);
    }
}
