# Command-line reference

Commands are `help`, `demo`, `solve`, and `compare`. No arguments, `--help`, or `-h` displays help; `solve --help`, `demo --help` and `compare --help` also work. Run through `dotnet run --project src/Spectrum.Cli -c Release -- COMMAND`, or invoke a built `Spectrum.Cli.dll` with `dotnet`.

Unknown/duplicate options, missing values and incompatible combinations are rejected. Decimal numbers use invariant culture. Quote paths containing spaces and comma-separated lists in PowerShell. No network or shell invocation is required inside the application.

## Solve

Choose either `--family NAME` or `--input path.json`. If neither is present, the family defaults to `random`. Input files cannot be combined with size, seed or family-specific options.

| Option | Default | Meaning |
|---|---|---|
| `--family` | `random` | `dominant`, `toeplitz`, `clustered`, `near-diagonal`, `random` |
| `--size` | 16 | Positive order; >512 requires `--allow-large` |
| `--seed` | 42 | Unsigned 64-bit decimal seed |
| `--pivot` | `max` | `max` or fixed-order `cyclic` |
| `--rtol`, `--atol` | 1e-12, 0 | Finite nonnegative tolerances; at least one positive |
| `--max-rotations` | 100*max(1,n*(n-1)/2) | Positive long integer |
| `--max-sweeps` | 100 | Positive integer; cyclic only |
| `--trace-stride` | 1 | Positive long integer |
| `--max-trace-samples` | 4096 | 0 disables trace; otherwise 2..4096 |
| `--snapshots` | 6 for n<=64, else 0 | 0 disables snapshots; otherwise 2..6 |
| `--out` | `artifacts/run` | Output directory |
| `--overwrite` | off | Permit replacement of generated files |
| `--allow-large` | off | Permit orders above 512 within checked array limits |

Family-specific options:

| Family | Options |
|---|---|
| `toeplitz` | `--diagonal 2`, `--off-diagonal -1`; both finite |
| `clustered` | `--cluster-delta 1e-6`; finite and nonnegative; zero creates repeated reference eigenvalues |
| `near-diagonal` | `--epsilon 1e-6`; finite and nonnegative; zero creates a diagonal matrix |

Unrelated family options are rejected. Input and budget validation occurs before output creation.

A minimal input file is:

```json
{
  "schemaVersion": 1,
  "name": "equal-diagonal",
  "matrix": [[2, 1], [1, 2]]
}
```

The matrix must be nonempty, square, finite and exactly symmetric. Extra nesting and approximate symmetry are rejected. Additional metadata such as `expectedEigenvalues` is ignored by the solver. Input solves export the matrix and eigenvectors for replay.

## Demo

`demo` solves the analytic block 6×6 and seed-42 random 6×6 cases with both policies. It accepts only `--out` (default `artifacts/demo`) and `--overwrite`. Reports go in separate case folders, with an explanatory `index.md` linking them.

## Compare

| Option | Default |
|---|---|
| `--sizes` | `8,16,32,64`; positive, ascending, distinct |
| `--families` | All five families; known and distinct |
| `--seed` | 42 |
| `--repetitions` | 5; positive |
| `--warmups` | 1; nonnegative |
| `--out` | `artifacts/comparison` |
| `--overwrite`, `--allow-large` | off |

Both strategies are always run with default solver tolerances. Each case has one traced educational run per policy, followed by separate untraced timing runs with alternating policy order. Fixture generation, validation and serialization are outside the solve timer; matrix cloning and eigenvector allocation are inside. Disk timing includes interval merging and batching. See [PERFORMANCE.md](PERFORMANCE.md).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Converged work or help |
| 2 | Invalid input/options |
| 4 | Finite but unconverged result: rotation/sweep limit or stagnation |
| 5 | Numerical failure |
| 6 | IO failure, including nonempty output without `--overwrite` |
| 130 | Cancellation; Ctrl+C stops at a rotation boundary |

In comparisons, numerical failure takes priority over ordinary nonconvergence. IO/cancellation terminates immediately. Error messages go to stderr; summaries go to stdout. Available numerical-failure metadata is exported.

## Filesystem behavior

Nonempty output directories require `--overwrite`. Only documented generated names are replaced atomically through temporary files; unrelated files are preserved. Disabling optional outputs removes their previous generated files. Previously generated case folders outside a later comparison's selected cases are retained; use a fresh directory when collecting a clean comparison set. Case folder names derive from known families, sizes and policies, never input display names. XML text is escaped.
