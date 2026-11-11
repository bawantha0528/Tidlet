#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('Dashboard','Settings','CPU','RAM','GPU','Disk','Network','Battery')][string]$Page='Dashboard',[Parameter(Mandatory)][string]$Executable,[ValidateSet('Static','Navigation','AfterClose','Paused')][string]$Scenario='Static',[ValidateSet(0,100,150)][int]$CapturePercent=0,[switch]$MemoryProfile)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
$currentLimit=if($Scenario -eq 'AfterClose'){90000000L}else{100000000L}
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root
$root=(Resolve-Path -LiteralPath $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=(Resolve-Path -LiteralPath $Executable).Path
$others=@(Get-Process IsletMonitor -ErrorAction SilentlyContinue)
if($others.Count){throw 'Exit other Tidlet instances before this resource trial.'}
$files=@('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json')
$hashes=foreach($file in $files){[ordered]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $file) -Algorithm SHA256).Hash}}
$hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
[ordered]@{Page=$Page;WorkingSetLimitBytes=$currentLimit;LifetimeLimitBytes=100000000;ApplicationSession=$true;CpuLimitPercent=1;DurationSeconds=300;WarmupSeconds=30;Accounting='Entire bar and Dashboard process; no auxiliary host';Interaction=if($Scenario -eq 'Navigation'){'Bounded automatic four-page navigation'}else{'Fixed requested page'};CaptureKind='WPF render exports, not physical screenshots';CapturePercent=if($CapturePercent){$CapturePercent}else{'Legacy dual-scale'}}|ConvertTo-Json|Set-Content (Join-Path $root 'context.json')
$arguments=@('--r1-probe','--dashboard-page',$Page,'--exit-after-seconds','345','--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'),'--capture',('"'+$root+'\bar.png"'))
if($CapturePercent){$arguments+=@('--dashboard-capture-scale',"$CapturePercent")}
if($MemoryProfile){$arguments+='--memory-profile'}
if($Scenario -eq 'Navigation'){$arguments+=@('--dashboard-cycle-seconds','4')}
if($Scenario -eq 'AfterClose'){$arguments+=@('--close-dashboard-after','15')}
$gate=if($Scenario -eq 'AfterClose'){'Bar'}else{'Application'}
[ordered]@{Scenario=$Scenario;Gate=$gate;Page=$Page;CapBytes=$currentLimit;LifetimeCapBytes=100000000;CloseSeconds=if($Scenario -eq 'AfterClose'){15}else{$null};CycleSeconds=if($Scenario -eq 'Navigation'){4}else{$null}}|ConvertTo-Json|Set-Content (Join-Path $root 'application-context.json')
if($Scenario -eq 'Paused'){'{"Version":1,"Pause":true}'|Set-Content (Join-Path $root 'settings.json')}
$p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
 & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario $gate -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
 $result=$LASTEXITCODE
 $tailPeak=0L;$tail=@();$deadline=[DateTime]::UtcNow.AddSeconds(25)
 while(-not $p.HasExited -and [DateTime]::UtcNow -lt $deadline){$p.Refresh();if(-not $p.HasExited){$tailPeak=[Math]::Max($tailPeak,$p.PeakWorkingSet64);$tail+=[pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');WorkingSetBytes=$p.WorkingSet64;LifetimePeakBytes=$p.PeakWorkingSet64}};Start-Sleep -Milliseconds 250}
 $tail|Export-Csv -NoTypeInformation (Join-Path $root 'post-observation.csv')
 if(-not $p.HasExited){throw 'Dashboard did not exit naturally; no termination attempted.'}
 $p.Refresh();$d=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
 $summary=Get-Content (Join-Path $root 'summary.json') -Raw|ConvertFrom-Json
 $unchanged=$true;foreach($hash in $hashes){if((Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $hash.File) -Algorithm SHA256).Hash -ne $hash.SHA256){$unchanged=$false}}
 $history=@($d.DashboardPageHistory)
 $pageProof=if($Scenario -eq 'Navigation'){$history.Count -gt 60 -and @($history|Select-Object -Unique).Count -eq 4}else{$history.Count -eq 1 -and $history[0] -eq $Page}
 $lifecycle=$p.ExitCode -eq 0 -and $d.DashboardOpens -eq 1 -and $d.DashboardWorkersDrained -and $d.TimersStopped -and $d.AllTrayRemoved -and $d.PointerSize -eq 4 -and -not $d.EverHidden -and $d.DiagnosticDashboardInputHeld -and $pageProof
 $activity=if($Page -in @('Dashboard','Settings')){$d.DashboardReads -eq 0 -and $d.DashboardFrames -eq 0}else{$d.DashboardReads -gt 100 -and $d.DashboardFrames -gt 100}
 if($Scenario -eq 'Paused'){$activity=$d.Settings.Pause -and $d.BaselineDashboardOpen -and $d.DashboardReads -eq 0 -and $d.DashboardFrames -eq 0 -and $d.MetricReads -eq 0 -and $d.Samples -eq 0}
 if($Scenario -in @('Navigation','AfterClose')){
  $closes=@($d.DashboardEvents|Where-Object{$_.Action -eq 'Close'})
  if($Scenario -eq 'AfterClose'){$activity=$closes.Count -eq 1 -and $closes[0].Seconds -lt 25 -and -not $d.BaselineDashboardOpen -and $d.DashboardReads -eq $d.BaselineDashboardReads -and $d.DashboardFrames -eq $d.BaselineDashboardFrames}
  else{$activity=$closes.Count -eq 1 -and $closes[0].Seconds -ge 330 -and $d.DashboardReads -gt 100 -and $d.DashboardFrames -gt 50}
 }
 $wholePeak=[Math]::Max([long]$summary.LifetimePeakWorkingSetBytes,$tailPeak)
 $currentMax=[long]$summary.WorkingSetMaxBytes;if($tail.Count){$currentMax=[Math]::Max($currentMax,[long]($tail.WorkingSetBytes|Measure-Object -Maximum).Maximum)}
 $policy=Test-SessionMemory -CurrentMaximumBytes $currentMax -LifetimePeakBytes $wholePeak -ApplicationSession $true -ApplicationOpen ($Scenario -ne 'AfterClose')
 $policy|ConvertTo-Json|Set-Content (Join-Path $root 'memory-policy.json')
 $lifetime=$policy.LifetimeWithinLimit
 $captureProof=$true
 if($CapturePercent -and $Scenario -ne 'AfterClose'){
  $exports=@($d.DashboardCaptures);$images=@(Get-ChildItem -LiteralPath $root -Filter 'bar.dashboard.*.png')
  $captureProof=$exports.Count -eq 1 -and $images.Count -eq 1 -and $d.RequestedDashboardCapturePercent -eq $CapturePercent
  if($captureProof){$export=$exports[0];$bytes=[IO.File]::ReadAllBytes($images[0].FullName);$width=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16));$height=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20));$captureProof=$export.Percent -eq $CapturePercent -and $width -eq $export.Width -and $height -eq $export.Height -and $export.Width -eq (860*$CapturePercent/100) -and $export.Height -eq (580*$CapturePercent/100) -and $export.StartedSeconds -ge 330 -and $export.CompletedSeconds -ge $export.StartedSeconds}
 }
 $accepted=$result -eq 0 -and $lifecycle -and $activity -and $unchanged -and $policy.Accepted -and $captureProof
 [ordered]@{Page=$Page;NumericalExit=$result;LifecyclePass=$lifecycle;ExpectedActivity=$activity;DashboardReads=$d.DashboardReads;DashboardFrames=$d.DashboardFrames;LifetimePeakWorkingSetBytes=$wholePeak;LifetimeWithinLimit=$lifetime;BinaryHashesUnchanged=$unchanged;CaptureVerified=$captureProof;CapturePercent=$CapturePercent;AttributionOnly=[bool]$MemoryProfile;OverallAccepted=($accepted -and -not $MemoryProfile);PhysicalInteractionVerified=$false}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
 if(-not $accepted){$result=2}
}finally{$p.Dispose()}
exit $result
