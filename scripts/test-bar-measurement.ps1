#requires -Version 5.1
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
function Sample([int]$second,[long]$memory,[double]$cpu){[pscustomobject]@{ElapsedSeconds=$second;TotalCpuSeconds=$cpu;WorkingSetBytes=$memory;PrivateBytes=10000000L;LifetimePeakWorkingSetBytes=$memory;HandleCount=100;ThreadCount=3}}
function Summary([long]$bytes,[double]$cpu,[string]$scenario){$samples=@(0..300|ForEach-Object{Sample $_ $bytes ($cpu*$_/300)});Get-MeasurementSummary -Samples $samples -RequiredDurationSeconds 300 -LogicalProcessorCount 8 -Scenario $scenario}
if((Summary 90000000 0 'Bar').Result -ne 'Pass'){throw 'Exact bar memory boundary rejected'}
if((Summary 90000001 0 'Bar').Result -ne 'Fail'){throw 'One-byte bar breach accepted'}
if((Summary 90000000 0 'Popup').Result -ne 'Pass'){throw 'Exact popup memory boundary rejected'}
if((Summary 90000001 0 'Popup').Result -ne 'Fail'){throw 'One-byte popup breach accepted'}
if((Summary 40000000 24 'Bar').Result -ne 'Fail'){throw 'CPU at one percent accepted'}
if((Summary 40000000 23.9 'Bar').Result -ne 'Pass'){throw 'CPU below one percent rejected'}
if((Summary 40000000 24 'Popup').Result -ne 'Fail'){throw 'Popup CPU at one percent accepted'}
if((Summary 90000001 0 'Fullscreen').Result -ne 'Fail'){throw 'Hidden memory breach accepted'}
if((Summary 100000000 0 'Application').Result -ne 'Pass'){throw 'Exact application memory boundary rejected'}
if((Summary 100000001 0 'Application').Result -ne 'Fail'){throw 'One-byte application breach accepted'}
if((Summary 40000000 24 'Application').Result -ne 'Fail'){throw 'Application CPU at one percent accepted'}
foreach($case in @(
 @(100000000L,100000000L,$true,$true,$true),
 @(100000000L,100000001L,$true,$true,$false),
 @(90000001L,95000000L,$true,$false,$false),
 @(90000000L,100000000L,$true,$false,$true),
 @(90000000L,100000001L,$true,$false,$false),
 @(90000000L,90000001L,$false,$false,$false)
)){
 $policy=Test-SessionMemory -CurrentMaximumBytes $case[0] -LifetimePeakBytes $case[1] -ApplicationSession $case[2] -ApplicationOpen $case[3]
 if($policy.Accepted -ne $case[4]){throw 'Application/fresh/after-close lifetime policy mismatch'}
}
$rejected=$false;try{Test-SessionMemory -CurrentMaximumBytes 1 -LifetimePeakBytes 1 -ApplicationOpen $true|Out-Null}catch{$rejected=$true}
if(-not $rejected){throw 'Application-open evidence accepted without an application session'}
[pscustomobject]@{Result='Pass';Checks=18}|ConvertTo-Json
