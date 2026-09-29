# Reports and serialization

Reports use schema version 1. JSON properties have stable ordering, enums are readable strings, finite doubles are numbers, and seeds are decimal strings to preserve all 64 bits. Files are UTF-8 with LF line endings and no timestamps. Per-run reproducibility comparisons omit `timings` and `environment`.

## Per-run files

| File | Contents |
|---|---|
| `summary.json` | Case metadata, options, status/counters, bounds, accuracy, eigenvalues, timings, environment and warnings |
| `eigenpairs.csv` | Sorted eigenvalue estimates with original-matrix residuals and containment diagnostics |
| `disks.csv` | Original and captured-snapshot disk data in original units; row ids retain matrix order |
| `convergence.csv` | Bounded direct off-norm samples |
| `gershgorin.svg` | Original matrix disks, real interval strips and final eigenvalue estimates |
| `convergence.svg` | Direct relative off-norm against applied rotations |
| `snapshots.svg` | Present only when snapshots are enabled |
| `matrix.json` | Replay input, exported by demo and input solves |
| `eigenvectors.csv` | Exported by demo and input solves; columns follow sorted eigenvalues |

Numerical failure may omit eigenpairs. Corresponding CSV files keep their headers even when no rows are available. No report certifies the computed eigenvalues.

## Summary JSON

| Group | Key contents |
|---|---|
| `schemaVersion` | 1 |
| `case` | `name`, nullable `family`, boolean `input`, `size`, nullable seed/generator version, canonical `matrixSha256`, family `parameters` |
| `options` | `pivotPolicy`, `rtol`, `atol`, resolved `maxRotations`, nullable `maxSweeps`, trace configuration and resolved snapshot cap |
| `solver` | Status/converged flag; long rotation, pivot-visit and search counts; completed/equivalent sweeps; scale/underflow count; initial norm, initial/final direct off-norm, relative off-norm, scaled threshold and effective trace stride |
| `bounds` | Center/radius statistics; estimated/outward merged intervals; envelope width, union length, finite/completeness flags and floating-point policy |
| `accuracy` | Per-eigenpair residuals/norms/distances; maximum/aggregate residuals; orthogonality; reconstruction; absolute scaled/relative invariant drifts; strict/tolerant containment and warnings. Null when eigenpairs are unavailable. |
| `eigenvalues` | Sorted finite array, or null on numerical failure |
| `timings` | `gershgorinMs`, `solverMs`, `diagnosticsMs`, `renderingMs`; nullable when unmeasured |
| `environment` | Framework, OS, process architecture, processor count and build configuration |
| `numericPayloadBytes` | Baseline matrix payload estimate |
| `approximatePeakMatrixPayloadBytes` | Conservative matrix payload estimate including extra copies |
| `timingScope` | Explains that this solve timing includes educational observation |
| `warnings` | Explanations for numerical limitations and unrepresentable diagnostics |

Unrepresentable diagnostics are null, never NaN/Infinity substitutes. `estimateComplete=false` means a finite interval list omits unrepresentable endpoints and is not a complete enclosure. Outward scalar enclosures concern the mathematical matrix spectrum, not the accuracy of numerical eigenvalue estimates.

A null scaled threshold/containment tolerance caused by overflow denotes an overwhelmingly permissive requested tolerance, explained in warnings. `maxSweeps=null` and `completedSweeps=null` for maximum pivots mean inapplicable, not numerical failure.

## CSV

CSV uses invariant `G17` doubles, comma separators, headers and RFC4180 quoting. Unrepresentable or unavailable fields are empty. Headers are:

```text
eigenpairs.csv:
index,eigenvalue,residualAbsolute,residualNormalized,vectorNorm,distanceToOriginalUnionScaled,containedWithTolerance

disks.csv:
snapshot,rotation,row,center,radiusEstimate,radiusUpper,left,right,outwardLeft,outwardRight,certifiedFinite

convergence.csv:
rotation,pivotVisit,completedSweeps,offNormScaled,offNormOriginal,relativeOffNorm,direct

eigenvectors.csv:
row,col,value
```

`row` in disk/snapshot output is a matrix row id. `index` in eigenpair output is a sorted eigenpair id. Snapshots precede sorting; these identifiers are intentionally different.

## SVG interpretation

SVGs have no scripts, remote assets or external font dependencies. Disk and convergence reports use a 1100×650 viewBox; snapshot height varies with panel count. The [README chart gallery](../README.md#charts-from-the-application) includes reproducible examples.

Disk plots use equal x/y scale. Coordinates are divided by the labelled display unit (matrix scale, or one for the zero matrix), keeping extreme finite inputs displayable. CSV retains original units. Eigenvalue estimates appear at imaginary coordinate zero; interval strips align horizontally. Zero radii use crosses rather than inflated circles. Exactly repeated estimates have multiplicity labels. Opacity adapts for overlapping disks.

Convergence plots show `log10(off(B)/||B0||F)` versus applied rotations, using direct samples only. The relative tolerance threshold is drawn. Zero samples sit at a disclosed display floor and carry a zero annotation; a one-sample run still has a point and sensible x range. Comparison plots distinguish policies by color, labels and line styles, and explain that equal rotations do not imply equal time.

Snapshots share coordinate limits computed from every captured set of disks and the final estimates. Capture order is initial, rotations 1,2,4,8 while capacity permits, and final before eigenpair sorting. Every panel labels its actual rotation and direct off-norm. An initially converged run has separate initial/final panels at rotation zero. Final estimates appear in every panel; matrix rows are never retroactively reordered.

## Comparison outputs

`comparison.csv` has this header:

```text
family,size,seed,matrixSha256,pivot,status,rotations,pivotVisits,searchComparisons,completedSweeps,equivalentSweeps,finalRelativeOffNorm,maxNormalizedResidual,orthogonalityFrobenius,solverMedianMs,solverMinMs,solverMaxMs,gershgorinMedianMs,repetitions
```

`comparison.json` stores the protocol/environment, per-case metadata, numerical results, timing statistics, raw solver samples (including status/counters/residuals), raw disk timings, repetitions/warmups and timed observation settings. Timings are not reproducible numerical data; compare metadata, counters and accuracy separately when inspecting repeated comparisons.

Each case/policy has its educational per-run files. Each family/size pair also has a combined convergence chart. `index.md` links reports and explains bounds versus eigensolve work. Failed solves remain represented with their statuses.
