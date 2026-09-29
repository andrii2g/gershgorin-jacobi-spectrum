# Docker verification

The verification stage builds the implemented solver, runs the locked test suite and generates the small example/comparison artifacts. Docker is unavailable on the validation host, so these image commands have not been executed there; see VALIDATION.md.

```bash
docker build --target verify -t spectrum-verify .
docker build -t gershgorin-jacobi-spectrum .
mkdir -p artifacts/container
docker run --rm --user "$(id -u):$(id -g)" \
  -v "$PWD/artifacts/container:/out" \
  gershgorin-jacobi-spectrum demo --out /out/demo
```

Run from a Linux/WSL shell. The numeric host UID allows writing to the bind mount. Default runtime image user is the image-provided nonroot app user; ensure a writable output mount. No privileged flag, Docker socket, network access or host credentials needed at runtime. Image build needs access to Microsoft images and NuGet for test restore. Tags are convenient defaults, not immutable pins; pin SDK/runtime image digests in a release if exact build reproducibility is required.
