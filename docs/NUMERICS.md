# Numerical specification

## 1. Domain and scaling
Matrix order n >= 1; n=0 is invalid. Data length must equal checked(n*n). All entries finite. Require exactly equal mirrored entries (a[i,j] == a[j,i]); +0 and -0 count equal. JSON input must meet this requirement. Do not silently accept approximate symmetry; any later symmetrization option must be explicit and report the perturbation.

Let scale = max(abs(A[i,j])). If zero, return zero eigenvalues, identity eigenvectors, zero rotations and Converged. Otherwise work with B=A/scale. Preserve original A. Values lost to underflow during scaling are a limitation: count nonzero original entries that become zero and report scalingUnderflowCount. For ordinary fixtures this must be zero. Do not claim high relative accuracy of tiny eigenvalues across arbitrary dynamic ranges.

All convergence calculations use scaled units. Compute ||B0||F by scaled sum-of-squares. Threshold = atol/scale + rtol*||B0||F. Compare safely if atol/scale overflows: an enormous absolute tolerance permits immediate convergence; do not serialize infinity. Both tolerances finite and nonnegative, and at least one positive. Defaults atol=0, rtol=1e-12. Relative tolerances below 16*u, u=2^-53, are allowed but flag as demanding and may stagnate; never silently change a user tolerance.

Unscaled eigenvalues, radii or norms can overflow even with finite matrix entries. Nonfinite result conversion must return NumericFailure, or mark an optional unrepresentable diagnostic as null with an explicit diagnostic warning. Never write NaN or Infinity to JSON or silently clamp a spectral endpoint.

## 2. Rotation convention (authoritative)
For p<q, use J[p,p]=J[q,q]=c, J[p,q]=s, J[q,p]=-s and B'=J^T B J. Accumulate V'=VJ. This sign convention applies to every equation below.

Read app=B[p,p], aqq=B[q,q], b=B[p,q]. If b==0: skip without counting a rotation. Let d=(aqq-app)/2; division by 2 may be computed as aqq/2-app/2 to avoid overflow in a generic kernel.

If d==0, choose t=CopySign(1,b). Otherwise:
  m = max(abs(d),abs(b))
  h = sqrt((d/m)^2 + (b/m)^2)
  t = (b/m) / (d/m + CopySign(h,d))
Then c=1/sqrt(1+t*t), s=t*c. This is equivalent to the stable small-magnitude root using tau=(aqq-app)/(2*b), without forming a potentially overflowing tau or subtracting nearly equal numbers. |t|<=1 (up to rounding). Check parameters are finite.

For each k != p,q, save x=B[k,p], y=B[k,q]:
  B[k,p] = c*x - s*y
  B[k,q] = s*x + c*y
Mirror each result to B[p,k], B[q,k].
Set:
  B[p,p] = app - t*b
  B[q,q] = aqq + t*b
  B[p,q] = B[q,p] = 0
For every row k of V, save x=V[k,p], y=V[k,q]:
  V[k,p] = c*x - s*y
  V[k,q] = s*x + c*y

Never compute matrix products per rotation. The kernel must be tested against explicit J^TBJ for small matrices using independently coded multiplication. Use a generic stable norm utility for overflow-safe input norms.

## 3. Pivot policies and budgets
MaxAbsolute: scan upper triangle in row-major (p then q) order. Update winner only on strictly greater absolute value so ties choose the first pair. Search O(n^2), rotation O(n). Count each off-diagonal candidate inspected. If maximum is zero, recompute off-norm and finish. It is incorrect to call a batch of arbitrary maximum pivots a true cyclic sweep.

Cyclic: visit p=0..n-2, q=p+1..n-1 every sweep. Skip exactly zero pivots only in MVP; no arbitrary local dropping threshold. Check convergence at sweep boundaries and after budget exhaustion. Record completed sweeps only after all pairs have been visited. Mid-sweep cancellation or budget limits leave an incomplete sweep.

Default maxSweeps=100 for cyclic. Default maxRotations=100*n*(n-1)/2 for either policy, calculated with checked long arithmetic; scalar handled separately. Limits must be positive. MaxAbsolute ignores maxSweeps (CLI rejects that option for max). Equivalent sweeps = rotations/pairCount, zero for n=1. A scan does not count as a rotation. Trace and all outputs use long rotation indices.

