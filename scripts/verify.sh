#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
test -f tests/Spectrum.Tests/Spectrum.Tests.csproj || { echo 'Task 01 must create the test project first.' >&2; exit 3; }
dotnet restore GershgorinJacobiSpectrum.slnx --locked-mode
dotnet build GershgorinJacobiSpectrum.slnx -c Release --no-restore
dotnet test GershgorinJacobiSpectrum.slnx -c Release --no-build
dotnet run --project src/Spectrum.Cli -c Release --no-build -- demo --out artifacts/verification-demo --overwrite
dotnet run --project src/Spectrum.Cli -c Release --no-build -- compare --sizes 4,8,16 --repetitions 2 --out artifacts/verification-compare --overwrite
