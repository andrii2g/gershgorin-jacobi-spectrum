# Decisions

| Decision | Reason |
|---|---|
| Flat src projects, root tests | Small navigable repository |
| Full row-major storage | Transparent symmetric rotations; simpler than packed indexing |
| MaxAbsolute plus Cyclic | Classical algorithm and explicit pivot-search trade-off |
| Exact input symmetry | Avoid hiding a user matrix perturbation |
| Scale by maximum absolute entry | Protect ordinary inputs from avoidable overflow |
| Residuals on original matrix | Detect wrong rotation/eigenvector convention |
| Bounded direct trace samples | Trustworthy charts with controlled memory |
| Estimated disks plus outward interval endpoints | Separate theorem from floating-point implementation claims |
| Pinned test dependencies and locked restore | Reproduce the resolved compatible test package graph |

These choices preserve the conventions in [NUMERICS.md](NUMERICS.md). Changes to numerical behavior require corresponding test and validation evidence.

## Implemented choices and clarifications

- `MaxSnapshots` is nullable: null selects six for n<=64 and zero for n>64. Explicit 0 disables it; explicit 2..6 works for any order. Trace caps accept 0 or 2..4096; one sample cannot preserve both endpoints. Snapshot caps accept 0 or 2..6.
- Initially converged runs have one direct trace sample and separate initial/final snapshot panels at rotation zero. Later captures occur at rotations 1,2,4,8 while space remains, with final always reserved.
- Related small types share files (bounds records in GershgorinBounds.cs, stagnation monitor in PivotSearch.cs). Tests are grouped by numerical/integration responsibility. No numerical dependency was introduced.
- Matrix getters on results/snapshots return copies. Collections are read-only; fixtures contain an externally immutable DenseMatrix. The public low-level rotation kernel mutates its explicitly supplied work matrices; callers should pass clones when preserving inputs.
- Unrepresentable Gershgorin fields are nullable with warnings, finite flags, and `estimateComplete` in JSON. A partial finite interval list must not be mistaken for a complete enclosure. Solver eigenvalue overflow produces NumericFailure. Diagnostics use scaled original data, independently of the transformed matrix.
- SVG coordinates use A/scale and eigenvalues/scale to avoid overflow during display. The display unit is labelled; the zero matrix uses display unit one. All panels use equal x/y scale and one shared domain. CSV retains original units. SVG colors and opacity adapt for overlapping disks; repeated equal estimates carry multiplicity labels.
- The payload estimate `numericPayloadBytes` is the baseline original/work/vector/snapshot storage. `approximatePeakMatrixPayloadBytes` conservatively includes sorting and defensive diagnostic copies (seven matrices plus snapshots); neither is a measured allocation or RSS value.
- The Windows verification script quotes comma-separated sizes because its wrapper function otherwise expands them. The unsigned-script override is process-scoped and does not change system execution policy.
- The test project uses xUnit 2.9.3, runner 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, and coverlet.collector 6.0.4. Lock files capture the resolved transitive closure. Production projects remain BCL-only.
- No acceptance tolerance was loosened. Kernel/invariant tolerances use scale-aware binary64 allowances; analytical spectra and original residuals remain independent acceptance checks.
