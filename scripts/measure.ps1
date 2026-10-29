#requires -Version 7.0
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
    [switch] $AllowShortRun
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
if ($DurationSeconds -lt 300 -and -not $AllowShortRun) {
    throw 'Acceptance requires at least 300 seconds. Use -AllowShortRun only for a diagnostic.'
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot "../artifacts/measurements/$Scenario-$([guid]::NewGuid().ToString('N'))"
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$samples = [System.Collections.Generic.List[object]]::new()
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
        Children = 'M0 has no child processes. Future helpers must be measured separately and included.'
    }
    $warmupClock = [Diagnostics.Stopwatch]::StartNew()
    while ($warmupClock.Elapsed.TotalSeconds -lt $WarmupSeconds) {
        $target.Refresh()
        if ($target.HasExited) { throw 'Target exited during warmup.' }
        Start-Sleep -Milliseconds 100
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextDue = 0.0
    do {
        $remaining = $nextDue - $clock.Elapsed.TotalSeconds
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int][math]::Ceiling(1000 * $remaining)) }
        $target.Refresh()
        if ($target.HasExited) { throw 'Target exited before the measurement completed.' }
        if ($target.StartTime.ToUniversalTime().Ticks -ne $identityTicks) { throw 'Process identity changed.' }
        $samples.Add([pscustomobject][ordered]@{
            ElapsedSeconds = $clock.Elapsed.TotalSeconds
            TotalCpuSeconds = $target.TotalProcessorTime.TotalSeconds
            WorkingSetBytes = $target.WorkingSet64
            PrivateBytes = $target.PrivateMemorySize64
            LifetimePeakWorkingSetBytes = $target.PeakWorkingSet64
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
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'summary.json') -Encoding utf8
    if ($null -ne $target) { $target.Dispose() }
    $observer.Dispose()
}
$summary | ConvertTo-Json -Depth 8
exit $exitCode
