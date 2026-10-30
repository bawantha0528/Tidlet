#requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'WorkerMeasurement.psm1') -Force
$active=[pscustomobject]@{CpuSeconds=2.5;WorkingSetBytes=20000000L;PrivateBytes=30000000L;PeakWorkingSetBytes=25000000L;Active=$true;IncludeExitPeak=$false}
$exit=[pscustomobject]@{CpuSeconds=1.5;WorkingSetBytes=0L;PrivateBytes=0L;PeakWorkingSetBytes=24000000L;Active=$false;IncludeExitPeak=$true}
$cost=Get-WorkerCost @($active,$exit)
if($cost.CpuSeconds -ne 4 -or $cost.WorkingSetBytes -ne 44000000 -or $cost.ActiveWorkers -ne 1){throw 'Active + exited worker costs were lost.'}
$exit.IncludeExitPeak=$false
$cost=Get-WorkerCost @($active,$exit)
if($cost.CpuSeconds -ne 4 -or $cost.WorkingSetBytes -ne 20000000){throw 'Exited CPU not retained or peak incorrectly counted twice.'}
$empty=Get-WorkerCost @()
if($empty.CpuSeconds -ne 0 -or $empty.ActiveWorkers -ne 0){throw 'Empty worker set is not zero.'}
$bad=$active.PSObject.Copy();$bad.CpuSeconds=-1
$rejected=$false;try{Get-WorkerCost @($bad)|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Negative worker costs accepted.'}
$root=Join-Path $PSScriptRoot '../artifacts/harness-tests/worker-journal'
$null=New-Item -ItemType Directory -Force $root
$path=Join-Path $root 'workers.jsonl'
$start=[ordered]@{ProcessId=123;StartedUtc='2026-10-30T00:00:00Z';ExitedUtc=$null;CpuSeconds=$null;PeakWorkingSetBytes=$null}
$finish=[ordered]@{ProcessId=123;StartedUtc='2026-10-30T00:00:00Z';ExitedUtc='2026-10-30T00:00:01Z';CpuSeconds=1.5;PeakWorkingSetBytes=24000000}
[IO.File]::WriteAllText($path,($start|ConvertTo-Json -Compress)+"`n"+($finish|ConvertTo-Json -Compress)+"`n"+'{"partial":')
$records=Get-WorkerRecords $path
if($records.Count -ne 1 -or @($records.Values)[0].CpuSeconds -ne 1.5){throw 'Journal lost final costs or consumed a partial line.'}
[pscustomobject]@{Result='Pass';Checks=5;Cases=@('Live + exited aggregation','Exited CPU retention','Empty set','Invalid cost rejection','Partial journal/exit update') }|ConvertTo-Json
