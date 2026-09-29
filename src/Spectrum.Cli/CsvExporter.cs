using System.Globalization;
using System.Text;
using Spectrum.Core;
namespace Spectrum.Cli;
public static class CsvExporter
{
    public static string Field(object? value)
    {
        string text = value switch { null => "", double x => double.IsFinite(x) ? x.ToString("G17", CultureInfo.InvariantCulture) : throw new ArgumentException("CSV requires finite numbers."),
            bool b => b ? "true" : "false", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString()! };
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? '"' + text.Replace("\"", "\"\"") + '"' : text;
    }
    public static string Row(params object?[] values) => string.Join(',', values.Select(Field)) + "\n";
    public static string Eigenpairs(Experiment run)
    {
        var text = new StringBuilder("index,eigenvalue,residualAbsolute,residualNormalized,vectorNorm,distanceToOriginalUnionScaled,containedWithTolerance\n");
        if (run.Accuracy is not null) foreach (var d in run.Accuracy.Eigenpairs) text.Append(Row(d.Index, run.Solver.Eigenvalues![d.Index], d.ResidualAbsolute, d.ResidualNormalized, d.VectorNorm, d.DistanceToOriginalUnionScaled, d.ContainedWithTolerance));
        return text.ToString();
    }
    public static string Eigenvectors(JacobiResult result)
    {
        var text = new StringBuilder("row,col,value\n"); var v = result.Eigenvectors;
        if (v is not null) for (int i = 0; i < v.Order; i++) for (int j = 0; j < v.Order; j++) text.Append(Row(i, j, v[i, j]));
        return text.ToString();
    }
    public static string Convergence(JacobiResult result)
    {
        var text = new StringBuilder("rotation,pivotVisit,completedSweeps,offNormScaled,offNormOriginal,relativeOffNorm,direct\n");
        foreach (var s in result.Trace) text.Append(Row(s.Rotation, s.PivotVisit, s.CompletedSweeps, s.OffNormScaled, s.OffNormOriginal, s.RelativeOffNorm, s.Direct));
        return text.ToString();
    }
    public static string Disks(Experiment run)
    {
        var text = new StringBuilder("snapshot,rotation,row,center,radiusEstimate,radiusUpper,left,right,outwardLeft,outwardRight,certifiedFinite\n");
        void Append(string label, long rotation, GershgorinResult bounds, double scale)
        {
            foreach (var d in bounds.Disks)
            {
                double? Convert(double? x) => x.HasValue ? GershgorinBounds.Finite(x.Value * scale) : null;
                // Rescaling outward endpoints also rounds outwards; representability is explicit.
                double? ol = d.OutwardLeft.HasValue ? GershgorinBounds.Finite(Math.BitDecrement(d.OutwardLeft.Value * scale)) : null;
                double? or = d.OutwardRight.HasValue ? GershgorinBounds.Finite(Math.BitIncrement(d.OutwardRight.Value * scale)) : null;
                if (label == "original") { ol = d.OutwardLeft; or = d.OutwardRight; }
                text.Append(Row(label, rotation, d.RowIndex, Convert(d.Center), Convert(d.RadiusEstimate), Convert(d.RadiusUpper), Convert(d.Left), Convert(d.Right), ol, or, d.CertifiedFinite && ol.HasValue && or.HasValue));
            }
        }
        Append("original", 0, run.Bounds, 1);
        foreach (var snapshot in run.Solver.Snapshots) Append(snapshot.Label, snapshot.Rotation, GershgorinBounds.Analyze(snapshot.Matrix), snapshot.Scale);
        return text.ToString();
    }
}
