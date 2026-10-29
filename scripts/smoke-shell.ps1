#requires -Version 5.1
[CmdletBinding()]
param([switch] $IncludeFullscreenFixture)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot "../artifacts/native-shell-smoke/$([guid]::NewGuid().ToString('N'))"
$null=New-Item -ItemType Directory -Path $root -Force
$exe=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path
function Invoke-OwnedHost([string[]]$Arguments,[int]$Timeout=35000) {
    $target=Start-Process -FilePath $exe -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    try {
        if(-not $target.WaitForExit($Timeout)) { throw 'Owned smoke host did not exit itself; no termination attempted.' }
        $target.Refresh()
        return $target.ExitCode
    } finally { $target.Dispose() }
}
$exercise=Join-Path $root 'exercise.json'
if((Invoke-OwnedHost @('--exercise-shell','--exit-after-seconds','24','--probe-report',('"' + $exercise + '"'))) -ne 0) { throw 'Native exercise failed.' }
$data=Get-Content -LiteralPath $exercise -Raw | ConvertFrom-Json
if($data.Failure -or -not $data.ExerciseCompleted -or $data.Frames.Count -ne 9 -or $data.LoadedWpfAssemblies.Count -ne 0 -or
    $data.TimersRemainingAfterExit -ne 0 -or -not $data.PollingStoppedOnExit -or $data.ForegroundEverIsland) { throw 'Native exercise diagnostics failed.' }
foreach($style in @('Island','Notch','Bar')) {
    foreach($state in @('Collapsed','Hover','Expanded')) {
        if(-not ($data.Frames -contains "$style-$state")) { throw "Missing rendered $style/$state." }
    }
}
$hidden=Join-Path $root 'hidden.json'
if((Invoke-OwnedHost @('--hidden','--exit-after-seconds','2','--probe-report',('"' + $hidden + '"'))) -ne 0) { throw 'Hidden smoke failed.' }
$hiddenData=Get-Content -LiteralPath $hidden -Raw | ConvertFrom-Json
if($hiddenData.RenderCount -ne 0 -or $hiddenData.SampleAttempts -ne 0 -or $hiddenData.PointerChecks -ne 0 -or
    $hiddenData.LastObservation.Visible -or -not $hiddenData.Suppressed) { throw 'Hidden host performed work.' }
foreach($arguments in @(
    @('--native-probe'),
    @('--style','invalid'),
    @('--hidden','--hidden'),
    @('--exit-after-seconds','0')
)) {
    if((Invoke-OwnedHost $arguments 15000) -ne 2) { throw 'Invalid arguments were not rejected.' }
}
$fixtureResult='Not requested'
if($IncludeFullscreenFixture) {
    $fixtureReport=Join-Path $root 'fixture.json'
    $fixture=Start-Process -FilePath $exe -ArgumentList @('--fullscreen-fixture','--exit-after-seconds','5','--probe-report',('"' + $fixtureReport + '"')) -PassThru -WindowStyle Hidden
    try {
        Start-Sleep -Milliseconds 900
        $transition=Join-Path $root 'transition.json'
        if((Invoke-OwnedHost @('--exit-after-seconds','10','--probe-report',('"' + $transition + '"')) 20000) -ne 0) { throw 'Fullscreen transition host failed.' }
        if(-not $fixture.WaitForExit(15000)) { throw 'Fixture did not exit itself.' }
        $fixtureData=Get-Content -LiteralPath $fixtureReport -Raw | ConvertFrom-Json
        $transitionData=Get-Content -LiteralPath $transition -Raw | ConvertFrom-Json
        if(-not $fixtureData.FixtureForegroundConfirmed -or $fixtureData.Failure -or
            $transitionData.SuppressionTransitions -lt 2 -or -not $transitionData.LastObservation.Visible -or $transitionData.ForegroundEverIsland) { throw 'Fullscreen hide/resume failed.' }
        $fixtureResult='Pass: owned borderless fixture; exclusive games untested'
    } finally { $fixture.Dispose() }
}
[ordered]@{Result='Pass';Cases=if($IncludeFullscreenFixture){7}else{6};RenderedCombinations=9;HiddenWork='Zero';
    FullscreenFixture=$fixtureResult;ReportDirectory=$root;RealProcessTermination=$false} | ConvertTo-Json

