namespace Spectrum.Core;
public readonly record struct Pivot(int P, int Q, double Magnitude, long Inspected);
public static class PivotSearch
{
    public static Pivot FindMaximum(DenseMatrix matrix)
    {
        int p = 0, q = matrix.Order > 1 ? 1 : 0; double max = 0; long inspected = 0;
        for (int i = 0; i < matrix.Order; i++) for (int j = i + 1; j < matrix.Order; j++)
        {
            inspected++; double value = Math.Abs(matrix[i, j]);
            if (value > max) { max = value; p = i; q = j; }
        }
        return new(p, q, max, inspected);
    }
}
/// <summary>Only complete cadence checks feed the stagnation monitor.</summary>
public sealed class StagnationMonitor(double initial, double initialNorm)
{
    private double previous = initial;
    private int stalled;
    public bool Observe(double direct)
    {
        stalled = previous - direct > 8 * StableNorm.UnitRoundoff * initialNorm ? 0 : stalled + 1;
        previous = direct;
        return stalled >= 3;
    }
}
