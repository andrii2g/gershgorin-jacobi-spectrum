# Measurement protocol

Gershgorin construction is O(n^2) for dense input; storage O(n) beyond input, sorting intervals O(n log n). A Jacobi matrix/eigenvector update is O(n). Maximum-pivot search scans n(n-1)/2 entries every rotation: naive n^2 rotations cost O(n^4) search work, versus O(n^3) update work. Do not describe this implementation as globally O(n^3) regardless of pivot strategy. Cyclic sweeps cost O(n^3) each. Neither a fixed sweep count nor quadratic local convergence implies a universal total-runtime bound for all input matrices.

Timing default n<=64 to keep experiments short. Optional 128/256 only after correctness, explicit large run for >512. Use Release, no debugger, Stopwatch, fixed matrices generated once. Both policies receive the same input matrix but allocate independent working copies and V. Perform one warmup then five timed repetitions; rotate execution order by repetition to reduce order bias. No forced GC inside measured intervals. Report median/min/max and raw samples, not only the fastest run. Keep one thread; record environment without asserting cross-machine comparability.

Run Gershgorin batches until >=20 ms total or a sensible iteration cap; report per-call elapsed. Consume results so computations cannot be elided. Explicitly state whether interval merging is included (yes by default). Validation and rendering have separate clocks. Trace/snapshots disabled in timed solves; operation counts and final direct convergence checks remain enabled. Educational run solverMs is labelled instrumented and must not be substituted for benchmark median.

Report approximate payload memory separately: original n^2 doubles + working n^2 + eigenvectors n^2 + snapshotCount*n^2, plus bounded trace. Label this numeric payload estimate, not process RSS or allocated-byte measurement. Diagnostic matrix products may allocate extra scratch; include it in documented peak estimate if used.

No hard speed target. Evidence of trade-off is ratios, operation counts and curves. Do not optimize pivot maintenance, vectorize, parallelize or introduce sparse storage before MVP verification.

## Implemented payload and observation details

`numericPayloadBytes` is 8*n*n*(3+snapshotCount), the baseline original/work/eigenvector payload. Defensive matrix getters, sorting, and diagnostics create additional copies; `approximatePeakMatrixPayloadBytes` uses a conservative 8*n*n*(7+snapshotCount). Both exclude object headers, bounded trace objects, serialization buffers and GC retention, and are estimates rather than RSS or allocation measurements. Diagnostic products use compensated scalar dots rather than allocating full product matrices. The benchmark excludes these diagnostics and turns observations off.
