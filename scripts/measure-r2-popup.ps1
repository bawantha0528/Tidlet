#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Cpu','Ram','Network','Battery','Disk','Gpu','AfterCpu','AfterRam')][string]$Scenario='Cpu',[string]$Executable='',[switch]$Bottom,[switch]$TopReveal)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($Bottom -and $TopReveal){throw 'Choose only one placement.'}
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root;$root=(Resolve-Path $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=if($Executable){(Resolve-Path -LiteralPath $Executable).Path}else{(Resolve-Path (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path}
$after=$Scenario.StartsWith('After');$module=$Scenario.Replace('After','');$metricScenario=if($after){'Bar'}else{'Popup'}
$arguments=@('--r1-probe','--popup-module',$module,'--exit-after-seconds','345','--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'))
if($Bottom){$arguments+='--bottom';'{"Version":1,"Items":["Cpu","Gpu","Ram","Network","Battery"]}'|Set-Content (Join-Path $root 'settings.json')}
if($TopReveal){'{"Version":1,"RevealAtTop":true,"Items":["Cpu","Gpu","Ram","Network","Battery"]}'|Set-Content (Join-Path $root 'settings.json')}
if(-not $after){$arguments+=@('--capture',('"'+$root+'\bar.png"'))}
if($after){$arguments+=@('--close-popup-after','15')}
$hashes=foreach($file in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json')){[ordered]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $file) -Algorithm SHA256).Hash}}
$hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
[ordered]@{Scenario=$Scenario;Gate=$metricScenario;Bottom=[bool]$Bottom;WorkingSetLimitBytes=if($after){90000000}else{90000000};WarmupSeconds=30;DurationSeconds=300;ExpectedArchitecture='X86';OtherTidletProcesses=@(Get-Process IsletMonitor -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id);CaptureKind='WPF 100/150 percent render exports, not desktop screenshots';ClosePopupSeconds=if($after){15}else{$null}}|ConvertTo-Json|Set-Content (Join-Path $root 'context.json')
$p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
 & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario $metricScenario -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
 $result=$LASTEXITCODE
 $tailPeak=0L;$tail=@();$deadline=[DateTime]::UtcNow.AddSeconds(25)
 while(-not $p.HasExited -and [DateTime]::UtcNow -lt $deadline){$p.Refresh();if(-not $p.HasExited){$tailPeak=[Math]::Max($tailPeak,$p.PeakWorkingSet64);$tail+=[pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');WorkingSetBytes=$p.WorkingSet64;LifetimePeakBytes=$p.PeakWorkingSet64}};Start-Sleep -Milliseconds 250}
 $tail|Export-Csv -NoTypeInformation (Join-Path $root 'post-observation.csv')
 if(-not $p.HasExited){throw 'Popup fixture did not exit naturally; no termination attempted.'}
 $p.Refresh();$d=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
 if($p.ExitCode -ne 0 -or -not $d.TimersStopped -or -not $d.AllTrayRemoved -or -not $d.PopupWorkersDrained -or $d.OutstandingPopupWorkers -ne 0 -or $d.PointerSize -ne 4 -or $d.ProcessArchitecture -ne 'X86' -or $d.EverHidden -or -not $d.DiagnosticPopupInputHeld -or $d.PopupOpens -ne 1 -or $d.PopupCloses -ne 1){throw 'Popup lifecycle/pure-state proof failed.'}
 $closed=@($d.PopupEvents|Where-Object{$_.Action -eq 'Close'})
 if($Bottom){$o=$d.Observations[0];if($d.Settings.Edge -ne 1 -or $o.Placement.Pixels.Bottom -gt $o.Work.Bottom -or $d.MeasuredPopupBounds.Bottom -ge $o.Placement.Pixels.Top){throw 'Bottom placement or upward popup proof failed.'}}
 if($TopReveal){$o=$d.Observations[0];if($d.Settings.Edge -ne 0 -or -not $d.Settings.RevealAtTop -or $o.Placement.Pixels.Top -lt $o.Work.Top -or $d.MeasuredPopupBounds.Top -le $o.Placement.Pixels.Bottom){throw 'Top reveal or downward popup proof failed.'}}
 if($after){if($d.BaselinePopupOpen -or $d.BaselinePopupReads -ne $d.ProcessWork -or $d.BaselinePopupFrames -ne $d.ChartWork -or $closed[0].Seconds -gt 25){throw 'Closed-popup heavy work did not remain paused.'}}
 else{if(-not $d.BaselinePopupOpen -or $closed[0].Seconds -lt 330 -or $d.ChartWork -lt 100){throw 'Popup was not held open throughout measurement.'}}
 $summary=Get-Content (Join-Path $root 'summary.json') -Raw|ConvertFrom-Json
 $lifetimePeak=[Math]::Max([long]$summary.LifetimePeakWorkingSetBytes,$tailPeak)
 $currentMax=[long]$summary.WorkingSetMaxBytes;if($tail.Count){$currentMax=[Math]::Max($currentMax,[long]($tail.WorkingSetBytes|Measure-Object -Maximum).Maximum)}
 $policy=Test-SessionMemory -CurrentMaximumBytes $currentMax -LifetimePeakBytes $lifetimePeak -ApplicationSession $false -ApplicationOpen $false
 $policy|ConvertTo-Json|Set-Content (Join-Path $root 'memory-policy.json')
 $lifetimePass=$policy.Accepted
 $hashesUnchanged=$true
 foreach($hash in $hashes){if((Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $hash.File) -Algorithm SHA256).Hash -ne $hash.SHA256){$hashesUnchanged=$false}}
 [ordered]@{Lifecycle='Pass';NumericalExit=$result;Scenario=$Scenario;PopupFrames=$d.ChartWork;PopupReads=$d.ProcessWork;WorkersDrained=$d.PopupWorkersDrained;ZeroHeavyWorkWhileClosed=if($after){$true}else{$null};NoFullscreenInterruption=(-not $d.EverHidden);LifetimePeakWorkingSetBytes=$lifetimePeak;LifetimeWithinPopupLimit=$lifetimePass;BinaryHashesUnchanged=$hashesUnchanged;OverallAccepted=($result -eq 0 -and $lifetimePass -and $hashesUnchanged)}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
 if(-not $lifetimePass -or -not $hashesUnchanged){$result=2}
}finally{$p.Dispose()}
exit $result



