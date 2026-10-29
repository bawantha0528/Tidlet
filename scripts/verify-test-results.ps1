#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $ResultsDirectory = (Join-Path $PSScriptRoot '../TestResults'),
    [string[]] $ExpectedAssemblies = @('Islet.Core.Tests.dll','Islet.Shell.Tests.dll','Islet.Windows.Tests.dll')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -File -Recurse)
if (-not $files.Count) { throw 'No TRX results found; an empty test run is not a pass.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$total = 0
foreach ($file in $files) {
    [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) { throw "Missing test counters in $($file.Name)." }
    $count = [int]$counters.GetAttribute('total')
    $executed = [int]$counters.GetAttribute('executed')
    $passed = [int]$counters.GetAttribute('passed')
    if ($count -le 0 -or $executed -ne $count -or $passed -ne $executed -or [int]$counters.GetAttribute('failed') -gt 0) {
        throw "Empty, skipped, incomplete or failed suite: $($file.Name)."
    }
    foreach ($unit in $trx.SelectNodes("//*[local-name()='UnitTest']")) {
        $null = $seen.Add([IO.Path]::GetFileName($unit.GetAttribute('storage')))
    }
    $total += $count
}
foreach ($expected in $ExpectedAssemblies) {
    if (-not $seen.Contains($expected)) { throw "Expected suite missing: $expected." }
}
[pscustomobject]@{ Result = 'Pass'; Executed = $total; Passed = $total; Suites = @($seen | Sort-Object) } | ConvertTo-Json

exit 0
