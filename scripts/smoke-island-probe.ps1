#requires -Version 7.0
[CmdletBinding()]
param([string] $Executable = (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$root = Join-Path $PSScriptRoot "../artifacts/probe-smoke/$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $root -Force
$report = Join-Path $root 'probe.json'
$target = Start-Process -FilePath $exe -ArgumentList @('--island-probe','--exit-after-seconds','9','--probe-report',('"' + $report + '"')) -PassThru -WindowStyle Hidden
try {
    if (-not $target.WaitForExit(25000)) { throw 'Probe did not exit itself; no termination attempted.' }
    $target.Refresh()
    if ($target.ExitCode -ne 0) { throw "Probe exited with $($target.ExitCode). Report: $report" }
    $data = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($data.Failure -or -not $data.NaturalExit -or -not $data.PollingStoppedOnExit) { throw 'Probe failed or did not detach polling.' }
    if ($data.Capability.Availability -ne 0 -or $data.AvailableSamples -lt 1) { throw 'This machine did not produce a valid live CPU sample.' }
    if (-not $data.LastObservation.Visible -or -not $data.LastObservation.Layered -or -not $data.LastObservation.RequiredStylesPresent) { throw 'Native visible layered/click-through/no-activate/toolwindow styles missing.' }
    if ($data.InvisibleChecks -ne 0 -or $data.ForegroundEverIsland -or $data.RenderCount -lt 1) { throw 'Probe visibility/activation/render check failed.' }
}
finally { $target.Dispose() }
foreach ($arguments in @(
    @('--island-probe'),
    @('--island-probe','--exit-after-seconds','0','--probe-report',('"' + $report + '"'))
)) {
    $invalid = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    try {
        if (-not $invalid.WaitForExit(15000)) { throw 'Invalid-argument process did not exit itself.' }
        $invalid.Refresh()
        if ($invalid.ExitCode -ne 2) { throw 'Invalid probe arguments were not rejected.' }
    }
    finally { $invalid.Dispose() }
}
[pscustomobject]@{ Result='Pass'; Cases=3; NativeStylesAndVisibility=$true; LiveCpu=$true; NaturalExit=$true; Report=$report } | ConvertTo-Json
