# Deterministic fixtures

Use SplitMix64 with state += 0x9E3779B97F4A7C15; z=(z^(z>>30))*0xBF58476D1CE4E5B9; z=(z^(z>>27))*0x94D049BB133111EB; return z^(z>>31). All arithmetic unchecked UInt64. Uniform01=(NextUInt64()>>11)*2^-53. UniformSigned=2*Uniform01-1. Record seed as unsigned decimal string in JSON to preserve all 64 bits. Generator version: splitmix64-v1.

Generate entries in upper-triangle row-major order. Use independent generator instance for every fixture; never share state across experiments. Same seed/family/order/options gives the same matrix within a fixed runtime arithmetic environment. Hash canonical row-major binary64 little-endian bytes for fixture identification; normalize signed zero first. Cross-architecture trigonometric differences may change hashes for rotation-generated fixtures.

## Families and defaults
1. `dominant`: off-diagonal UniformSigned/n, mirrored; radius computed after off-diagonal fill; diagonal radius+1+i/n. Strict positive diagonal dominance is guaranteed mathematically. Include a custom separated diagonal example in tests; default disks need not be disjoint.
2. `toeplitz`: a=2, b=-1 by default. Diagonal a; first off-diagonals b; remaining zero. Spectrum a+2*b*cos(k*pi/(n+1)), k=1..n; sort ascending. Parameters --diagonal and --off-diagonal finite.
3. `clustered`: delta=1e-6 default. lambda_i = base_(i mod 3) + delta*floor(i/3)/max(1,n), bases {1,2,4}. Build Q=I then 4*n plane rotations. Pick p from NextUInt64()%n, choose q != p by drawing modulo n-1 and shifting; angle=(2*Uniform01-1)*pi. n=1 bypass. Form A_ij=sum_k Q_ik*lambda_k*Q_jk once for i<=j and mirror, using compensated dot products. Intended spectrum is an approximate oracle because construction rounds; validate Q orthogonality and residuals against actual A. Repeated variant delta=0.
4. `near-diagonal`: diagonal 1+i, off-diagonals epsilon*UniformSigned/n with epsilon=1e-6 default. epsilon=0 produces a diagonal matrix; reject negative or nonfinite epsilon.
5. `random`: upper triangle including diagonal UniformSigned/sqrt(n), mirrored. Matrix is not assumed positive definite. Generate diagonal in the same upper-triangle loop.

## Fixed demo
The fixed demonstration matches `examples/demo-6x6.json`: two 2x2 blocks and two scalar entries. Eigenvalues sorted {-2,1,3,4,5,7}. First two nontrivial blocks have equal diagonals and opposite off-diagonal signs, exercising both sign branches. The default demonstration shows initial, intermediate and final regions and explain that some eigenvalues lie in overlapping disks. A seed-42 random n=6 second demonstration supplies denser trajectories; its spectrum is not used as the analytic oracle.

## Size policy
Default solve n=16. Default comparison sizes 8,16,32,64; CI uses 4,8,16 only. CLI rejects n>512 unless --allow-large is set; checked array indexing still imposes a hard limit. Do not promise practical million-element dense eigensolves. Classical full pivot scans become expensive well before memory is exhausted.
