$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Invoke-Dotnet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" } }
Invoke-Dotnet restore GershgorinJacobiSpectrum.slnx --locked-mode
Invoke-Dotnet build GershgorinJacobiSpectrum.slnx -c Release --no-restore
Invoke-Dotnet test GershgorinJacobiSpectrum.slnx -c Release --no-build
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- demo --out artifacts/verification-demo --overwrite
foreach ($policy in @('max', 'cyclic')) {
    Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --family toeplitz --size 32 --pivot $policy --out "artifacts/verification-toeplitz-$policy" --overwrite
}
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --family near-diagonal --size 4 --epsilon 0 --out artifacts/verification-diagonal --overwrite
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --input examples/zero-3x3.json --out artifacts/verification-zero --overwrite
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- compare --sizes '4,8,16' --repetitions 2 --out artifacts/verification-compare --overwrite
foreach ($runNumber in @(1, 2)) {
    Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- solve --family random --size 16 --seed 42 --out "artifacts/verification-repeat-$runNumber" --overwrite
}
& (Join-Path $PSScriptRoot 'audit-artifacts.ps1')
