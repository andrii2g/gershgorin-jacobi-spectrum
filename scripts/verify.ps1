$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if (!(Test-Path 'tests/Spectrum.Tests/Spectrum.Tests.csproj')) { throw 'Task 01 must create the test project first.' }
function Invoke-Dotnet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" } }
Invoke-Dotnet restore GershgorinJacobiSpectrum.slnx --locked-mode
Invoke-Dotnet build GershgorinJacobiSpectrum.slnx -c Release --no-restore
Invoke-Dotnet test GershgorinJacobiSpectrum.slnx -c Release --no-build
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- demo --out artifacts/verification-demo --overwrite
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- compare --sizes 4,8,16 --repetitions 2 --out artifacts/verification-compare --overwrite
