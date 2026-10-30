#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string] $ValidEvidence)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot "../artifacts/harness-tests/native-proof-$([guid]::NewGuid().ToString('N'))"
$null=New-Item -ItemType Directory -Path $root -Force
foreach($name in @('context.json','summary.json','probe.json','samples.csv')) { Copy-Item -LiteralPath (Join-Path $ValidEvidence $name) -Destination (Join-Path $root $name) }
foreach($name in @('workers.csv','probe.workers.jsonl')) {
    if(Test-Path -LiteralPath (Join-Path $ValidEvidence $name)){Copy-Item -LiteralPath (Join-Path $ValidEvidence $name) -Destination (Join-Path $root $name)}
}
$probePath=Join-Path $root 'probe.json'
$summaryPath=Join-Path $root 'summary.json'
$csvPath=Join-Path $root 'samples.csv'
$probeOriginal=Get-Content -LiteralPath $probePath -Raw
$summaryOriginal=Get-Content -LiteralPath $summaryPath -Raw
$csvOriginal=Get-Content -LiteralPath $csvPath -Raw
function Check([bool]$ExpectedPass) {
    $oldEap = $ErrorActionPreference; $ErrorActionPreference = "SilentlyContinue"
    try { & powershell -NoProfile -File (Join-Path $PSScriptRoot "verify-native-scenario.ps1") -ArtifactDirectory $root 2>$null | Out-Null } finally { $ErrorActionPreference = $oldEap }
    if(($LASTEXITCODE -eq 0) -ne $ExpectedPass) { throw 'Evidence guard accepted invalid data or rejected the baseline.' }
}
Check $true
$data=$probeOriginal | ConvertFrom-Json
$data.TimersRemainingAfterExit=1
$data | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $probePath -Encoding utf8
Check $false
$data=$probeOriginal | ConvertFrom-Json
$data.LoadedWpfAssemblies=@('PresentationFramework')
$data | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $probePath -Encoding utf8
Check $false
$data=$probeOriginal | ConvertFrom-Json
$data.StatesObserved | Add-Member -NotePropertyName 'Notch/Hover' -NotePropertyValue 1 -Force
$data | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $probePath -Encoding utf8
Check $false
Set-Content -LiteralPath $probePath -Value $probeOriginal -Encoding utf8
$summary=$summaryOriginal | ConvertFrom-Json
$summary.WorkingSetMaxBytes=0
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Check $false
Set-Content -LiteralPath $summaryPath -Value $summaryOriginal -Encoding utf8
$lines=@(Get-Content -LiteralPath $csvPath)
$lines[0..($lines.Count-2)] | Set-Content -LiteralPath $csvPath -Encoding utf8
Check $false
Set-Content -LiteralPath $csvPath -Value $csvOriginal -Encoding utf8
$extra=0
if(($probeOriginal|ConvertFrom-Json).SchemaVersion -ge 3){
    $journal=Join-Path $root 'probe.workers.jsonl'
    if(Test-Path $journal){
        $original=[IO.File]::ReadAllText($journal)
        [IO.File]::WriteAllText($journal,'')
        Check $false
        [IO.File]::WriteAllText($journal,$original)
        $extra++
    }
    $components=Join-Path $root 'workers.csv'
    if((Test-Path $components) -and (Get-Item $components).Length){
        $original=[IO.File]::ReadAllText($components)
        [IO.File]::WriteAllText($components,'')
        Check $false
        [IO.File]::WriteAllText($components,$original)
        $extra++
    }
}[pscustomobject]@{Result='Pass';Checks=(6+);WorkerOmissionChecks=;Cases=@('Valid CSV/native proof','Leaked timer','WPF loaded','Altered summary','Incomplete duration','Unexpected style/state history')} | ConvertTo-Json


