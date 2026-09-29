using System.Text.Json;
using Spectrum.Core;
namespace Spectrum.Cli;
public static class MatrixJsonReader
{
    public static FixtureResult Read(string path, CommandLine command)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1 ||
            !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("matrix", out var matrix) || matrix.ValueKind != JsonValueKind.Array) throw new ArgumentException("Input requires schemaVersion=1, name, and matrix rows.");
        int n = matrix.GetArrayLength(); command.CheckSize(n);
        foreach (var row in matrix.EnumerateArray()) if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != n) throw new ArgumentException("Matrix rows must form a square.");
        var values = new double[DenseMatrix.CheckedLength(n)]; int i = 0;
        foreach (var row in matrix.EnumerateArray()) foreach (var value in row.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double x) || !double.IsFinite(x)) throw new ArgumentException("Matrix entries must be finite numbers.");
            values[i++] = x;
        }
        return MatrixFixtures.Input(DenseMatrix.FromRowMajor(n, values), name.GetString()!);
    }
}