## 4. Off-diagonal norm and convergence
off(B)^2=2*sum(p<q, B[p,q]^2). Initial direct norm, then maintain an estimate S_new=S_old-2*b*b for each rotation. Small negative values from roundoff trigger direct recomputation; do not infer convergence from max(0,S).

Directly recompute at least every pairCount applied rotations (min cadence 1), at cyclic sweep boundaries, whenever the estimate reaches the convergence threshold, and before final result. If estimated and direct values disagree appreciably, reset to direct. The estimate may suffer catastrophic cancellation near convergence.

For educational trace samples, directly recompute the norm at each recorded sample; label these as direct. Benchmark mode must not pay that cost per rotation. An estimate can trigger a convergence check, but only a direct norm establishes Converged.

Exact arithmetic identity: off(B')^2 = off(B)^2 - 2*b^2. Verify with a tolerance proportional to machine precision times ||B||F^2. Do not demand bitwise monotonicity near the roundoff floor.

Every exit has explicit status: Converged, RotationLimit, SweepLimit, Stagnated, Cancelled, NumericFailure. Detect stagnation when three consecutive complete cadence checks fail to reduce direct off-norm by more than 8*u*||B0||F and the threshold is not met. Finalize available finite eigenpairs on budget/stagnation exits, flag them unconverged. NumericFailure may omit eigenpairs. Cancellation stops at a rotation boundary.

## 5. Final eigenpairs
Diagonal B times scale yields eigenvalue estimates. Stable sort ascending using original column index for ties. Reorder V columns with the same permutation. Normalize eigenvector sign: largest absolute component, first index on ties, must be nonnegative. This is a display convention and does not fix a unique basis in a repeated eigenspace.

Keep trace/snapshot row order as it existed during the solve; do not retroactively sort matrix rows. Final transformed matrix snapshot precedes eigenpair sorting. Distinguish matrix row ids from eigenpair ids in exports.

## 6. Validation
For unit-length v_i: r_i=||A0*v_i-lambda_i*v_i||2. To avoid intermediate overflow evaluate using B0 and lambda_i/scale, report normalizedResidual_i=||B0*v_i-lambdaScaled_i*v_i||2/||B0||F; zero matrix defines zero. Absolute residual is scaled back if representable, otherwise null with warning.
Aggregate relative residual = ||B0*V-V*LambdaScaled||F/||B0||F. Orthogonality = ||V^T V-I||F (absolute). Relative reconstruction = ||B0-V*LambdaScaled*V^T||F/||B0||F. Compute diagnostics outside solver timings. Original residuals must not use the diagonalized working matrix.
Trace drift and Frobenius drift compare scaled initial/final matrices; report absolute scaled drift and relative drift with a nonzero reference. For zero trace, do not divide by trace; use ||B0||F as reference.

## 7. Gershgorin policy
center_i=a_ii; radius_i=sum(j!=i,abs(a_ij)). Compute on original A initially, and scaled snapshots with rescaling for display. Use compensated summation for radius estimates. Intervals [center-radius,center+radius]. Merge sorted intervals when next.left <= current.right; touching intervals merge. The union length is the sum of merged widths, not the global envelope width. Scalar and zero-radius disks are valid.

Gershgorin theorem applies exactly to the mathematical input matrix. Estimated floating-point radii are not certified enclosures. Include a separate outward interval mode: start radiusUpper=0; for each nonnegative term perform radiusUpper=BitIncrement(radiusUpper+term) (skip exact zero terms if desired); then left=BitDecrement(center-radiusUpper), right=BitIncrement(center+radiusUpper). Under ordinary IEEE binary64 round-to-nearest this conservatively encloses accumulated rounding. If an endpoint overflows, certifiedFinite=false and report an explicit failure/warning, not a finite claim. Plot central radius estimates; include outward endpoints in CSV and validation. This is scalar arithmetic enclosure, not certification of computed eigenvalues.

Approximate eigenvalues can fall just outside even a certified true-spectrum bound because of solver error. Report distance to interval union and tolerance used. Define containmentTolerance = 10*(atol+rtol*||A0||F) + 64*u*||A0||F, evaluated in scaled units. Also report strict estimate containment separately. Never call a numerical containment check proof of solver correctness.

Eigenvalues lie on the real axis. A connected isolated component formed by k disks contains k eigenvalues counting multiplicity in exact arithmetic; optional count diagnostics must group disks rather than assert one eigenvalue per disk. Disk centers/radii and union width need not shrink monotonically under rotation. Only total off-diagonal mass has the stated exact monotonic identity.
