namespace Spectrum.Core;

public static class StableNorm
{
    public const double UnitRoundoff = 1.1102230246251565e-16;
    public struct Accumulator
    {
        private double scale, sum;
        public void Add(double value)
        {
            double x = Math.Abs(value);
            if (x == 0) return;
            if (scale < x) { double r = scale / x; sum = 1 + sum * r * r; scale = x; }
            else { double r = x / scale; sum += r * r; }
        }
        public readonly double Norm => scale == 0 ? 0 : scale * Math.Sqrt(sum);
    }
    public static double Vector(IEnumerable<double> values) { var a = new Accumulator(); foreach (double x in values) a.Add(x); return a.Norm; }
    public static double Hypot(double x, double y) { var a = new Accumulator(); a.Add(x); a.Add(y); return a.Norm; }
    public static double Frobenius(DenseMatrix matrix)
    {
        var a = new Accumulator();
        for (int i = 0; i < matrix.Order; i++) for (int j = 0; j < matrix.Order; j++) a.Add(matrix[i, j]);
        return a.Norm;
    }
    public static double OffDiagonal(DenseMatrix matrix)
    {
        var a = new Accumulator();
        for (int i = 0; i < matrix.Order; i++) for (int j = i + 1; j < matrix.Order; j++) { a.Add(matrix[i, j]); a.Add(matrix[i, j]); }
        return a.Norm;
    }
    public static double Sum(IEnumerable<double> values)
    {
        double sum = 0, correction = 0;
        foreach (double value in values) { double y = value - correction, t = sum + y; correction = (t - sum) - y; sum = t; }
        return sum;
    }
}
