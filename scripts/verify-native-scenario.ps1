#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string] $ArtifactDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
$root=(Resolve-Path -LiteralPath $ArtifactDirectory).Path
$context=Get-Content -LiteralPath (Join-Path $root 'context.json') -Raw | ConvertFrom-Json
$summary=Get-Content -LiteralPath (Join-Path $root 'summary.json') -Raw | ConvertFrom-Json
$data=Get-Content -LiteralPath (Join-Path $root 'probe.json') -Raw | ConvertFrom-Json
$samples=@(Import-Csv -LiteralPath (Join-Path $root 'samples.csv') | ForEach-Object {
    [pscustomobject]@{ElapsedSeconds=[double]$_.ElapsedSeconds;TotalCpuSeconds=[double]$_.TotalCpuSeconds;
        WorkingSetBytes=[long]$_.WorkingSetBytes;PrivateBytes=[long]$_.PrivateBytes;
        LifetimePeakWorkingSetBytes=[long]$_.LifetimePeakWorkingSetBytes;HandleCount=[int]$_.HandleCount;ThreadCount=[int]$_.ThreadCount}
})
$scenario=$context.Scenario
$observerScenario=if($scenario -eq 'PostExpansion'){'Collapsed'}else{$scenario}
$computed=Get-MeasurementSummary -Samples $samples -RequiredDurationSeconds 300 -LogicalProcessorCount $context.LogicalProcessors -Scenario $observerScenario
if($computed.WorkingSetMaxBytes -ne $summary.WorkingSetMaxBytes -or
    [math]::Abs($computed.CpuMachineNormalizedPercent-$summary.CpuMachineNormalizedPercent) -gt 0.000000001 -or
    $computed.Result -ne $summary.Result -or $summary.DurationSeconds -lt 300) { throw 'Raw CSV does not reproduce the complete summary.' }
if($data.ExitCode -ne 0 -or $data.Failure -or -not $data.NaturalExit -or -not $data.PollingStoppedOnExit -or
    $data.LoadedWpfAssemblies.Count -ne 0 -or $data.TimersRemainingAfterExit -ne 0 -or $data.ForegroundEverIsland) { throw 'Native module/lifetime/focus diagnostics failed.' }
$native=$data.LastObservation
if(-not $native.Layered -or -not $native.NoActivate -or -not $native.ToolWindow) { throw 'Native styles missing.' }
if($scenario -in @('Hidden','Fullscreen')) {
    if($native.Visible -or -not $data.Suppressed -or $data.SampleAttempts -ne 0 -or $data.PointerChecks -ne 0 -or $data.RenderCount -ne 0) { throw 'Suppressed scenario performed metric/pointer/drawing work.' }
} else {
    if(-not $native.Visible -or $data.Suppressed -or $data.PointerChecks -lt 2000 -or
        $data.AvailableSamples -lt $(if($scenario -eq 'Expanded'){270}else{70})) { throw 'Visible/cadence diagnostics failed.' }
    if($scenario -eq 'Expanded' -and ($data.State -ne 2 -or $data.HistoryCount -ne 60)) { throw 'Expanded graph scenario failed.' }
    if($scenario -in @('Collapsed','PostExpansion') -and ($data.State -ne 0 -or -not $native.ClickThrough)) { throw 'Collapsed scenario failed.' }
    if($data.Style -ne @{Island=0;Notch=1;Bar=2}[$context.Style]) { throw 'Style changed during observation.' }
    if($scenario -eq 'PostExpansion' -and (-not $data.ExerciseCompleted -or $data.Frames.Count -ne 9)) { throw 'Post-expansion exercise incomplete.' }
}
if($scenario -eq 'Fullscreen') {
    $fixture=Get-Content -LiteralPath (Join-Path $root 'fullscreen-fixture.json') -Raw | ConvertFrom-Json
    if(-not $fixture.FixtureForegroundConfirmed -or $fixture.ExitCode -ne 0 -or $fixture.Failure) { throw 'Fullscreen fixture did not establish a valid test.' }
}
$result=[ordered]@{Result=$computed.Result;Scenario=$scenario;Style=$context.Style;RawCsvRecomputed=$true;
    LifetimeDiagnostics='Pass';SensorSamples=$data.AvailableSamples;PointerChecks=$data.PointerChecks;RenderCount=$data.RenderCount;
    WpfAssemblies=$data.LoadedWpfAssemblies.Count;NativeTimersAfterExit=$data.TimersRemainingAfterExit;
    HiddenBehaviorEvidence=if($scenario -in @('Hidden','Fullscreen')){'Native visibility false; zero sample/pointer/draw calls'}else{'Visible scenario'};
    WorkingSetMaxMB=$computed.WorkingSetMaxBytes/1000000;CpuMachineNormalizedPercent=$computed.CpuMachineNormalizedPercent;
    CpuCoreEquivalentPercent=$computed.CpuCoreEquivalentPercent;Recovery=$data.Recovery}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'scenario-check.json') -Encoding utf8
$result | ConvertTo-Json -Depth 6
if($computed.Result -ne 'Pass'){exit 2}
exit 0

