#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $Executable = (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
if ((Get-ChildItem -LiteralPath $outputRoot -Force | Measure-Object).Count -gt 0) { throw 'Use a fresh evidence directory; do not overwrite a trial.' }
$report = Join-Path $outputRoot 'probe.json'
$target = Start-Process -FilePath $exe -ArgumentList @('--island-probe','--exit-after-seconds','345','--probe-report',('"' + $report + '"')) -PassThru -WindowStyle Hidden
$observerExit = 1
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { $admin = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
    [ordered]@{
        StartedUtc=[DateTimeOffset]::UtcNow.ToString('O')
        IsAdministrator=$admin
        LogicalProcessors=[Environment]::ProcessorCount
        ProbeProcessId=$target.Id
        NaturalExitAfterSeconds=345
        WarmupSeconds=30
        MeasuredSeconds=300
        Scope='Minimal real-CPU transparent WPF pill; not completed M1'
        NativeVisibilityMeaning='WS_VISIBLE observed on each publication; occlusion and cross-process clicks require manual checks.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'context.json') -Encoding utf8
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $target.Id -Scenario Collapsed -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $outputRoot
    $observerExit = $LASTEXITCODE
    if (-not $target.WaitForExit(25000)) { throw 'Probe did not exit itself; no process termination attempted.' }
    $target.Refresh()
    if ($target.ExitCode -ne 0) { throw "Probe failed with code $($target.ExitCode)." }
    $data = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($data.Failure -or -not $data.PollingStoppedOnExit -or $data.AvailableSamples -lt 70 -or
        $data.InvisibleChecks -ne 0 -or -not $data.LastObservation.Visible -or
        -not $data.LastObservation.RequiredStylesPresent -or -not $data.LastObservation.Layered -or
        $data.ForegroundEverIsland) { throw 'Native/provider diagnostics failed; numerical results cannot establish feasibility.' }
    Write-Output "Probe exited naturally; CPU samples: $($data.AvailableSamples); native visibility checks: $($data.VisibilityChecks)."
}
finally { $target.Dispose() }
exit $observerExit
