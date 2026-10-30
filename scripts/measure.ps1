#requires -Version 5.1
<#
.SYNOPSIS
Read-only external process observer. Never changes settings, trims memory or terminates a process.
.DESCRIPTION
A five-minute minimum is required for acceptance. Short runs require -AllowShortRun
and always become DiagnosticOnly. Scenario is operator-supplied, not automatically
verified; Pass covers numerical limits only. Hidden behavior needs separate evidence.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int] $ProcessId,
    [ValidateSet('ScaffoldIdle','Collapsed','Expanded','Hidden','Fullscreen')]
    [string] $Scenario = 'ScaffoldIdle',
    [ValidateRange(1, 86400)][int] $DurationSeconds = 300,
    [ValidateRange(0, 600)][int] $WarmupSeconds = 30,
    [ValidateRange(0, 4096)][int] $LogicalProcessorCount = 0,
    [string] $OutputDirectory = '',
    [string] $ChildJournal = '',
    [switch] $AllowShortRun
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'WorkerMeasurement.psm1') -Force
if ($DurationSeconds -lt 300 -and -not $AllowShortRun) {
    throw 'Acceptance requires at least 300 seconds. Use -AllowShortRun only for a diagnostic.'
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot "../artifacts/measurements/$Scenario-$([guid]::NewGuid().ToString('N'))"
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$samples = [System.Collections.Generic.List[object]]::new()
$componentSamples = [System.Collections.Generic.List[object]]::new()
$observedExits = @{}
$aggregatePeak = 0L
$target = $null
$observer = [System.Diagnostics.Process]::GetCurrentProcess()
$observerCpuStart = $observer.TotalProcessorTime.TotalSeconds
$summary = $null
$exitCode = 1

try {
    $target = [System.Diagnostics.Process]::GetProcessById($ProcessId)
    $target.Refresh()
    $identityTicks = $target.StartTime.ToUniversalTime().Ticks
    $exePath = $target.MainModule.FileName
    $exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    $denominator = if ($LogicalProcessorCount) { $LogicalProcessorCount } else { [Environment]::ProcessorCount }
    $affinity = $target.ProcessorAffinity.ToInt64().ToString('X')
    $runtimeModule = @($target.Modules | Where-Object ModuleName -EQ 'coreclr.dll' | Select-Object -First 1)
    $runtimeVersion = if ($runtimeModule.Count) { $runtimeModule[0].FileVersionInfo.ProductVersion } else { $null }
    $metadata = [ordered]@{
        SchemaVersion = 1
        StartedUtc = [DateTime]::UtcNow.ToString('O')
        ProcessId = $ProcessId
        ProcessStartUtc = $target.StartTime.ToUniversalTime().ToString('O')
        ExecutableName = [IO.Path]::GetFileName($exePath)
        ExecutableSHA256 = $exeHash
        ProcessArchitecture = 'TODO(verify): set from measured build configuration'
        RuntimeProductVersion = $runtimeVersion
        WindowsVersion = [Environment]::OSVersion.VersionString
        ProcessorAffinityHex = $affinity
        DenominatorSource = if ($LogicalProcessorCount) { 'Operator supplied' } else { 'Observer Environment.ProcessorCount; verify target quota/group capacity' }
        WarmupSeconds = $WarmupSeconds
        ScenarioIsOperatorSupplied = $true
        IsAcceptanceDuration = $DurationSeconds -ge 300
        NumericalLimitsOnly = $true
        Children = if ($ChildJournal) { 'Aggregate parent plus identity-checked journaled workers; exited CPU retained; exit peaks conservatively included once.' } else { 'No child journal supplied; helpers require aggregate accounting.' }
    }
    $warmupClock = [Diagnostics.Stopwatch]::StartNew()
    while ($warmupClock.Elapsed.TotalSeconds -lt $WarmupSeconds) {
        $target.Refresh()
        if ($target.HasExited) { throw 'Target exited during warmup.' }
        Start-Sleep -Milliseconds 100
    }
    $baselineWorkers=Get-WorkerRecords $ChildJournal
    foreach($key in @($baselineWorkers.Keys)) { if($baselineWorkers[$key].ExitedUtc){$observedExits[$key]=$true} }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextDue = 0.0
    do {
        $remaining = $nextDue - $clock.Elapsed.TotalSeconds
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int][math]::Ceiling(1000 * $remaining)) }
        $target.Refresh()
        if ($target.HasExited) { throw 'Target exited before the measurement completed.' }
        if ($target.StartTime.ToUniversalTime().Ticks -ne $identityTicks) { throw 'Process identity changed.' }
        $parentCpu=$target.TotalProcessorTime.TotalSeconds; $parentWorking=$target.WorkingSet64
        $parentPrivate=$target.PrivateMemorySize64; $parentPeak=$target.PeakWorkingSet64
        $components = [System.Collections.Generic.List[object]]::new()
        $records=Get-WorkerRecords $ChildJournal
        foreach($key in @($records.Keys)) {
            $record=$records[$key]
            $child=$null
            try {
                if ($record.ExitedUtc) {
                    if ($null -eq $record.CpuSeconds -or -not $record.PeakWorkingSetBytes) { throw 'Exited worker has no complete resource costs.' }
                    $components.Add([pscustomobject]@{ProcessId=$record.ProcessId;StartedUtc=$record.StartedUtc;StartTimeUtcTicks=$record.StartTimeUtcTicks;Active=$false;CpuSeconds=[double]$record.CpuSeconds;
                        WorkingSetBytes=0L;PrivateBytes=0L;PeakWorkingSetBytes=[long]$record.PeakWorkingSetBytes;IncludeExitPeak=(-not $observedExits.ContainsKey($key))})
                    $observedExits[$key]=$true
                } else {
                    try { $child=[Diagnostics.Process]::GetProcessById($record.ProcessId); $child.Refresh(); $childWorking=$child.WorkingSet64 }
                    catch {
                        Start-Sleep -Milliseconds 50
                        $updated=Get-WorkerRecords $ChildJournal
                        if (-not $updated.ContainsKey($key) -or -not $updated[$key].ExitedUtc) { throw 'Worker exited without a final cost record; evidence incomplete.' }
                        $record=$updated[$key]
                        if ($null -eq $record.CpuSeconds -or -not $record.PeakWorkingSetBytes) { throw 'Exited worker cost missing.' }
                        $components.Add([pscustomobject]@{ProcessId=$record.ProcessId;StartedUtc=$record.StartedUtc;StartTimeUtcTicks=$record.StartTimeUtcTicks;Active=$false;CpuSeconds=[double]$record.CpuSeconds;
                            WorkingSetBytes=0L;PrivateBytes=0L;PeakWorkingSetBytes=[long]$record.PeakWorkingSetBytes;IncludeExitPeak=(-not $observedExits.ContainsKey($key))})
                        $observedExits[$key]=$true
                        continue
                    }
                    if ($child.StartTime.ToUniversalTime().Ticks -ne [long]$record.StartTimeUtcTicks -or
                        $child.MainModule.FileName -ne $exePath) { throw 'Worker identity/executable changed.' }
                    $components.Add([pscustomobject]@{ProcessId=$record.ProcessId;StartedUtc=$record.StartedUtc;StartTimeUtcTicks=$record.StartTimeUtcTicks;Active=$true;CpuSeconds=$child.TotalProcessorTime.TotalSeconds;
                        WorkingSetBytes=$childWorking;PrivateBytes=$child.PrivateMemorySize64;PeakWorkingSetBytes=$child.PeakWorkingSet64;IncludeExitPeak=$false})
                }
            } finally { if($child){$child.Dispose()} }
        }
        $cost=Get-WorkerCost -Components $components.ToArray()
        $aggregatePeak=[math]::Max($aggregatePeak,$parentPeak+$cost.PeakWorkingSetBytes)
        foreach($component in $components) {
            $componentSamples.Add([pscustomobject]@{SampleIndex=$samples.Count;ElapsedSeconds=$clock.Elapsed.TotalSeconds;ProcessId=$component.ProcessId;StartedUtc=$component.StartedUtc;
                StartTimeUtcTicks=$component.StartTimeUtcTicks;Active=$component.Active;CpuSeconds=$component.CpuSeconds;WorkingSetBytes=$component.WorkingSetBytes;PrivateBytes=$component.PrivateBytes;
                PeakWorkingSetBytes=$component.PeakWorkingSetBytes;IncludeExitPeak=$component.IncludeExitPeak})
        }
        $samples.Add([pscustomobject][ordered]@{
            SampleIndex = $samples.Count
            ElapsedSeconds = $clock.Elapsed.TotalSeconds
            TotalCpuSeconds = $parentCpu + $cost.CpuSeconds
            WorkingSetBytes = $parentWorking + $cost.WorkingSetBytes
            PrivateBytes = $parentPrivate + $cost.PrivateBytes
            LifetimePeakWorkingSetBytes = $aggregatePeak
            ParentCpuSeconds = $parentCpu
            ParentWorkingSetBytes = $parentWorking
            ParentPrivateBytes = $parentPrivate
            WorkerCpuSeconds = $cost.CpuSeconds
            WorkerWorkingSetBytes = $cost.WorkingSetBytes
            WorkerPrivateBytes = $cost.PrivateBytes
            ActiveWorkers = $cost.ActiveWorkers
            HandleCount = $target.HandleCount
            ThreadCount = $target.Threads.Count
        })
        $nextDue += 1.0
    } while (($samples[-1].ElapsedSeconds - $samples[0].ElapsedSeconds) -lt $DurationSeconds)

    $summary = Get-MeasurementSummary -Samples $samples.ToArray() -RequiredDurationSeconds $DurationSeconds -LogicalProcessorCount $denominator -Scenario $Scenario
    if ($DurationSeconds -lt 300) {
        $summary.Result = 'DiagnosticOnly'
    }
    $summary | Add-Member NoteProperty Metadata ([pscustomobject]$metadata)
    $exitCode = if ($summary.Violations.Count) { 2 } else { 0 }
}
catch {
    $summary = [pscustomobject][ordered]@{
        Scenario = $Scenario
        Result = 'Incomplete'
        Error = $_.Exception.Message
        SampleCount = $samples.Count
        IsAcceptanceMeasurement = $false
    }
}
finally {
    $observer.Refresh()
    $summary | Add-Member NoteProperty ObserverCpuSeconds ($observer.TotalProcessorTime.TotalSeconds - $observerCpuStart)
    if ($samples.Count) { $samples.ToArray() | Export-Csv -LiteralPath (Join-Path $outputRoot 'samples.csv') -NoTypeInformation -Encoding utf8 }
    if ($ChildJournal) { $componentSamples.ToArray() | Export-Csv -LiteralPath (Join-Path $outputRoot 'workers.csv') -NoTypeInformation -Encoding utf8 }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json') -Encoding utf8
    if ($null -ne $target) { $target.Dispose() }
    $observer.Dispose()
}
$summary | ConvertTo-Json -Depth 8
exit $exitCode

