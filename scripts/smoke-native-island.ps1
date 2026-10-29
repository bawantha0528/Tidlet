#requires -Version 7.0
[CmdletBinding()]
param([string] $Executable=(Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe'))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot "../artifacts/native-smoke/$([guid]::NewGuid().ToString('N'))"
$null=New-Item -ItemType Directory -Path $root -Force
$report=Join-Path $root 'probe.json'
$exe=(Resolve-Path -LiteralPath $Executable).Path
$target=Start-Process -FilePath $exe -ArgumentList @('--native-probe','--exit-after-seconds','9','--probe-report',('"' + $report + '"')) -PassThru -WindowStyle Hidden
try {
    if (-not $target.WaitForExit(20000)) { throw 'Native probe did not exit itself; no termination attempted.' }
    $target.Refresh()
    $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($target.ExitCode -ne 0 -or $data.Failure) { throw "Native probe failed: $($data.Failure)" }
    $native=$data.LastObservation
    if (-not $native.Visible -or -not $native.Layered -or -not $native.NoActivate -or -not $native.ToolWindow -or -not $native.ClickThrough) { throw 'Native island styles/visibility failed.' }
    if ($data.LoadedWpfAssemblies.Count -ne 0) { throw 'Native entry unexpectedly loaded WPF.' }
    if ($data.AvailableSamples -lt 1 -or -not $data.PollingStoppedOnExit -or $data.InvisibleChecks -ne 0 -or $data.ForegroundEverIsland) { throw 'CPU, visibility, focus or natural-exit diagnostics failed.' }
    [pscustomobject]@{ Result='Pass'; LiveCpu=$data.AvailableSamples; WpfAssemblies=0; NativeStyles=$true; NaturalExit=$true; Report=$report; Frame=[IO.Path]::ChangeExtension($report,'.bmp') } | ConvertTo-Json
}
finally { $target.Dispose() }
