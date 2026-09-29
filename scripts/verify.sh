#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore GershgorinJacobiSpectrum.slnx --locked-mode
dotnet build GershgorinJacobiSpectrum.slnx -c Release --no-restore
dotnet test GershgorinJacobiSpectrum.slnx -c Release --no-build
dotnet run --project src/Spectrum.Cli -c Release --no-build -- demo --out artifacts/verification-demo --overwrite
for policy in max cyclic; do
  dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --family toeplitz --size 32 --pivot "$policy" --out "artifacts/verification-toeplitz-$policy" --overwrite
done
dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --family near-diagonal --size 4 --epsilon 0 --out artifacts/verification-diagonal --overwrite
dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --input examples/zero-3x3.json --out artifacts/verification-zero --overwrite
dotnet run --project src/Spectrum.Cli -c Release --no-build -- compare --sizes 4,8,16 --repetitions 2 --out artifacts/verification-compare --overwrite
