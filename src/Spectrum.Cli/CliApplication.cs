using System.Text.Json;
using Spectrum.Core;
namespace Spectrum.Cli;
public static class CliApplication
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token = default)
    {
        try
        {
            var command = CommandLine.Parse(args);
            if (command.Command == "help") { stdout.WriteLine(CommandLine.Help); return 0; }
            if (command.Command == "compare") return BenchmarkRunner.Run(command, stdout, token);
            if (command.Command == "solve")
            {
                var fixture = command.Has("input") ? MatrixJsonReader.Read(command.Values["input"], command) : MatrixFixtures.Create(command.Text("family", "random"), command.Integer("size", 16), command.Seed,
                    command.Number("diagonal", 2), command.Number("off-diagonal", -1), command.Number("cluster-delta", 1e-6), command.Number("epsilon", 1e-6));
                JacobiSolver.ValidateOptions(command.Options, fixture.Matrix.Order);
                OutputDirectory.Prepare(command.Output, command.Has("overwrite"));
                var run = ExperimentRunner.Run(fixture, command.Options, token); ExperimentRunner.Export(run, command.Output, command.Has("input"));
                stdout.WriteLine($"{fixture.Name}: {run.Solver.Status}; {run.Solver.Rotations} rotations; {Path.GetFullPath(command.Output)}");
                return ExperimentRunner.ExitCode(run.Solver.Status);
            }
            OutputDirectory.Prepare(command.Output, command.Has("overwrite"));
            var links = new List<string>(); int exit = 0;
            foreach (var fixture in new[] { MatrixFixtures.Demo(), MatrixFixtures.Create("random", 6) }) foreach (var policy in Enum.GetValues<PivotPolicy>())
            {
                string id = $"{(fixture.Family is null ? "analytic" : "random")}-{ExperimentRunner.PolicyName(policy)}";
                var run = ExperimentRunner.Run(fixture, new() { Policy = policy }, token);
                ExperimentRunner.Export(run, Path.Combine(command.Output, id), true);
                links.Add($"- [{id}]({id}/summary.json): [disks]({id}/gershgorin.svg), [convergence]({id}/convergence.svg), [snapshots]({id}/snapshots.svg)");
                exit = Math.Max(exit, ExperimentRunner.ExitCode(run.Solver.Status)); if (exit == 130) return exit;
            }
            OutputDirectory.Write(command.Output, "index.md", "# Jacobi and Gershgorin demo\n\nThe analytic block spectrum is -2, 1, 3, 4, 5, 7. Opposite signs in equal-diagonal blocks exercise both rotation branches. Some eigenvalues belong to overlapping disks; a disk is not assigned to one eigenvalue. The random example shows denser trajectories.\n\n" + string.Join('\n', links) + "\n\nOnly off-diagonal mass has the exact decreasing identity. Individual disk radii and union widths need not decrease. Final ticks are numerical estimates, not certified eigenvalues.\n");
            stdout.WriteLine($"Demo: {Path.GetFullPath(command.Output)}"); return exit;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or JsonException)
        { stderr.WriteLine($"Invalid input: {ex.Message}"); return 2; }
        catch (OperationCanceledException) { stderr.WriteLine("Cancelled."); return 130; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { stderr.WriteLine($"IO failure: {ex.Message}"); return 6; }
    }
}


