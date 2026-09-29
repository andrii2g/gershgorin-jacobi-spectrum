# Core API and ownership

The numerical library uses namespace `Spectrum.Core` and targets .NET 10. Core code has no IO, clocks, console output or plotting; the CLI owns those responsibilities. Production projects use only the BCL.

| Type | Implemented contract |
|---|---|
| `DenseMatrix` | `Order`, read-only public indexer, `FromRows`, `FromRowMajor`, `Clone`, `Identity`, `CopyData`. Checked full row-major storage; finite, exact-symmetry validation; no hidden repair. |
| `StableNorm` | `Frobenius`, `OffDiagonal`, `Vector`, `Hypot`, compensated `Sum`, and a scaled sum-of-squares accumulator. `UnitRoundoff` is 2^-53. |
| `GershgorinDisk` | Row index, center, estimated/upper radius, estimated/outward endpoints, and `CertifiedFinite`. Unrepresentable diagnostic fields are nullable. |
| `Interval` | Immutable finite endpoints, validated `Left <= Right`. |
| `GershgorinResult` | Disks, `MergedIntervals`, `MergedOutwardIntervals`, radius statistics, envelope/union widths and warnings. |
| `GershgorinBounds` | `Analyze`, `MergeIntervals`, `DistanceToUnion`. Partial finite interval lists are accompanied by failure flags/warnings. |
| `JacobiRotation` | `Compute(app,aqq,apq)` returns `C`, `S`, `T`; `Apply(matrix,vectors,p,q)` mutates the supplied work matrices and returns false for an exactly zero pivot. |
| `PivotSearch` | `FindMaximum` returns `P`, `Q`, magnitude and inspected count; ties use the first upper-triangle pair in row-major order. |
| `JacobiOptions` | Policy, tolerances, rotation/sweep limits, trace stride/cap and nullable snapshot cap; defaults below. |
| `JacobiSolver` | `Solve(input,options,CancellationToken)` preserves input and returns a `JacobiResult`. `ValidateOptions` is also public. |
| `JacobiResult` | Status, sorted original/scaled eigenvalues, eigenvectors, transformed matrix, operation counts, scale/underflow count, norms, trace, snapshots and warnings. |
| `TraceSample` | Long rotation/pivot-visit indices, optional completed sweeps, direct scaled/original off-norm and relative off-norm. Original norm is nullable if unrepresentable. |
| `MatrixSnapshot` | Rotation, order, scale, label and a deep copy of the scaled transformed matrix before eigenpair sorting. |
| `SpectrumDiagnostics` | `Analyze(original,result,options)` returns `AccuracyResult` or null when eigenpairs are unavailable; `Orthogonality` checks vector columns. |
| `AccuracyResult` | Per-eigenpair residuals/norms/distances, aggregate residual, reconstruction, orthogonality, invariant drift, strict/tolerant containment and warnings. |
| `FixtureResult` | Matrix, family/input metadata, optional analytical spectrum and construction orthogonality. Metadata never enters the solver. |
| `DeterministicRandom` | SplitMix64 integer generator and upper-53-bit uniform conversion; unchecked UInt64 wraparound is intentional. |

## Options and results

| Option | Default and accepted values |
|---|---|
| `Policy` | `MaxAbsolute`; alternatively `Cyclic` |
| `RelativeTolerance` / `AbsoluteTolerance` | 1e-12 / 0; finite, nonnegative, at least one positive |
| `MaxRotations` | null derives checked `100*max(1,n*(n-1)/2)`; explicit limits are positive `long` |
| `MaxSweeps` | 100, positive; applies to cyclic only |
| `TraceStride` | 1, positive `long` |
| `MaxTraceSamples` | 4096; accepted 0 or 2..4096 |
| `MaxSnapshots` | null selects 6 for n<=64, otherwise 0; explicit values are 0 or 2..6 |

Statuses are `Converged`, `RotationLimit`, `SweepLimit`, `Stagnated`, `Cancelled`, and `NumericFailure`. Budgets/stagnation/cancellation retain finite intermediate eigenpairs; unrepresentable eigenvalues cause numerical failure with omitted eigenpairs. `CompletedSweeps` is null for maximum pivots. `EquivalentSweeps` is rotations divided by pair count, or zero for scalar matrices.

Invalid matrices and solver options raise argument exceptions. Invalid dimensions fail before backing-array allocation. Numerical failure, exhausted budgets and cancellation from `Solve` are results rather than exceptions. CLI IO failures are handled separately.

## Ownership and observation

Matrix factories and `CopyData` copy their arrays. Public indexers cannot modify matrices. `JacobiRotation.Apply` explicitly mutates its matrix arguments, so callers using the low-level kernel should supply clones. `Solve` creates its own working matrix and eigenvectors.

Result collections are read-only. Result matrix getters and `MatrixSnapshot.Matrix` return copies, preserving internal ownership. Eigenvectors are allocated even when a caller only displays eigenvalues.

The bounded trace retains initial/final samples, compacts alternate interior samples when full, and doubles its effective stride. The result reports that effective stride. Every recorded norm is direct. An initially converged run needs one trace sample; snapshot reporting keeps separate initial/final panels at rotation zero. Snapshot capture uses initial, rotations 1,2,4,8 while capacity allows, and a reserved final slot.

`ExperimentRunner` wraps the solver with clocks outside Core. `BenchmarkRunner` disables trace and snapshots for timed solves. Observer callbacks cannot change solver behavior.

## Maintaining numerical correctness

Use the sign convention in [NUMERICS.md](NUMERICS.md), mirror symmetric writes, and save both affected values before updates. Keep reductions and pivot ties deterministic. Validate accuracy on the original matrix rather than the diagonalized work matrix. Analytical fixture spectra are independent oracles, never solver inputs. Re-run the [acceptance tests](TEST_PLAN.md) after numerical changes and record measured results in [VALIDATION.md](VALIDATION.md).
