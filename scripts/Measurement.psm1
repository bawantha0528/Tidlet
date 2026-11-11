Set-StrictMode -Version Latest

function Get-MeasurementSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]] $Samples,
        [Parameter(Mandatory)][ValidateRange(1, 86400)][int] $RequiredDurationSeconds,
        [ValidateRange(1, 4096)][int] $LogicalProcessorCount = 1,
        [ValidateRange(0.01, 3600)][double] $SampleIntervalSeconds = 1,
        [ValidateSet('ScaffoldIdle','Collapsed','Expanded','Hidden','Fullscreen','Bar','Popup','Application')]
        [string] $Scenario = 'ScaffoldIdle'
    )

    if ($Samples.Count -lt 2) { throw 'At least two observations are required.' }
    $previous = $null
    foreach ($sample in $Samples) {
        foreach ($property in @('ElapsedSeconds','TotalCpuSeconds','WorkingSetBytes','PrivateBytes','LifetimePeakWorkingSetBytes')) {
            $v = [double]$sample.$property; if ($null -eq $sample.$property -or [double]::IsNaN($v) -or [double]::IsInfinity($v)) {
                throw "Missing or nonfinite observation: $property."
            }
        }
        if ($sample.ElapsedSeconds -lt 0 -or $sample.TotalCpuSeconds -lt 0 -or
            $sample.WorkingSetBytes -le 0 -or $sample.PrivateBytes -lt 0 -or
            $sample.LifetimePeakWorkingSetBytes -lt $sample.WorkingSetBytes) {
            throw 'Invalid memory, time or CPU observation.'
        }
        if ($null -ne $previous) {
            if ($sample.ElapsedSeconds -le $previous.ElapsedSeconds -or
                $sample.TotalCpuSeconds -lt $previous.TotalCpuSeconds) {
                throw 'Observations must have increasing elapsed time and nondecreasing CPU time.'
            }
            if (($sample.ElapsedSeconds - $previous.ElapsedSeconds) -gt 2.5 * $SampleIntervalSeconds) {
                throw 'Observation gap is too large; measurement is incomplete.'
            }
        }
        $previous = $sample
    }

    $first = $Samples[0]
    $last = $Samples[-1]
    $elapsed = [double]$last.ElapsedSeconds - [double]$first.ElapsedSeconds
    if ($elapsed -lt $RequiredDurationSeconds) { throw 'Measurement duration is incomplete.' }
    $minimumCount = 1 + [math]::Floor($RequiredDurationSeconds / $SampleIntervalSeconds)
    if ($Samples.Count -lt $minimumCount) { throw 'Required observations are missing.' }

    $cpuSeconds = [double]$last.TotalCpuSeconds - [double]$first.TotalCpuSeconds
    $coreCpu = 100.0 * $cpuSeconds / $elapsed
    $machineCpu = $coreCpu / $LogicalProcessorCount
    $workingSets = @($Samples | ForEach-Object { [long]$_.WorkingSetBytes } | Sort-Object)
    $middle = [int][math]::Floor($workingSets.Count / 2)
    $median = if ($workingSets.Count % 2) { [double]$workingSets[$middle] } else {
        ([double]$workingSets[$middle - 1] + [double]$workingSets[$middle]) / 2.0
    }
    $p95Index = [int][math]::Ceiling(0.95 * $workingSets.Count) - 1
    $maximum = [long]$workingSets[-1]
    $violations = [System.Collections.Generic.List[string]]::new()
    $memoryLimit = switch ($Scenario) { 'Application' { 100000000L }; 'Bar' { 90000000L }; 'Popup' { 90000000L }; 'Hidden' { 90000000L }; 'Fullscreen' { 90000000L }; 'Collapsed' { 60000000L }; 'Expanded' { 100000000L }; default { $null } }
    if ($null -ne $memoryLimit -and $maximum -gt $memoryLimit) { $violations.Add('WorkingSet') }
    if ($Scenario -eq 'Bar' -and $machineCpu -ge 1.0) { $violations.Add('BarCpu') }
    if ($Scenario -eq 'Popup' -and $machineCpu -ge 1.0) { $violations.Add('PopupCpu') }
    if ($Scenario -eq 'Application' -and $machineCpu -ge 1.0) { $violations.Add('ApplicationCpu') }
    if ($Scenario -eq 'Collapsed' -and $machineCpu -ge 1.0) { $violations.Add('CollapsedCpu') }
    if ($Scenario -in @('Hidden','Fullscreen') -and $coreCpu -gt 0.1) { $violations.Add('SuppressedCpu') }

    [pscustomobject][ordered]@{
        Scenario = $Scenario
        Result = if ($violations.Count) { 'Fail' } elseif ($Scenario -eq 'ScaffoldIdle') { 'BaselineOnly' } else { 'Pass' }
        Violations = @($violations.ToArray())
        DurationSeconds = $elapsed
        RequiredDurationSeconds = $RequiredDurationSeconds
        SampleCount = $Samples.Count
        SampleIntervalSeconds = $SampleIntervalSeconds
        WorkingSetMeanBytes = [double]($workingSets | Measure-Object -Average).Average
        WorkingSetMedianBytes = $median
        WorkingSetP95Bytes = [long]$workingSets[$p95Index]
        WorkingSetMaxBytes = $maximum
        WorkingSetMaxMB = $maximum / 1000000.0
        WorkingSetMaxMiB = $maximum / 1048576.0
        LifetimePeakWorkingSetBytes = [long]($Samples.LifetimePeakWorkingSetBytes | Measure-Object -Maximum).Maximum
        CpuSeconds = $cpuSeconds
        CpuCoreEquivalentPercent = $coreCpu
        CpuMachineNormalizedPercent = $machineCpu
        LogicalProcessorDenominator = $LogicalProcessorCount
        HiddenBehaviorVerified = $false
    }
}

function Test-SessionMemory {
    param([Parameter(Mandatory)][long]$CurrentMaximumBytes,[Parameter(Mandatory)][long]$LifetimePeakBytes,[bool]$ApplicationSession=$false,[bool]$ApplicationOpen=$false)
    if($CurrentMaximumBytes -le 0 -or $LifetimePeakBytes -lt $CurrentMaximumBytes -or ($ApplicationOpen -and -not $ApplicationSession)){throw 'Invalid session memory evidence.'}
    $currentLimit=if($ApplicationSession -and $ApplicationOpen){100000000L}else{90000000L}
    $lifetimeLimit=if($ApplicationSession){100000000L}else{90000000L}
    [pscustomobject]@{Policy='31 October application 100 MB; fresh bar/popup and post-close current 90 MB';CurrentLimitBytes=$currentLimit;LifetimeLimitBytes=$lifetimeLimit;CurrentWithinLimit=($CurrentMaximumBytes -le $currentLimit);LifetimeWithinLimit=($LifetimePeakBytes -le $lifetimeLimit);Accepted=($CurrentMaximumBytes -le $currentLimit -and $LifetimePeakBytes -le $lifetimeLimit)}
}
Export-ModuleMember -Function Get-MeasurementSummary,Test-SessionMemory


