#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Cpu','Ram','AfterCpu','AfterRam')][string]$Scenario='Cpu')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root;$root=(Resolve-Path $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=(Resolve-Path (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path
$after=$Scenario.StartsWith('After');$module=$Scenario.Replace('After','');$metricScenario=if($after){'Bar'}else{'Popup'}
$arguments=@('--r1-probe','--popup-module',$module,'--exit-after-seconds','345','--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'),'--capture',('"'+$root+'\bar.png"'))
if($after){$arguments+=@('--close-popup-after','15')}
$hashes=foreach($file in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json')){[ordered]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $file) -Algorithm SHA256).Hash}}
$hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
[ordered]@{Scenario=$Scenario;Gate=$metricScenario;WorkingSetLimitBytes=if($after){75000000}else{90000000};WarmupSeconds=30;DurationSeconds=300;ExpectedArchitecture='X86';OtherTidletProcesses=@(Get-Process IsletMonitor -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id);CaptureKind='WPF 100/150 percent render exports, not desktop screenshots';ClosePopupSeconds=if($after){15}else{$null}}|ConvertTo-Json|Set-Content (Join-Path $root 'context.json')
$p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
 & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario $metricScenario -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
 $result=$LASTEXITCODE
 if(-not $p.WaitForExit(25000)){throw 'Popup fixture did not exit naturally; no termination attempted.'}
 $p.Refresh();$d=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
 if($p.ExitCode -ne 0 -or -not $d.TimersStopped -or -not $d.AllTrayRemoved -or -not $d.PopupWorkersDrained -or $d.OutstandingPopupWorkers -ne 0 -or $d.PointerSize -ne 4 -or $d.ProcessArchitecture -ne 'X86' -or $d.EverHidden -or -not $d.DiagnosticPopupInputHeld -or $d.PopupOpens -ne 1 -or $d.PopupCloses -ne 1){throw 'Popup lifecycle/pure-state proof failed.'}
 $closed=@($d.PopupEvents|Where-Object{$_.Action -eq 'Close'})
 if($after){if($d.BaselinePopupOpen -or $d.BaselinePopupReads -ne $d.ProcessWork -or $d.BaselinePopupFrames -ne $d.ChartWork -or $closed[0].Seconds -gt 25){throw 'Closed-popup heavy work did not remain paused.'}}
 else{if(-not $d.BaselinePopupOpen -or $closed[0].Seconds -lt 330 -or $d.ChartWork -lt 100){throw 'Popup was not held open throughout measurement.'}}
 [ordered]@{Lifecycle='Pass';NumericalExit=$result;Scenario=$Scenario;PopupFrames=$d.ChartWork;PopupReads=$d.ProcessWork;WorkersDrained=$d.PopupWorkersDrained;ZeroHeavyWorkWhileClosed=if($after){$true}else{$null};NoFullscreenInterruption=(-not $d.EverHidden)}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
}finally{$p.Dispose()}
exit $result


