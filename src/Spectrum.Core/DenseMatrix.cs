namespace Spectrum.Core;

/// <summary>Owned row-major storage. Factories and CopyData copy; internal setters serve the numerical kernels.</summary>
public sealed class DenseMatrix
{
    private readonly double[] data;
    public int Order { get; }
    internal DenseMatrix(int order) { Order = order; data = new double[CheckedLength(order)]; }
    public static int CheckedLength(int order)
    {
        if (order < 1 || (long)order * order > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(order));
        return checked(order * order);
    }
    public double this[int row, int col]
    {
        get { Check(row, col); return data[row * Order + col]; }
        internal set { Check(row, col); data[row * Order + col] = value; }
    }
    private void Check(int row, int col)
    {
        if ((uint)row >= Order || (uint)col >= Order) throw new ArgumentOutOfRangeException(nameof(row));
    }
    public static DenseMatrix FromRowMajor(int order, IReadOnlyList<double> values)
    {
        int length = CheckedLength(order);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != length) throw new ArgumentException("Wrong matrix length.", nameof(values));
        for (int i = 0; i < length; i++)
            if (!double.IsFinite(values[i])) throw new ArgumentException("Entries must be finite.", nameof(values));
        for (int i = 0; i < order; i++) for (int j = i + 1; j < order; j++)
            if (values[i * order + j] != values[j * order + i]) throw new ArgumentException("Matrix must be exactly symmetric.", nameof(values));
        var result = new DenseMatrix(order);
        for (int i = 0; i < length; i++) result.data[i] = values[i];
        return result;
    }
    public static DenseMatrix FromRows(params double[][] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int length = CheckedLength(rows.Length);
        if (rows.Any(r => r is null || r.Length != rows.Length)) throw new ArgumentException("Rows must form a square.", nameof(rows));
        var flat = new double[length];
        for (int i = 0; i < rows.Length; i++) rows[i].CopyTo(flat, i * rows.Length);
        return FromRowMajor(rows.Length, flat);
    }
    public DenseMatrix Clone() { var clone = new DenseMatrix(Order); data.CopyTo(clone.data, 0); return clone; }
    public double[] CopyData() => (double[])data.Clone();
    public static DenseMatrix Identity(int order)
    {
        var result = new DenseMatrix(order);
        for (int i = 0; i < order; i++) result[i, i] = 1;
        return result;
    }
}
