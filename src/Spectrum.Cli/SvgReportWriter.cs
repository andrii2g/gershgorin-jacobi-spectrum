using System.Globalization;
using System.Xml.Linq;
using Spectrum.Core;
namespace Spectrum.Cli;

public static class SvgReportWriter
{
    private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
    private static readonly string[] Colors = ["#2563eb", "#d97706", "#059669", "#9333ea", "#dc2626", "#0891b2", "#475569", "#be185d"];
    private static string F(double value) => double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : throw new ArgumentException("Nonfinite SVG coordinate.");
    private static XElement E(string tag, params object[] content) => new(Ns + tag, content);
    private static XAttribute A(string key, object value) => new(key, value is double x ? F(x) : value);
    private static XElement Text(double x, double y, string value, int size = 14) => E("text", A("x", x), A("y", y), A("font-size", size), value);
    private static XElement Line(double x1, double y1, double x2, double y2, string color, string css = "", string dash = "") =>
        E("line", A("x1", x1), A("y1", y1), A("x2", x2), A("y2", y2), A("stroke", color), A("stroke-width", 1.6), A("class", css), A("stroke-dasharray", dash));
    private static XElement Root(int width, int height, string title) => E("svg", A("viewBox", $"0 0 {width} {height}"), A("width", width), A("height", height),
        A("font-family", "sans-serif"), A("fill", "#172033"), E("title", title), E("rect", A("width", width), A("height", height), A("fill", "#ffffff")));
    private static DenseMatrix ScaledInput(Experiment run)
    {
        double scale = run.Solver.Scale;
        return DenseMatrix.FromRowMajor(run.Fixture.Matrix.Order, run.Fixture.Matrix.CopyData().Select(x => scale == 0 ? x : x / scale).ToArray());
    }
    private sealed record Limits(double Left, double Right, double Radius);
    private static Limits CoordinateLimits(IEnumerable<GershgorinResult> sets, IReadOnlyList<double>? eigenvalues)
    {
        var disks = sets.SelectMany(x => x.Disks).ToArray();
        double left = disks.Min(d => d.Left ?? d.Center), right = disks.Max(d => d.Right ?? d.Center), radius = disks.Max(d => d.RadiusEstimate ?? 0);
        if (eigenvalues is { Count: > 0 }) { left = Math.Min(left, eigenvalues.Min()); right = Math.Max(right, eigenvalues.Max()); }
        if (right == left) { left -= 1; right += 1; }
        return new(left, right, radius);
    }
    private static void Panel(XElement root, GershgorinResult bounds, IReadOnlyList<double>? eigenvalues, Limits limits,
        double x, double y, double width, double height, string title, string detail)
    {
        root.Add(Text(x + 15, y + 23, title, 17), Text(x + 15, y + 45, detail, 12));
        double paneLeft = x + 55, paneTop = y + 70, paneWidth = width - 90, paneHeight = height - 158;
        double horizontal = paneWidth / (limits.Right - limits.Left) * 0.9;
        double pixels = limits.Radius == 0 ? horizontal : Math.Min(horizontal, paneHeight * 0.43 / limits.Radius);
        double mid = limits.Left / 2 + limits.Right / 2, cy = paneTop + paneHeight / 2;
        double X(double value) => paneLeft + paneWidth / 2 + (value - mid) * pixels;
        root.Add(Line(paneLeft, cy, paneLeft + paneWidth, cy, "#64748b", "real-axis"));
        if (X(0) >= paneLeft && X(0) <= paneLeft + paneWidth) root.Add(Line(X(0), paneTop, X(0), paneTop + paneHeight, "#cbd5e1", "imaginary-axis", "4 4"));
        root.Add(Text(paneLeft + paneWidth - 33, cy - 8, "Re", 12), Text(paneLeft, paneTop - 5, "Im", 12));
        for (int tick = 0; tick <= 4; tick++)
        {
            double value = limits.Left + (limits.Right - limits.Left) * tick / 4;
            root.Add(Line(X(value), cy - 3, X(value), cy + 3, "#64748b"), Text(X(value) - 15, paneTop + paneHeight + 20, value.ToString("G4", CultureInfo.InvariantCulture), 11));
        }
        foreach (var d in bounds.Disks)
        {
            string color = Colors[d.RowIndex % Colors.Length]; double radius = (d.RadiusEstimate ?? 0) * pixels, cx = X(d.Center);
            if (radius == 0)
            {
                root.Add(Line(cx - 4, cy - 4, cx + 4, cy + 4, color, "zero-radius"), Line(cx - 4, cy + 4, cx + 4, cy - 4, color, "zero-radius"));
            }
            else root.Add(E("circle", A("class", "disk"), A("cx", cx), A("cy", cy), A("r", radius), A("fill", color), A("fill-opacity", 0.045), A("stroke", color), A("stroke-opacity", 0.65), E("title", $"matrix row {d.RowIndex}")));
            double stripY = paneTop + paneHeight + 32 + d.RowIndex % 6 * 3;
            root.Add(Line(X(d.Left ?? d.Center), stripY, X(d.Right ?? d.Center), stripY, color, "interval-strip"));
        }
        if (eigenvalues is not null) foreach (var group in eigenvalues.GroupBy(value => value))
        {
            double cx = X(group.Key); root.Add(Line(cx, cy - 7, cx, cy + 7, "#111827", "eigenvalue"));
            if (group.Count() > 1) root.Add(Text(cx + 4, cy - 10, $"×{group.Count()}", 12));
        }
        root.Add(Text(x + 15, y + height - 14, "Colored circles/strips: matrix rows · black ticks: eigenpair estimates", 11));
    }
    public static string Disks(Experiment run)
    {
        var bounds = GershgorinBounds.Analyze(ScaledInput(run)); var root = Root(1100, 650, "Gershgorin disks");
        root.Add(Text(25, 30, $"{run.Fixture.Name} · {run.Options.Policy} · {run.Solver.Status}", 22),
            Text(25, 54, "Real symmetric ⇒ real spectrum. Floating-point bounds; eigenvalue estimates are not certified.", 14));
        Panel(root, bounds, run.Solver.EigenvaluesScaled, CoordinateLimits([bounds], run.Solver.EigenvaluesScaled), 20, 70, 1060, 550,
            "Original matrix disks and real intervals", $"Axes in original units / scale; scale = {run.Solver.Scale.ToString("G6", CultureInfo.InvariantCulture)}. Equal x/y scale; crosses denote zero radius.");
        return root.ToString();
    }
    public static string Snapshots(Experiment run)
    {
        var snapshots = run.Solver.Snapshots; var bounds = snapshots.Select(s => GershgorinBounds.Analyze(s.Matrix)).ToArray();
        int height = 100 + ((snapshots.Count + 1) / 2) * 350;
        var root = Root(1100, height, "Jacobi transformation snapshots");
        root.Add(Text(25, 30, $"{run.Fixture.Name} · {run.Options.Policy} · transformation snapshots", 22),
            Text(25, 54, "Fixed axes across all panels; matrix row ids stay unsorted. Black ticks show final estimates.", 14),
            Text(25, 76, $"Real symmetric ⇒ real spectrum; axes divided by scale {run.Solver.Scale.ToString("G6", CultureInfo.InvariantCulture)}.", 13));
        var limits = CoordinateLimits(bounds, run.Solver.EigenvaluesScaled);
        for (int i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            Panel(root, bounds[i], run.Solver.EigenvaluesScaled, limits, i % 2 * 550, 95 + i / 2 * 350, 550, 350,
                $"Rotation {snapshot.Rotation} · {snapshot.Label}", $"direct off(B) = {StableNorm.OffDiagonal(snapshot.Matrix).ToString("G5", CultureInfo.InvariantCulture)}");
        }
        return root.ToString();
    }
    public static string Convergence(Experiment run) => Convergence([run]);
    public static string Convergence(IReadOnlyList<Experiment> runs)
    {
        var root = Root(1100, 650, "Direct convergence trace");
        root.Add(Text(25, 32, $"{runs[0].Fixture.Name} · direct convergence", 22), Text(25, 58, "y = log10(off(B) / ||B₀||F), x = applied rotations. Direct samples only.", 14));
        if (runs.Count > 1) root.Add(Text(25, 82, "Equal rotations do not mean equal time: maximum-pivot scans inspect every upper-triangle entry.", 14));
        double floor = 1e-18;
        var positive = runs.SelectMany(r => r.Solver.Trace).Where(s => s.RelativeOffNorm > 0).Select(s => s.RelativeOffNorm).ToArray();
        if (positive.Length > 0) floor = Math.Max(1e-300, Math.Min(floor, positive.Min() / 10));
        double low = Math.Log10(floor), high = Math.Max(0, positive.Length == 0 ? 0 : Math.Log10(positive.Max()));
        double maxRotation = Math.Max(1, runs.Max(r => (double)r.Solver.Rotations));
        double X(double rotation) => 100 + 930 * rotation / maxRotation;
        double Y(double value) => 530 - 400 * (Math.Clamp(Math.Log10(Math.Max(floor, value)), low, high) - low) / (high - low);
        root.Add(Line(100, 130, 100, 530, "#64748b"), Line(100, 530, 1030, 530, "#64748b"));
        for (int i = 0; i <= 4; i++)
        {
            double exponent = low + (high - low) * i / 4, y = 530 - 100 * i;
            root.Add(Line(100, y, 1030, y, "#e2e8f0"), Text(25, y + 5, $"10^{exponent.ToString("F1", CultureInfo.InvariantCulture)}", 12),
                Text(X(maxRotation * i / 4) - 10, 553, (maxRotation * i / 4).ToString("G5", CultureInfo.InvariantCulture), 12));
        }
        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i]; string color = Colors[i];
            string points = string.Join(' ', run.Solver.Trace.Select(s => $"{F(X(s.Rotation))},{F(Y(s.RelativeOffNorm))}"));
            root.Add(E("polyline", A("class", "direct-trace"), A("points", points), A("fill", "none"), A("stroke", color), A("stroke-width", 2), A("stroke-dasharray", i == 0 ? "" : "6 3")));
            foreach (var s in run.Solver.Trace)
            {
                root.Add(E("circle", A("class", s.RelativeOffNorm == 0 ? "zero-sample" : "direct-sample"), A("cx", X(s.Rotation)), A("cy", Y(s.RelativeOffNorm)), A("r", 2.8), A("fill", color)));
                if (s.RelativeOffNorm == 0) root.Add(Text(X(s.Rotation) - 24, Y(0) - 10 - i * 15, "zero", 11));
            }
            double tolerance = run.Solver.InitialNormScaled == 0 ? run.Options.RelativeTolerance : (run.Solver.ThresholdScaled ?? double.MaxValue) / run.Solver.InitialNormScaled;
            if (!double.IsFinite(tolerance)) tolerance = double.MaxValue;
            root.Add(Line(100, Y(tolerance), 1030, Y(tolerance), color, "tolerance", "3 5"),
                Text(110 + i * 480, 108, $"{run.Options.Policy}: {(i == 0 ? "solid" : "dashed")}; tolerance {tolerance.ToString("G4", CultureInfo.InvariantCulture)}", 13));
        }
        root.Add(Text(100, 580, $"Zero samples are shown at display floor {floor.ToString("G3", CultureInfo.InvariantCulture)} and labelled zero; this is not a measured positive error.", 12),
            Text(100, 605, "Trace compaction preserves initial/final samples; operation counts remain exact.", 12));
        return root.ToString();
    }
}
