#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$root=(Resolve-Path -LiteralPath $OutputDirectory).Path
if ((Get-ChildItem -LiteralPath $root -Force | Measure-Object).Count) { throw 'Use a fresh evidence directory.' }
$exe=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path
$report=Join-Path $root 'probe.json'
$binaryRoot=Split-Path -Parent $exe
$hashes=foreach ($name in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Core.dll','Islet.Shell.dll','Islet.Windows.dll','IsletMonitor.runtimeconfig.json','IsletMonitor.deps.json')) {
    [pscustomobject]@{ File=$name; SHA256=(Get-FileHash -LiteralPath (Join-Path $binaryRoot $name) -Algorithm SHA256).Hash }
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'binaries.json') -Encoding utf8
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
try { $admin=([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
finally { $identity.Dispose() }
$target=Start-Process -FilePath $exe -ArgumentList @('--native-probe','--exit-after-seconds','345','--probe-report',('"' + $report + '"')) -PassThru -WindowStyle Hidden
try {
    [ordered]@{ StartedUtc=[DateTimeOffset]::UtcNow.ToString('O'); IsAdministrator=$admin; LogicalProcessors=[Environment]::ProcessorCount; ProcessId=$target.Id; WarmupSeconds=30; MeasuredSeconds=300; NaturalExitAfterSeconds=345; Scope='Approved native Win32 CPU feasibility pill; not full M1'; NativeVisibilityMeaning='Sampled WS_VISIBLE; occlusion needs physical observation' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'context.json') -Encoding utf8
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $target.Id -Scenario Collapsed -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
    $measurementExit=$LASTEXITCODE
    if (-not $target.WaitForExit(25000)) { throw 'Probe did not exit itself; no termination attempted.' }
    $target.Refresh()
    $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $native=$data.LastObservation
    if ($target.ExitCode -ne 0 -or $data.Failure -or $data.LoadedWpfAssemblies.Count -ne 0 -or $data.AvailableSamples -lt 70 -or
        -not $native.Visible -or -not $native.Layered -or -not $native.NoActivate -or -not $native.ToolWindow -or -not $native.ClickThrough -or
        $data.InvisibleChecks -ne 0 -or $data.ForegroundEverIsland -or -not $data.PollingStoppedOnExit) { throw 'Native diagnostics failed; numerical measurement is not acceptance.' }
    Write-Output "Native probe exited naturally; live CPU readings: $($data.AvailableSamples); WPF assemblies: $($data.LoadedWpfAssemblies.Count)."
}
finally { $target.Dispose() }
exit $measurementExit
