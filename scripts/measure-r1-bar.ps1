#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Reserve,[switch]$Bottom,[string]$Executable='',[switch]$Gpu,[switch]$Light,[switch]$DesktopWidgets,[switch]$DataUsageWidget)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
if(@(Get-Process IsletMonitor -ErrorAction SilentlyContinue).Count){throw 'Exit other Tidlet instances before the resource trial.'}
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root
$root=(Resolve-Path $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=if($Executable){(Resolve-Path -LiteralPath $Executable).Path}else{(Resolve-Path (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/win-x86-probe/IsletMonitor.exe')).Path}
if($DesktopWidgets -or $DataUsageWidget){
    $names=@()
    if($DesktopWidgets){$names+=@('CPU','GPU','RAM','Disk','Network','Combined')}
    if($DataUsageWidget){$names+='Data usage'}
    $index=0
    $layout=@(foreach($name in $names){
        @{Name=$name;X=20+$index%3*240;Y=48+[int][Math]::Floor($index/3)*210}
        $index++
    })
    ConvertTo-Json -InputObject $layout | Set-Content (Join-Path $root 'desktop-widgets.json')
}
if($Gpu -or $Light){
    $settings=@{Version=1;Theme=if($Light){'Light'}else{'Dark'}}
    if($Gpu){$settings.Items=@('Cpu','Gpu','Ram','Network','Battery')}
    $settings|ConvertTo-Json|Set-Content (Join-Path $root 'settings.json')
}
$args=@('--r1-probe','--exit-after-seconds','345','--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'),'--capture',('"'+$root+'\bar.png"'))
if($Reserve){$args+='--reserve'}
if($Bottom){$args+='--bottom'}
$hashes=foreach($file in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json')){[pscustomobject]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $file) -Algorithm SHA256).Hash}}
$hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
[ordered]@{GpuEnabled=[bool]$Gpu;Explanation='GPU sampler is included in the same measured process; no helper or second application'}|ConvertTo-Json|Set-Content (Join-Path $root 'gpu-context.json')
[ordered]@{Scenario='Bar';Milestone='R1';WorkingSetLimitBytes=90000000;CpuLimitPercent=1;WarmupSeconds=30;DurationSeconds=300;Reserve=[bool]$Reserve;Bottom=[bool]$Bottom;ExpectedArchitecture='X86';OtherTidletProcesses=@(Get-Process IsletMonitor -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id);CaptureKind='WPF render target, not desktop screenshot'}|ConvertTo-Json|Set-Content (Join-Path $root 'context.json')
$p=Start-Process -FilePath $exe -ArgumentList $args -WindowStyle Hidden -PassThru
try
{
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario Bar -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
    $result=$LASTEXITCODE
    $tail=@();$tailPeak=0L;$deadline=[DateTime]::UtcNow.AddSeconds(25)
    while(-not $p.HasExited -and [DateTime]::UtcNow -lt $deadline){$p.Refresh();if(-not $p.HasExited){$tailPeak=[Math]::Max($tailPeak,$p.PeakWorkingSet64);$tail+=[pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');WorkingSetBytes=$p.WorkingSet64;LifetimePeakBytes=$p.PeakWorkingSet64}};Start-Sleep -Milliseconds 250}
    $tail|Export-Csv -NoTypeInformation (Join-Path $root 'post-observation.csv')
    if(-not $p.HasExited){throw 'Bar did not exit naturally; no process termination attempted.'}
    $p.Refresh();$d=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
    if($p.ExitCode -ne 0 -or -not $d.FirstFrame -or -not $d.TimersStopped -or -not $d.AllTrayRemoved -or $d.PointerSize -ne 4 -or $d.ProcessArchitecture -ne 'X86' -or $d.Samples -lt 100 -or $d.EverHidden -or $d.ChartWork -ne 0 -or $d.ProcessWork -ne 0 -or ($Light -and $d.Settings.Theme -ne 1)){throw 'R1 lifecycle/architecture/source proof failed.'}
    $summary=Get-Content (Join-Path $root 'summary.json') -Raw|ConvertFrom-Json
    $wholePeak=[Math]::Max([long]$summary.LifetimePeakWorkingSetBytes,$tailPeak);$currentMax=[long]$summary.WorkingSetMaxBytes
    if($tail.Count){$currentMax=[Math]::Max($currentMax,[long]($tail.WorkingSetBytes|Measure-Object -Maximum).Maximum)}
    $policy=Test-SessionMemory -CurrentMaximumBytes $currentMax -LifetimePeakBytes $wholePeak -ApplicationSession $false -ApplicationOpen $false
    $policy|ConvertTo-Json|Set-Content (Join-Path $root 'memory-policy.json')
    $unchanged=$true;foreach($hash in $hashes){if((Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $hash.File) -Algorithm SHA256).Hash -ne $hash.SHA256){$unchanged=$false}}
    $gpuProof=if($Gpu){$d.GpuBarWorkersDrained -and $d.GpuBarRequests -ge 100 -and $null -ne $d.LastSnapshot.Gpu -and $d.LastSnapshot.Gpu -ge 0 -and $d.LastSnapshot.Gpu -le 100}else{$true}
    $expectedPins=6*[int][bool]$DesktopWidgets+[int][bool]$DataUsageWidget
    $desktopProof=$expectedPins -eq 0 -or ($d.RestoredDesktopWidgets -eq $expectedPins -and $d.DesktopWidgetWorkersDrained -and (!$DesktopWidgets -or $d.DesktopWidgetFrames -gt 100))
    $accepted=$result -eq 0 -and $policy.Accepted -and $unchanged -and $gpuProof -and $desktopProof
    [ordered]@{Lifecycle='Pass';NumericalExit=$result;Samples=$d.Samples;Windows=$d.WindowCount;TrayRemoved=$d.AllTrayRemoved;ClosedPopupChartWork=$d.ChartWork;ClosedPopupProcessWork=$d.ProcessWork;GpuProof=$gpuProof;DesktopWidgetProof=$desktopProof;LifetimePeakWorkingSetBytes=$wholePeak;BinaryHashesUnchanged=$unchanged;OverallAccepted=$accepted}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
    if(-not $accepted){$result=2}
}finally{$p.Dispose()}
exit $result

