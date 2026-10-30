#requires -Version 5.1
[CmdletBinding()]
param([string] $OutputDirectory = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot "../artifacts/dashboard-smoke/$([guid]::NewGuid().ToString('N'))" }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$root = (Resolve-Path -LiteralPath $OutputDirectory).Path
if ((Get-ChildItem -LiteralPath $root -Force | Measure-Object).Count) { throw 'Use a fresh evidence directory.' }
$exe = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path
$report = Join-Path $root 'probe.json'
$target = Start-Process -FilePath $exe -ArgumentList @('--exercise-dashboard','--exit-after-seconds','24','--probe-report',('"' + $report + '"')) -WindowStyle Hidden -PassThru
try {
    if (-not $target.WaitForExit(35000)) { throw 'Dashboard host did not exit itself; no termination attempted.' }
    $target.Refresh()
    $data = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($target.ExitCode -ne 0 -or $data.Failure -or -not $data.DashboardExerciseCompleted -or $data.Frames.Count -ne 16 -or
        $data.LoadedWpfAssemblies.Count -ne 0 -or -not $data.PollingStoppedOnExit -or $data.TimersRemainingAfterExit -ne 0 -or
        $data.ForegroundEverIsland -or $data.DashboardSamples -lt 18 -or $data.Requests -lt 18) { throw 'Dashboard lifetime, module or demand diagnostics failed.' }
    foreach ($theme in @('Dark','Light')) {
        foreach ($page in @('Overview','Cpu','Memory','Network','Disk','Gpu','Battery','Apps')) {
            $frame = "Dashboard-$theme-$page"
            if ($data.Frames -notcontains $frame -or -not (Test-Path -LiteralPath (Join-Path $root "frames/$frame.bmp"))) { throw "Missing rendered frame: $frame" }
        }
    }
    [ordered]@{ Result='Pass'; RenderedPages=16; DashboardSamples=$data.DashboardSamples; DiskRequests=$data.Requests;
        LoadedWpfAssemblies=$data.LoadedWpfAssemblies.Count; NativeTimersAfterExit=$data.TimersRemainingAfterExit;
        ReportDirectory=$root; VisualReview='Separate inspection required'; PersonalSettingsTouched=$false } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $root 'checks.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $root 'checks.json') -Raw
} finally { $target.Dispose() }