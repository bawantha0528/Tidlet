#requires -Version 5.1
[CmdletBinding()]
param([string] $OutputDirectory='', [string] $ExecutablePath='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot "../artifacts/control-center-smoke/$([guid]::NewGuid().ToString('N'))" }
$null=New-Item -ItemType Directory -Force $OutputDirectory
$root=(Resolve-Path -LiteralPath $OutputDirectory).Path
if ((Get-ChildItem -LiteralPath $root -Force | Measure-Object).Count) { throw 'Use a fresh evidence directory.' }
if (-not $ExecutablePath) { $ExecutablePath=Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe' }
$exe=(Resolve-Path -LiteralPath $ExecutablePath).Path
$checks=foreach ($case in @(@{Name='Notch-Dark';Theme='Dark';Page='Overview'},@{Name='Notch-Light';Theme='Light';Page='Overview'},@{Name='Full-Dark';Theme='Dark';Page='Cpu'})) {
    $report=Join-Path $root ($case.Name+'.json')
    $target=Start-Process -FilePath $exe -ArgumentList @('--style','Notch','--state','Expanded','--theme',$case.Theme,'--page',$case.Page,'--exit-after-seconds','8','--probe-report',('"'+$report+'"')) -WindowStyle Hidden -PassThru
    try {
        if (-not $target.WaitForExit(15000)) { throw 'Host did not exit naturally; no termination attempted.' }
        $target.Refresh(); $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if ($target.ExitCode -ne 0 -or $data.Failure -or $data.LoadedWpfAssemblies.Count -ne 0 -or $data.MediaCommands -ne 0 -or
            $data.TimersRemainingAfterExit -ne 0 -or -not $data.PollingStoppedOnExit -or $data.ForegroundEverIsland) { throw 'Control center lifetime/focus/action checks failed.' }
        $compact=$case.Page -eq 'Overview'
        if ($data.CompactNotch -ne $compact -or ($compact -and $data.MediaCapability.Availability -eq 0 -and ($data.MediaReads -lt 2 -or $data.LastCompletedMediaStatus.State -eq 0))) { throw 'Media/layout did not initialize.' }
        if ($data.LoadedMediaAssemblies.Count -ne 0 -or @($data.Workers | Where-Object { -not $_.ExitedUtc -or $_.ExitCode -ne 0 -or -not $_.PeakWorkingSetBytes -or ($_.Info -and ($_.Info.Commands -ne 0 -or $_.Info.WpfAssemblies.Count -ne 0)) }).Count -ne 0) { throw 'Music isolation/lifetime checks failed.' }
        if ($compact -and $data.MediaCapability.Availability -eq 0 -and $data.WorkerStarts -lt 1) { throw 'No isolated worker was exercised.' }
        if (-not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($report,'.bmp')))) { throw 'Missing rendered frame.' }
        [pscustomobject]@{ Case=$case.Name; Result='Pass'; MediaReads=$data.MediaReads; MediaState=$data.LastCompletedMediaStatus.State; RealPlaybackCommands=$data.MediaCommands; VisualReview='Separate rendered-buffer inspection required' }
    } finally { $target.Dispose() }
}
$checks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'checks.json') -Encoding utf8
$checks
