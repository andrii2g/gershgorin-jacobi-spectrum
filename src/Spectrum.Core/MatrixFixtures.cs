using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Collections.ObjectModel;
namespace Spectrum.Core;

public sealed record FixtureResult(DenseMatrix Matrix, string Name, string? Family, ulong? Seed,
    string? GeneratorVersion, string MatrixSha256, IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyList<double>? AnalyticalSpectrum, double? ConstructionOrthogonality = null);
public static class MatrixFixtures
{
    public static IReadOnlyList<string> Families { get; } = Array.AsReadOnly(new[] { "dominant", "toeplitz", "clustered", "near-diagonal", "random" });
    public static string Hash(DenseMatrix matrix)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Span<byte> bytes = stackalloc byte[8];
        for (int i = 0; i < matrix.Order; i++) for (int j = 0; j < matrix.Order; j++)
        { double value = matrix[i, j]; BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value == 0 ? 0 : value)); hash.AppendData(bytes); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public static FixtureResult Input(DenseMatrix matrix, string name) => new(matrix, name, null, null, null, Hash(matrix), new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()), null);
    public static FixtureResult Demo() => Input(DenseMatrix.FromRows([2, 1, 0, 0, 0, 0], [1, 2, 0, 0, 0, 0],
        [0, 0, 6, -1, 0, 0], [0, 0, -1, 6, 0, 0], [0, 0, 0, 0, -2, 0], [0, 0, 0, 0, 0, 4]), "demo-6x6")
        with { AnalyticalSpectrum = Array.AsReadOnly(new double[] { -2, 1, 3, 4, 5, 7 }) };
    public static FixtureResult Create(string family, int order = 16, ulong seed = 42, double diagonal = 2,
        double offDiagonal = -1, double clusterDelta = 1e-6, double epsilon = 1e-6)
    {
        DenseMatrix.CheckedLength(order);
        if (!Families.Contains(family)) throw new ArgumentException("Unknown family.", nameof(family));
        if (!double.IsFinite(diagonal) || !double.IsFinite(offDiagonal) || !double.IsFinite(clusterDelta) || clusterDelta < 0 || !double.IsFinite(epsilon) || epsilon < 0)
            throw new ArgumentException("Invalid fixture parameter.");
        var a = new DenseMatrix(order); var rng = new DeterministicRandom(seed); double[]? spectrum = null; double? constructionOrthogonality = null;
        var parameters = new Dictionary<string, double>();
        switch (family)
        {
            case "toeplitz":
                parameters.Add("diagonal", diagonal); parameters.Add("offDiagonal", offDiagonal);
                for (int i = 0; i < order; i++) { a[i, i] = diagonal; if (i + 1 < order) a[i, i + 1] = a[i + 1, i] = offDiagonal; }
                spectrum = Enumerable.Range(1, order).Select(k => diagonal + 2 * offDiagonal * Math.Cos(k * Math.PI / (order + 1))).Order().ToArray();
                if (spectrum.Any(x => !double.IsFinite(x))) spectrum = null;
                break;
            case "dominant":
                for (int i = 0; i < order; i++) for (int j = i + 1; j < order; j++) a[i, j] = a[j, i] = rng.UniformSigned() / order;
                for (int i = 0; i < order; i++) a[i, i] = StableNorm.Sum(Enumerable.Range(0, order).Where(j => j != i).Select(j => Math.Abs(a[i, j]))) + 1 + (double)i / order;
                break;
            case "near-diagonal":
                parameters.Add("epsilon", epsilon);
                for (int i = 0; i < order; i++) { a[i, i] = 1 + i; for (int j = i + 1; j < order; j++) a[i, j] = a[j, i] = epsilon * rng.UniformSigned() / order; }
                if (epsilon == 0) spectrum = Enumerable.Range(1, order).Select(i => (double)i).ToArray();
                break;
            case "random":
                for (int i = 0; i < order; i++) for (int j = i; j < order; j++) a[i, j] = a[j, i] = rng.UniformSigned() / Math.Sqrt(order);
                break;
            case "clustered":
                parameters.Add("clusterDelta", clusterDelta); double[] bases = [1, 2, 4];
                spectrum = Enumerable.Range(0, order).Select(i => bases[i % 3] + clusterDelta * ((double)(i / 3) / Math.Max(1, order))).ToArray();
                var q = DenseMatrix.Identity(order);
                if (order > 1) for (long k = 0; k < 4L * order; k++)
                {
                    int p = (int)(rng.NextUInt64() % (ulong)order), r = (int)(rng.NextUInt64() % (ulong)(order - 1)); if (r >= p) r++;
                    double angle = rng.UniformSigned() * Math.PI, c = Math.Cos(angle), s = Math.Sin(angle);
                    for (int i = 0; i < order; i++) { double x = q[i, p], y = q[i, r]; q[i, p] = c * x - s * y; q[i, r] = s * x + c * y; }
                }
                constructionOrthogonality = SpectrumDiagnostics.Orthogonality(q);
                for (int i = 0; i < order; i++) for (int j = i; j < order; j++)
                    a[i, j] = a[j, i] = StableNorm.Sum(Enumerable.Range(0, order).Select(k => q[i, k] * spectrum[k] * q[j, k]));
                Array.Sort(spectrum);
                break;
        }
        if (a.CopyData().Any(x => !double.IsFinite(x))) throw new ArgumentException("Fixture construction overflowed.");
        return new(a, $"{family}-{order}", family, seed, DeterministicRandom.Version, Hash(a), new ReadOnlyDictionary<string, double>(parameters), spectrum is null ? null : Array.AsReadOnly(spectrum), constructionOrthogonality);
    }
}
