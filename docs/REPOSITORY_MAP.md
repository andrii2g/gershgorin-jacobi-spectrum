# Implemented source map

`src/Spectrum.Core` is BCL-only and contains no IO, clocks, console or plotting. `src/Spectrum.Cli` references Core. `tests/Spectrum.Tests` references both and runs xUnit.

| File | Responsibility |
|---|---|
| Core/DenseMatrix.cs | Validated row-major ownership, cloning, identity |
| Core/StableNorm.cs | Scaled norms and compensated sums |
| Core/GershgorinBounds.cs | Disk/interval/result types, estimated and outward bounds, merges/distances/statistics |
| Core/DeterministicRandom.cs | Versioned SplitMix64 |
| Core/JacobiRotation.cs | Stable rotation parameters and O(n) symmetric/eigenvector updates |
| Core/PivotSearch.cs | Deterministic maximum scan and cadence stagnation monitor |
| Core/Model.cs | Options and status/policy enums |
| Core/JacobiSolver.cs | Both policies, scaling, budgets, direct convergence, final eigenpair ordering |
| Core/JacobiResult.cs | Read-only result and deep-copy snapshot contracts |
| Core/TraceRecorder.cs | Bounded direct trace, compaction and snapshot schedule |
| Core/MatrixFixtures.cs | Five families, demo and canonical hash metadata |
| Core/SpectrumDiagnostics.cs | Original-matrix residuals/reconstruction, orthogonality, invariant/containment diagnostics |
| Cli/Program.cs, CliApplication.cs | Dispatch, Ctrl+C cancellation and exit codes |
| Cli/CommandLine.cs, MatrixJsonReader.cs | Option/input validation and help |
| Cli/ExperimentRunner.cs | Educational solve and export orchestration |
| Cli/BenchmarkRunner.cs | Separate untraced timings, alternating repeats, aggregate comparison |
| Cli/JsonExporter.cs, CsvExporter.cs | Stable finite JSON, invariant RFC4180 CSV |
| Cli/SvgReportWriter.cs | Disks, convergence and fixed-axis snapshot panels |
| Cli/OutputDirectory.cs | Nonempty-output policy and atomic generated-file replacement |

Paths in the table abbreviate `src/Spectrum.Core` as Core and `src/Spectrum.Cli` as Cli.

Tests are grouped in FoundationTests, RotationTests, SolverTests, FixtureDiagnosticsTests, AdditionalNumericalTests, CliIntegrationTests and BenchmarkTests. `scripts/verify.ps1` runs the Windows release gate and artifact audit; `scripts/verify.sh` runs the Linux/CI gate. `.github/workflows/ci.yml` restores/builds/tests and uploads the small generated artifacts. `docs/VALIDATION.md` records actual host evidence and unexecuted checks.

The README embeds generated SVGs in `docs/images/`; `scripts/update-readme-charts.ps1` rebuilds and refreshes them from deterministic demo/comparison runs. Their inputs and regeneration procedure are documented in [images/README.md](images/README.md).
