# References and usage

The project implements its numerical kernels directly. These resources provide background reading; none is a runtime dependency.

- LAPACK Working Note 150, stable Givens/Jacobi rotation computation: https://www.netlib.org/lapack/lawnspdf/lawn150.pdf
- LAPACK symmetric eigenproblem overview: https://www.netlib.org/lapack95/lug95/node33.html
- Wolfram Gershgorin construction and eigenvalue visualization: https://www.wolfram.com/language/12/complex-visualization/gerschgorin-disks.html
- .NET documentation entry point: https://learn.microsoft.com/dotnet/

Jacobi refers here to the symmetric eigenvalue plane-rotation algorithm, not the Jacobi stationary iteration for solving linear systems. The project's disk bounds are spectral localization, not interval-certified eigendecomposition.
