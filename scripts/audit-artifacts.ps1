$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$directories = Get-ChildItem -LiteralPath (Join-Path $root 'artifacts') -Directory -Filter 'verification-*'
$summaries = @(); $jsonCount = 0; $csvCount = 0; $svgCount = 0
$invariant = [Globalization.CultureInfo]::InvariantCulture
foreach ($directory in $directories) {
    foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -Recurse -File) {
        $content = [IO.File]::ReadAllText($file.FullName)
        if ($content.Contains("`r")) { throw "Non-LF artifact: $($file.FullName)" }
        switch ($file.Extension) {
            '.json' {
                $value = ConvertFrom-Json -InputObject $content -Depth 100
                if ($content -match '(?<![A-Za-z])(NaN|Infinity)(?![A-Za-z])') { throw "Nonfinite JSON: $($file.FullName)" }
                $jsonCount++
                if ($file.Name -eq 'summary.json') {
                    if ($value.solver.status -ne 'Converged') { throw "Unconverged result: $($file.FullName)" }
                    if ($value.accuracy.aggregateRelativeResidual -gt 5e-11 -or $value.accuracy.orthogonalityFrobenius -gt 5e-11 -or $value.accuracy.relativeReconstruction -gt 1e-10) { throw "Accuracy failure: $($file.FullName)" }
                    if ($value.accuracy.toleranceContainedCount -ne $value.case.size) { throw "Containment failure: $($file.FullName)" }
                    $summaries += $value
                }
            }
            '.csv' {
                $rows = @(ConvertFrom-Csv -InputObject $content)
                foreach ($row in $rows) { foreach ($property in $row.PSObject.Properties) {
                    if ($property.Name -match '^(eigenvalue|residualAbsolute|residualNormalized|vectorNorm|distanceToOriginalUnionScaled|center|radiusEstimate|radiusUpper|left|right|outwardLeft|outwardRight|offNormScaled|offNormOriginal|relativeOffNorm)$' -and $property.Value -ne '') {
                        $number = [double]::Parse($property.Value, [Globalization.NumberStyles]::Float, $invariant)
                        if (![double]::IsFinite($number)) { throw "Nonfinite CSV field: $($file.FullName)" }
                    }
                } }
                $csvCount++
            }
            '.svg' {
                $xml = [xml]$content
                foreach ($element in $xml.SelectNodes('//*')) { foreach ($attribute in $element.Attributes) {
                    if ($attribute.Name -in @('x','y','cx','cy','r','x1','x2','y1','y2')) {
                        if (![double]::IsFinite([double]::Parse($attribute.Value, $invariant))) { throw "Nonfinite SVG coordinate: $($file.FullName)" }
                    }
                } }
                $svgCount++
            }
        }
    }
}
$first = Join-Path $root 'artifacts/verification-repeat-1'
$second = Join-Path $root 'artifacts/verification-repeat-2'
foreach ($file in Get-ChildItem -LiteralPath $first -File) {
    $a = [IO.File]::ReadAllText($file.FullName); $b = [IO.File]::ReadAllText((Join-Path $second $file.Name))
    if ($file.Name -eq 'summary.json') {
        $ja = ConvertFrom-Json $a -AsHashtable -Depth 100; $jb = ConvertFrom-Json $b -AsHashtable -Depth 100
        $ja.Remove('timings'); $ja.Remove('environment'); $jb.Remove('timings'); $jb.Remove('environment')
        $a = ConvertTo-Json $ja -Depth 100 -Compress; $b = ConvertTo-Json $jb -Depth 100 -Compress
    }
    if ($a -cne $b) { throw "Determinism mismatch: $($file.Name)" }
}
$spectralMax = 0.0
foreach ($s in $summaries | Where-Object { $_.case.family -eq 'toeplitz' }) {
    $n = $s.case.size
    for ($i = 0; $i -lt $n; $i++) {
        $expected = 2 - 2 * [Math]::Cos(($i + 1) * [Math]::PI / ($n + 1))
        $spectralMax = [Math]::Max($spectralMax, [Math]::Abs($s.eigenvalues[$i] - $expected))
    }
}
$evidence = [ordered]@{
    convergedRuns = $summaries.Count; jsonFiles = $jsonCount; csvFiles = $csvCount; svgFiles = $svgCount
    aggregateRelativeResidualMax = ($summaries.accuracy.aggregateRelativeResidual | Measure-Object -Maximum).Maximum
    normalizedResidualMax = ($summaries.accuracy.normalizedResidualMax | Measure-Object -Maximum).Maximum
    orthogonalityFrobeniusMax = ($summaries.accuracy.orthogonalityFrobenius | Measure-Object -Maximum).Maximum
    relativeReconstructionMax = ($summaries.accuracy.relativeReconstruction | Measure-Object -Maximum).Maximum
    traceDriftScaledMax = ($summaries.accuracy.traceDriftScaled | Measure-Object -Maximum).Maximum
    frobeniusDriftScaledMax = ($summaries.accuracy.frobeniusDriftScaled | Measure-Object -Maximum).Maximum
    maxDistanceScaled = ($summaries.accuracy.maxDistanceScaled | Measure-Object -Maximum).Maximum
    toeplitzAbsoluteSpectralErrorMax = $spectralMax
    deterministicRepeat = 'All files equal after removing timings/environment from summary.json'
}
$json = ConvertTo-Json $evidence -Depth 10
[IO.File]::WriteAllText((Join-Path $root 'artifacts/validation-evidence.json'), $json.Replace("`r`n", "`n") + "`n")
$json
