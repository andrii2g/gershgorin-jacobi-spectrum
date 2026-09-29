# README charts

These SVGs are application output, stored alongside the documentation so they render on GitHub without generating local artifacts first. They contain no timings or timestamps.

| File | Data and method |
|---|---|
| [analytic-disks.svg](analytic-disks.svg) | Fixed 6×6 block demonstration; maximum-absolute pivots; exact reference spectrum −2, 1, 3, 4, 5, 7 |
| [random-convergence.svg](random-convergence.svg) | Random symmetric n=16, seed=42, both policies; direct relative off-norm against applied rotations |
| [random-snapshots.svg](random-snapshots.svg) | Random symmetric n=6, seed=42, maximum-absolute pivots; fixed axes across initial, intermediate and final panels |

All solves use default tolerances (`rtol=1e-12`, `atol=0`). Disk/snapshot axes use the labelled display unit; CSV exports retain original units. Convergence compares rotations, not elapsed time. No benchmark speed claim is made by these images.

Regenerate from the repository root with PowerShell 7 and a .NET 10 SDK:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/update-readme-charts.ps1
```

The script performs locked restore and a Release build, generates reports under `artifacts/readme-demo` and `artifacts/readme-compare`, and copies only these three SVGs here. The comparison command uses one repetition and no warmup because only its deterministic convergence chart is published. Use the normal comparison defaults for measurements.

Chart bytes are reproducible within the validated runtime/arithmetic environment. Cross-architecture floating-point identity is not guaranteed.
