#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=(Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory)
$null=New-Item -ItemType Directory -Force $root;$root=(Resolve-Path $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=(Resolve-Path (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe')).Path
foreach($module in @('Network','Battery','Disk','Gpu')){
 $folder=Join-Path $root $module;$null=New-Item -ItemType Directory $folder
 $args=@('--r1-probe','--popup-module',$module,'--exit-after-seconds','10','--settings-file',('"'+$folder+'\settings.json"'),'--probe-report',('"'+$folder+'\probe.json"'),'--capture',('"'+$folder+'\bar.png"'))
 $p=Start-Process $exe -ArgumentList $args -WindowStyle Hidden -PassThru
 try{if(-not $p.WaitForExit(20000)){throw 'Owned smoke did not exit naturally; no termination attempted.'};$p.Refresh()
 $d=Get-Content (Join-Path $folder 'probe.json') -Raw|ConvertFrom-Json
 if($p.ExitCode -ne 0 -or $d.ChartWork -lt 2 -or $d.OutstandingPopupWorkers -ne 0 -or -not $d.PopupWorkersDrained -or -not $d.TimersStopped -or -not $d.AllTrayRemoved -or $d.PointerSize -ne 4){throw "$module lifecycle check failed"}
 [pscustomobject]@{Module=$module;Frames=$d.ChartWork;WorkersDrained=$d.PopupWorkersDrained;NaturalExit=$true;ResourceAcceptance=$false}|ConvertTo-Json|Set-Content (Join-Path $folder 'smoke-check.json')
 }finally{$p.Dispose()}
}
'Four module smokes completed; these are not five-minute resource passes.'
