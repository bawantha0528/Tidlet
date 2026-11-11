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
$proof=Join-Path $root 'aggregate-proof';$null=New-Item -ItemType Directory -Force $proof
$record=[ordered]@{ProcessId=123;StartedUtc='2026-10-30T00:00:00Z';StartTimeUtcTicks=123;ExitedUtc='2026-10-30T00:00:10Z';CpuSeconds=2.0;PeakWorkingSetBytes=25000000;ExitCode=0;FinalWpfAssemblies=@()}
[IO.File]::WriteAllText((Join-Path $proof 'probe.workers.jsonl'),($record|ConvertTo-Json -Compress)+"`n")
$component=[pscustomobject]@{SampleIndex=0;ProcessId=123;StartTimeUtcTicks=123;CpuSeconds=1.0;WorkingSetBytes=20000000;PrivateBytes=15000000;PeakWorkingSetBytes=24000000;Active=$true;IncludeExitPeak=$false}
$component |Export-Csv (Join-Path $proof 'workers.csv') -NoTypeInformation
$sample=[pscustomobject]@{SampleIndex=0;ParentCpuSeconds=3.0;ParentWorkingSetBytes=40000000;ParentPrivateBytes=30000000;TotalCpuSeconds=4.0;WorkingSetBytes=60000000;PrivateBytes=45000000;ActiveWorkers=1}
$sample |Export-Csv (Join-Path $proof 'samples.csv') -NoTypeInformation
$probe=[pscustomobject]@{WorkerStarts=1;LoadedMediaAssemblies=@();Style=1;MediaCapability=[pscustomobject]@{Availability=0}}
$null=Assert-WorkerAggregateEvidence $proof $probe 'Expanded'
$componentOriginal=[IO.File]::ReadAllText((Join-Path $proof 'workers.csv'))
[IO.File]::WriteAllText((Join-Path $proof 'workers.csv'),'')
$rejected=$false;try{$null=Assert-WorkerAggregateEvidence $proof $probe 'Expanded'}catch{$rejected=$true};if(-not $rejected){throw 'Omitted helper costs accepted.'}
[IO.File]::WriteAllText((Join-Path $proof 'workers.csv'),$componentOriginal)
$sample.WorkingSetBytes=40000000;$sample|Export-Csv (Join-Path $proof 'samples.csv') -NoTypeInformation
$rejected=$false;try{$null=Assert-WorkerAggregateEvidence $proof $probe 'Expanded'}catch{$rejected=$true};if(-not $rejected){throw 'Altered aggregate accepted.'}[pscustomobject]@{Result='Pass';Checks=8;Cases=@('Live + exited aggregation','Exited CPU retention','Empty set','Invalid cost rejection','Partial journal/exit update','Component recomputation','Omitted worker rejected','Altered aggregate rejected') }|ConvertTo-Json
