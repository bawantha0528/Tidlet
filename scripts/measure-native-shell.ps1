#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Collapsed','Expanded','Hidden','Fullscreen','PostExpansion')][string] $Scenario,
    [ValidateSet('Island','Notch','Bar')][string] $Style='Island',
    [string] $ExecutablePath='',
    [Parameter(Mandatory)][string] $OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$root=(Resolve-Path -LiteralPath $OutputDirectory).Path
if ((Get-ChildItem -LiteralPath $root -Force | Measure-Object).Count) { throw 'Use a fresh evidence directory.' }
if (-not $ExecutablePath) { $ExecutablePath=Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe' }
$exe=(Resolve-Path -LiteralPath $ExecutablePath).Path
$report=Join-Path $root 'probe.json'
$arguments=@('--style',$Style,'--exit-after-seconds','345','--probe-report',('"' + $report + '"'))
if ($Scenario -eq 'Expanded') { $arguments+=@('--state','Expanded') }
if ($Scenario -eq 'Hidden') { $arguments+='--hidden' }
if ($Scenario -eq 'PostExpansion') { $arguments+='--exercise-shell' }
$observerScenario=if ($Scenario -eq 'PostExpansion') { 'Collapsed' } else { $Scenario }
$fixture=$null
$target=$null
$measurementExit=1
try {
    $hashes=foreach ($name in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Core.dll','Islet.Shell.dll','Islet.Windows.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json','Microsoft.Windows.SDK.NET.dll','WinRT.Runtime.dll')) {
        [pscustomobject]@{ File=$name; SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $exe) $name) -Algorithm SHA256).Hash }
    }
    $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'binaries.json') -Encoding utf8
    if ($Scenario -eq 'Fullscreen') {
        $fixtureReport=Join-Path $root 'fullscreen-fixture.json'
        $fixture=Start-Process -FilePath $exe -ArgumentList @('--fullscreen-fixture','--exit-after-seconds','360','--probe-report',('"' + $fixtureReport + '"')) -PassThru -WindowStyle Hidden
        if ($fixture.WaitForExit(1000)) { throw 'The fullscreen fixture exited early; fullscreen acceptance cannot run.' }
    }
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    try { $admin=([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
    $target=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    [ordered]@{ StartedUtc=[DateTimeOffset]::UtcNow.ToString('O'); Scenario=$Scenario; Style=$Style; IsAdministrator=$admin;
        LogicalProcessors=[Environment]::ProcessorCount; ProcessId=$target.Id; WarmupSeconds=30; MeasuredSeconds=300;
        NaturalExitAfterSeconds=345; Scope='Native Tidlet dashboard with real metric providers; no WPF initialization';
        Fixture=if($Scenario -eq 'Fullscreen'){'Owned borderless foreground fixture; not an exclusive D3D game'}else{$null};
        NativeVisibilityMeaning='WS_VISIBLE observed; occlusion and exhaustive input/focus audit remain separate' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'context.json') -Encoding utf8
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $target.Id -Scenario $observerScenario -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root -ChildJournal ([IO.Path]::ChangeExtension($report,'.workers.jsonl'))
    $measurementExit=$LASTEXITCODE
    if (-not $target.WaitForExit(25000)) { throw 'Target did not exit itself; no termination attempted.' }
    $target.Refresh()
    $data=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($target.ExitCode -ne 0 -or $data.Failure -or $data.LoadedWpfAssemblies.Count -ne 0 -or
        -not $data.PollingStoppedOnExit -or $data.TimersRemainingAfterExit -ne 0 -or $data.ForegroundEverIsland) { throw 'Native lifetime/module/focus diagnostics failed.' }
    if ($Scenario -in @('Collapsed','Expanded')) {
        $expectedState = $Style + '/' + $(if ($Scenario -eq 'Expanded') { 'Expanded' } else { 'Collapsed' })
        $unexpected = @($data.StatesObserved.PSObject.Properties | Where-Object { $_.Name -ne $expectedState })
        if ($unexpected.Count -ne 0) { throw 'State history contains interaction or suppression; this is not a pure scenario measurement.' }
    }
    $native=$data.LastObservation
    if ($data.LoadedMediaAssemblies.Count -ne 0 -or @($data.Workers | Where-Object { -not $_.ExitedUtc -or $_.ExitCode -ne 0 -or -not $_.PeakWorkingSetBytes -or $_.FinalWpfAssemblies.Count -ne 0 }).Count) { throw 'Media isolation/exit costs failed.' }
    $aggregate=@(Import-Csv -LiteralPath (Join-Path $root 'samples.csv'))
    if ($Scenario -eq 'Expanded' -and $Style -eq 'Notch' -and $data.MediaCapability.Availability -eq 0 -and @($aggregate | Where-Object { [int]$_.ActiveWorkers -ne 1 }).Count) { throw 'Expanded Notch did not include one media worker throughout.' }
    if ($Scenario -in @('Collapsed','PostExpansion','Hidden','Fullscreen') -and @($aggregate | Where-Object { [int]$_.ActiveWorkers -ne 0 }).Count) { throw 'Workers remained during collapsed/hidden measurement.' }
    if (-not $native.NoActivate -or -not $native.ToolWindow -or -not $native.Layered) { throw 'Required native window styles were lost.' }
    if ($Scenario -in @('Hidden','Fullscreen')) {
        if ($native.Visible -or -not $data.Suppressed -or $data.SampleAttempts -ne 0 -or $data.PointerChecks -ne 0 -or $data.RenderCount -ne 0) {
            throw 'Suppressed scenario performed metric, pointer or drawing work.'
        }
    } else {
        if (-not $native.Visible -or $data.Suppressed -or $data.InvisibleChecks -gt $(if($Scenario -eq 'PostExpansion'){1}else{0}) -or
            $data.AvailableSamples -lt $(if($Scenario -eq 'Expanded'){270}else{70}) -or $data.PointerChecks -lt 2000) {
            throw 'Visible shell scenario/cadence diagnostics failed.'
        }
        if ($Scenario -eq 'Expanded' -and ($data.State -ne 2 -or $data.HistoryCount -ne 60)) { throw 'Expanded graph scenario did not stay expanded.' }
        if ($Scenario -in @('Collapsed','PostExpansion') -and ($data.State -ne 0 -or -not $native.ClickThrough)) { throw 'Collapsed scenario changed state or lost click-through.' }
        $expectedStyle=@{Island=0;Notch=1;Bar=2}[$Style]
        if ($data.Style -ne $expectedStyle) { throw 'Style changed during measurement.' }
        if ($Scenario -eq 'PostExpansion' -and (-not $data.ExerciseCompleted -or $data.Frames.Count -ne 9)) { throw 'Repeated expansion exercise did not complete.' }
    }
    if ($fixture) {
        if (-not $fixture.WaitForExit(25000)) { throw 'Fullscreen fixture did not exit itself; no termination attempted.' }
        $fixture.Refresh()
        $fixtureData=Get-Content -LiteralPath $fixtureReport -Raw | ConvertFrom-Json
        if ($fixture.ExitCode -ne 0 -or -not $fixtureData.FixtureForegroundConfirmed -or $fixtureData.Failure) { throw 'Fullscreen fixture did not establish a valid foreground test.' }
    }
    [ordered]@{ Result=if($measurementExit -eq 0){'Pass'}else{'Fail'}; Scenario=$Scenario; LifetimeDiagnostics='Pass'; MeasurementExit=$measurementExit;
        SensorSamples=$data.AvailableSamples; PointerChecks=$data.PointerChecks; RenderCount=$data.RenderCount;
        WpfAssemblies=$data.LoadedWpfAssemblies.Count; NativeTimersAfterExit=$data.TimersRemainingAfterExit;
        Recovery=$data.Recovery } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'scenario-check.json') -Encoding utf8
    Write-Output "$Scenario/$Style native scenario diagnostics passed; measurement result is in summary.json."
}
finally {
    if ($target) { $target.Dispose() }
    if ($fixture) { $fixture.Dispose() }
}
exit $measurementExit

