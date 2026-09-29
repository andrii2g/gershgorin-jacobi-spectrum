namespace Spectrum.Core;

public readonly record struct RotationParameters(double C, double S, double T);
public static class JacobiRotation
{
    public static RotationParameters Compute(double app, double aqq, double apq)
    {
        if (!double.IsFinite(app) || !double.IsFinite(aqq) || !double.IsFinite(apq)) throw new ArgumentException("Rotation entries must be finite.");
        if (apq == 0) return new(1, 0, 0);
        double d = aqq / 2 - app / 2, t;
        if (d == 0) t = Math.CopySign(1, apq);
        else
        {
            double m = Math.Max(Math.Abs(d), Math.Abs(apq));
            double h = StableNorm.Hypot(d / m, apq / m);
            t = (apq / m) / (d / m + Math.CopySign(h, d));
        }
        double c = 1 / Math.Sqrt(1 + t * t);
        return new(c, t * c, t);
    }
    public static bool Apply(DenseMatrix matrix, DenseMatrix vectors, int p, int q)
    {
        ArgumentNullException.ThrowIfNull(matrix); ArgumentNullException.ThrowIfNull(vectors);
        if (ReferenceEquals(matrix, vectors) || vectors.Order != matrix.Order || p < 0 || q <= p || q >= matrix.Order)
            throw new ArgumentException("Distinct equal-order matrices and 0 <= p < q < n required.");
        double b = matrix[p, q]; if (b == 0) return false;
        double app = matrix[p, p], aqq = matrix[q, q]; var (c, s, t) = Compute(app, aqq, b);
        for (int k = 0; k < matrix.Order; k++)
        {
            if (k != p && k != q)
            {
                double x = matrix[k, p], y = matrix[k, q];
                matrix[k, p] = matrix[p, k] = c * x - s * y;
                matrix[k, q] = matrix[q, k] = s * x + c * y;
            }
            double vx = vectors[k, p], vy = vectors[k, q];
            vectors[k, p] = c * vx - s * vy; vectors[k, q] = s * vx + c * vy;
        }
        matrix[p, p] = app - t * b; matrix[q, q] = aqq + t * b;
        matrix[p, q] = matrix[q, p] = 0;
        return true;
    }
}
