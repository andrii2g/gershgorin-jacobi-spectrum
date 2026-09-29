$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" }
}
Invoke-Dotnet restore GershgorinJacobiSpectrum.slnx --locked-mode
Invoke-Dotnet build GershgorinJacobiSpectrum.slnx -c Release --no-restore
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- demo --out artifacts/readme-demo --overwrite
Invoke-Dotnet run --project src/Spectrum.Cli -c Release --no-build -- compare --sizes 16 --families random --seed 42 --warmups 0 --repetitions 1 --out artifacts/readme-compare --overwrite

$images = Join-Path (Get-Location) 'docs/images'
New-Item -ItemType Directory -Path $images -Force | Out-Null
$sources = [ordered]@{
    'analytic-disks.svg' = 'artifacts/readme-demo/analytic-max/gershgorin.svg'
    'random-convergence.svg' = 'artifacts/readme-compare/random-16/convergence.svg'
    'random-snapshots.svg' = 'artifacts/readme-demo/random-max/snapshots.svg'
}
foreach ($name in $sources.Keys) {
    $source = $sources[$name]
    $svg = [xml][IO.File]::ReadAllText((Join-Path (Get-Location) $source))
    if ($svg.DocumentElement.LocalName -ne 'svg') { throw "Not SVG: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $images $name) -Force
    Write-Output "Updated docs/images/$name"
}
