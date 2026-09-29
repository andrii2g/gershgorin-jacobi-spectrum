# Test coverage and numerical acceptance

The xUnit project in `tests/Spectrum.Tests` validates numerical kernels, solver policies, diagnostics and CLI integration. Test package versions and their resolved dependency graph are pinned in the project and lock file; production projects use only the BCL. Run `dotnet test GershgorinJacobiSpectrum.slnx -c Release` after locked restore. Current executed results are recorded in [VALIDATION.md](VALIDATION.md). The cases below define the maintained acceptance contract.

## Exact and analytical cases
| Input | Expected |
|---|---|
| [5] | lambda=5, V=[1], zero rotations, zero residual |
| zero matrices | all zero, V=I, zero rotations; solver and CLI coverage |
| diag(3,-2,3,0) | sorted -2,0,3,3; matching permuted eigenvectors |
| [[2,1],[1,2]] | 1,3; one rotation; sign convention verified |
| [[2,-1],[-1,2]] | 1,3; negative equal-diagonal pivot branch |
| [[1,2],[2,4]] | 0,5; semidefinite and tiny-eigenvalue absolute error |
| [[1,1e-150],[1e-150,2]] | finite stable kernel, no tau overflow pathway |
| block demo | -2,1,3,4,5,7 |
| Toeplitz 2,-1; n=2,3,8,32 | cosine analytical spectrum |

Ordinary fixtures n<=64, rtol=1e-12: sorted spectral error <=5e-10*max(1,||A||F), aggregate normalized residual <=5e-11, orthogonality <=5e-11, relative reconstruction <=1e-10. These are acceptance ceilings, not claimed typical errors. Tight 2x2 tests use ~1e-14 scale-aware tolerance. Check both strategies. Check residuals even when analytical eigenvalues match.

## Kernel and invariants
- Compare one rotation to independently multiplied J^T B J and VJ for seeded 3x3 and 5x5 matrices.
- Assert symmetry exactly after each mirrored update; trace and Frobenius invariance with <=128*u*n*max(1,norm) style scale-aware allowances.
- Verify off-norm squared reduction identity away from roundoff floor; avoid zero-tolerance monotonicity near convergence.
- Ensure caller matrix unchanged bit-for-bit after Solve.
- Maximum-pivot ties choose lexicographically first p,q. Count search comparisons exactly for a single scan.
- Sorted eigenvalues and eigenvector columns stay paired; compare residual before/after sorting.
- Repeated eigenvalues: use residual and orthogonality, not exact eigenvectors. Sign normalization first-maximum tie rule is tested separately.

## Bounds and intervals
- [[4,-1,2],[-1,3,0],[2,0,5]] gives centers 4,3,5 and radii 3,1,2; intervals [1,7],[2,4],[3,7], union [1,7].
- Disjoint [0,1],[3,4] gives union length 2, envelope width 4. Touching [0,1],[1,2] merges.
- Zero radius rendered and exported correctly. Radius mean not accidentally computed from interval width.
- Outward endpoints enclose hand-computable exact dyadic sums; test mixed-magnitude accumulation and overflow flag.
- Deliberately incorrect candidate eigenvalue inside a broad disk must fail residual tests, demonstrating containment insufficiency.
- Compare all computed eigenvalues with original union using documented tolerance, not just global envelope.

## Robustness and stopping
- Reject n=0, wrong data length, nonfinite matrix/tolerances, negative budgets, nonsymmetry and checked dimension overflow before allocation.
- Scale a well-conditioned fixture by 1e-200 and 1e200; use normalized diagnostics and scale-aware spectral comparison. Do not use an absolute 1e-10 tolerance for tiny matrices.
- Test mixed dynamic range underflow count explicitly, finite values leading to unrepresentable spectrum, and no NaN/Infinity serialization.
- MaxRotations=1 on dense nontrivial n=5 returns RotationLimit with useful diagnostics; maxSweeps=1 tests cyclic SweepLimit.
- Initially converged case returns Converged regardless of unused positive limits. Budget reached exactly when convergence achieved returns Converged after final direct check.
- Cancel before start and during boundary checks; no half-updated matrix exposed.
- Trace estimate cancellation cannot cause false convergence; compare final direct norm to threshold.
- For stagnation, use a controlled cadence-monitor unit test rather than an architecture-sensitive hard matrix.

## Integration
- Exit codes cover help/convergence (0), invalid input (2), nonconvergence (4), numerical failure (5), IO failure (6) and cancellation (130).
- Read input JSON fixtures, produce all required files, parse JSON/CSV/XML, and assert known eigenvalues and counts.
- Compare deterministic output sections across two runs and two cultures (en-US, de-DE); timing and runtime metadata excluded.
- Initially zero off-norm: convergence SVG uses an annotated floor/zero marker, no log(0), NaN coordinate or missing plot.
- Trace cap, snapshot cap and large-n snapshot default hold without unbounded allocations.
- Compare command uses identical matrix hashes for both strategies. Failure in one run is exported, not silently omitted.
- Visually inspect circle aspect ratio, fixed snapshot axis, eigenvalue ticks at real y=0, readable legends, and negative eigenvalue labels.

No timing threshold in CI. Performance assertions use operation counts and absence of full matrix products inside the rotation loop.

## Test file map

| File | Coverage |
|---|---|
| FoundationTests.cs | Shape/finite/symmetry validation, copies, stable norms, interval unions/outward bounds, PRNG reference sequence |
| RotationTests.cs | Independent similarity multiplication, invariants, both equal-diagonal sign branches and tiny pivots |
| SolverTests.cs | Analytical spectra, both policies, budgets/counts, convergence, cancellation, underflow/overflow, observation caps |
| FixtureDiagnosticsTests.cs | All families at orders 4,8,16,32; repeated spectra, cross-scale residuals, hashes and containment insufficiency |
| AdditionalNumericalTests.cs | Order-64 families, mid-solve cancellation, snapshot ownership, exact dyadic bounds, exported subnormal residuals |
| CliIntegrationTests.cs | Parsing/replay, exit codes, JSON/CSV/SVG validity, cultures and deterministic output |
| BenchmarkTests.cs | Shared inputs, separate untraced runs, raw sample/counter consistency and median statistics |
