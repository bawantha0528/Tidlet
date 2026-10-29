#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
$checks = 0
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-Condition $rejected $Message
}
function New-Fixture([double]$CpuEnd = 12, [long]$Maximum = 60000000) {
    @(
        [pscustomobject]@{ ElapsedSeconds=0.0; TotalCpuSeconds=10.0; WorkingSetBytes=40000000L; PrivateBytes=30000000L; LifetimePeakWorkingSetBytes=70000000L }
        [pscustomobject]@{ ElapsedSeconds=150.0; TotalCpuSeconds=((10.0 + $CpuEnd) / 2); WorkingSetBytes=50000000L; PrivateBytes=30000000L; LifetimePeakWorkingSetBytes=70000000L }
        [pscustomobject]@{ ElapsedSeconds=300.0; TotalCpuSeconds=$CpuEnd; WorkingSetBytes=$Maximum; PrivateBytes=30000000L; LifetimePeakWorkingSetBytes=[math]::Max(70000000L,$Maximum) }
    )
}
$common = @{ RequiredDurationSeconds=300; LogicalProcessorCount=8; SampleIntervalSeconds=150 }
$summary = Get-MeasurementSummary -Samples (New-Fixture) @common -Scenario Collapsed
Assert-Condition ($summary.Result -eq 'Pass') 'Exact 60 MB should meet memory budget.'
Assert-Condition ([math]::Abs($summary.CpuCoreEquivalentPercent - (2.0/3)) -lt 0.000001) 'Core-equivalent CPU formula incorrect.'
Assert-Condition ([math]::Abs($summary.CpuMachineNormalizedPercent - (1.0/12)) -lt 0.000001) 'Normalized CPU formula incorrect.'
Assert-Condition ($summary.WorkingSetMeanBytes -eq 50000000 -and $summary.WorkingSetMedianBytes -eq 50000000) 'Memory statistics incorrect.'
Assert-Condition ($summary.WorkingSetP95Bytes -eq 60000000 -and $summary.LifetimePeakWorkingSetBytes -eq 70000000) 'Peak/p95 statistics incorrect.'
$over = Get-MeasurementSummary -Samples (New-Fixture -Maximum 60000001) @common -Scenario Collapsed
Assert-Condition ($over.Result -eq 'Fail' -and $over.Violations -contains 'WorkingSet') 'Memory breach must fail.'
$cpu = Get-MeasurementSummary -Samples (New-Fixture -CpuEnd 34) @common -Scenario Collapsed
Assert-Condition ($cpu.Result -eq 'Fail' -and $cpu.Violations -contains 'CollapsedCpu') 'Exactly 1% normalized CPU must fail strict <1% budget.'
$hidden = Get-MeasurementSummary -Samples (New-Fixture -CpuEnd 10.31) @common -Scenario Hidden
Assert-Condition ($hidden.Result -eq 'Fail') 'Hidden CPU breach must fail.'
$baseline = Get-MeasurementSummary -Samples (New-Fixture) @common -Scenario ScaffoldIdle
Assert-Condition ($baseline.Result -eq 'BaselineOnly') 'Scaffold must never claim island acceptance.'

$bad = New-Fixture
$bad[2].ElapsedSeconds = 299
Assert-Rejected { Get-MeasurementSummary -Samples $bad @common } 'Incomplete duration accepted.'
$bad = New-Fixture
$bad[2].TotalCpuSeconds = 0
Assert-Rejected { Get-MeasurementSummary -Samples $bad @common } 'Counter reversal accepted.'
$bad = New-Fixture
$bad[1].WorkingSetBytes = [double]::NaN
Assert-Rejected { Get-MeasurementSummary -Samples $bad @common } 'NaN accepted.'
Assert-Rejected { Get-MeasurementSummary -Samples (New-Fixture) -RequiredDurationSeconds 300 -SampleIntervalSeconds 1 } 'Missing observations accepted.'
Assert-Rejected { Get-MeasurementSummary -Samples @((New-Fixture)[0]) @common } 'One observation accepted.'

$out = Join-Path $PSScriptRoot '../artifacts/harness-tests'
$null = New-Item -ItemType Directory -Path $out -Force
$invalidPidOut = Join-Path $out 'invalid-pid'
& (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId 2147483647 -DurationSeconds 1 -WarmupSeconds 0 -AllowShortRun -OutputDirectory $invalidPidOut | Out-Null
Assert-Condition ($LASTEXITCODE -ne 0) 'Invalid PID should fail.'
$invalidResult = Get-Content -LiteralPath (Join-Path $invalidPidOut 'summary.json') -Raw | ConvertFrom-Json
Assert-Condition ($invalidResult.Result -eq 'Incomplete') 'Invalid PID result must be Incomplete.'
# Real harness integration uses only owned naturally exiting fixtures.
$pwshPath = (Get-Process -Id $PID).Path
$early = Start-Process -FilePath $pwshPath -ArgumentList '-NoProfile','-Command','Start-Sleep -Milliseconds 200' -PassThru -WindowStyle Hidden
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $early.Id -DurationSeconds 2 -WarmupSeconds 1 -AllowShortRun -OutputDirectory (Join-Path $out 'early-exit') | Out-Null
Assert-Condition ($LASTEXITCODE -ne 0) 'Natural early exit should fail measurement.'
$earlyResult = Get-Content -LiteralPath (Join-Path $out 'early-exit/summary.json') -Raw | ConvertFrom-Json
Assert-Condition ($earlyResult.Result -eq 'Incomplete') 'Early exit cannot be a pass.'
$early.Dispose()
$short = Start-Process -FilePath $pwshPath -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 4' -PassThru -WindowStyle Hidden
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $short.Id -DurationSeconds 1 -WarmupSeconds 0 -AllowShortRun -OutputDirectory (Join-Path $out 'diagnostic') | Out-Null
Assert-Condition ($LASTEXITCODE -eq 0) 'Complete diagnostic fixture should run.'
$shortResult = Get-Content -LiteralPath (Join-Path $out 'diagnostic/summary.json') -Raw | ConvertFrom-Json
Assert-Condition ($shortResult.Result -eq 'DiagnosticOnly') 'Short run cannot claim budget acceptance.'
$short.WaitForExit(5000) | Out-Null
$short.Dispose()

# Synthetic TRX fixtures prove the nonempty-suite guard rejects empty/failed/skipped output.
$trxRoot = Join-Path $out 'trx-fixtures'
$null = New-Item -ItemType Directory -Path $trxRoot -Force
function Write-TrxFixture([int]$Total, [int]$Executed, [int]$Passed, [int]$Failed = 0) {
    @"
<TestRun><ResultSummary><Counters total="$Total" executed="$Executed" passed="$Passed" failed="$Failed" /></ResultSummary><TestDefinitions><UnitTest storage="Fixture.Tests.dll" /></TestDefinitions></TestRun>
"@ | Set-Content -LiteralPath (Join-Path $trxRoot 'fixture.trx')
}
Write-TrxFixture 1 1 1
& $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'verify-test-results.ps1') -ResultsDirectory $trxRoot -ExpectedAssemblies Fixture.Tests.dll | Out-Null
Assert-Condition ($LASTEXITCODE -eq 0) 'Valid TRX fixture should pass.'
foreach ($fixture in @(@(0,0,0,0),@(1,1,0,1),@(1,0,0,0))) {
    Write-TrxFixture @fixture
    & $pwshPath -NoProfile -File (Join-Path $PSScriptRoot 'verify-test-results.ps1') -ResultsDirectory $trxRoot -ExpectedAssemblies Fixture.Tests.dll 2>$null | Out-Null
    Assert-Condition ($LASTEXITCODE -ne 0) 'Empty, failed or unexecuted fixture was accepted.'
}

[pscustomobject]@{ Result='Pass'; Checks=$checks } | ConvertTo-Json | Tee-Object -FilePath (Join-Path $out 'checks.json')

exit 0
