#requires -Version 5.1
# Shared journal reader/accounting for aggregate process measurements.
Set-StrictMode -Version Latest
function Get-WorkerRecords([string]$Path) {
    $records=@{}
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return $records }
    $raw=[IO.File]::ReadAllText($Path)
    $lines=$raw.Split("`n")
    for($index=0;$index -lt $lines.Length-1;$index++) {
        if ([string]::IsNullOrWhiteSpace($lines[$index])) { continue }
        $record=$lines[$index]|ConvertFrom-Json
        if ($record.ProcessId -le 0 -or -not $record.StartedUtc) { throw 'Invalid worker identity.' }
        $identity=if($record.PSObject.Properties['StartTimeUtcTicks']){[string]$record.StartTimeUtcTicks}else{[string]$record.StartedUtc}
        $key=[string]$record.ProcessId+'|'+$identity
        if ($records.ContainsKey($key) -and $records[$key].ExitedUtc -and -not $record.ExitedUtc) { throw 'Worker journal reversed an exit.' }
        $records[$key]=$record
    }
    return $records
}
function Get-WorkerCost([object[]]$Components) {
    $cpu=0.0; $working=0L; $private=0L; $peak=0L; $active=0
    foreach($component in $Components) {
        if ([double]::IsNaN([double]$component.CpuSeconds) -or [double]::IsInfinity([double]$component.CpuSeconds) -or $component.CpuSeconds -lt 0 -or $component.WorkingSetBytes -lt 0 -or $component.PrivateBytes -lt 0 -or $component.PeakWorkingSetBytes -lt $component.WorkingSetBytes) { throw 'Invalid worker cost.' }
        $cpu += [double]$component.CpuSeconds
        if ($component.Active) { $active++; $working += [long]$component.WorkingSetBytes; $private += [long]$component.PrivateBytes; $peak += [long]$component.PeakWorkingSetBytes }
        elseif($component.IncludeExitPeak) { $working += [long]$component.PeakWorkingSetBytes; $peak += [long]$component.PeakWorkingSetBytes }
    }
    [pscustomobject]@{CpuSeconds=$cpu;WorkingSetBytes=$working;PrivateBytes=$private;PeakWorkingSetBytes=$peak;ActiveWorkers=$active}
}
Export-ModuleMember -Function Get-WorkerRecords,Get-WorkerCost

function Assert-WorkerAggregateEvidence([string]$Root,[object]$Probe,[string]$Scenario) {
    $records=Get-WorkerRecords (Join-Path $Root 'probe.workers.jsonl')
    if($records.Count -ne $Probe.WorkerStarts){throw 'Worker journal omitted starts.'}
    if($Probe.LoadedMediaAssemblies.Count -ne 0){throw 'WinRT projection loaded in the main process.'}
    foreach($record in $records.Values){
        if(-not $record.ExitedUtc -or $null -eq $record.CpuSeconds -or -not $record.PeakWorkingSetBytes -or
            $record.ExitCode -ne 0 -or $null -eq $record.FinalWpfAssemblies -or $record.FinalWpfAssemblies.Count -ne 0){throw 'Worker exit/cost/module evidence incomplete.'}
    }
    $workerPath=Join-Path $Root 'workers.csv';$rows=@()
    if((Test-Path $workerPath) -and (Get-Item $workerPath).Length){$rows=@(Import-Csv -LiteralPath $workerPath)}
    $groups=@{}
    foreach($row in $rows){$index=[int]$row.SampleIndex;if(-not $groups.ContainsKey($index)){$groups[$index]=[Collections.Generic.List[object]]::new()};$groups[$index].Add($row)}
    $samples=@(Import-Csv -LiteralPath (Join-Path $Root 'samples.csv'))
    for($index=0;$index -lt $samples.Count;$index++){
        $sample=$samples[$index];if([int]$sample.SampleIndex -ne $index){throw 'Aggregate sample index changed.'}
        $components=@();$identities=@{}
        if($groups.ContainsKey($index)){
            $components=@(foreach($row in $groups[$index]){
                $key=[string]$row.ProcessId+'|'+[string]$row.StartTimeUtcTicks
                if(-not $records.ContainsKey($key) -or $identities.ContainsKey($key)){throw 'Missing or duplicated worker identity.'};$identities[$key]=$true
                if([double]$row.CpuSeconds -gt [double]$records[$key].CpuSeconds+0.001){throw 'Sample worker CPU exceeds its final cost.'}
                [pscustomobject]@{CpuSeconds=[double]$row.CpuSeconds;WorkingSetBytes=[long]$row.WorkingSetBytes;PrivateBytes=[long]$row.PrivateBytes;
                    PeakWorkingSetBytes=[long]$row.PeakWorkingSetBytes;Active=($row.Active -eq 'True');IncludeExitPeak=($row.IncludeExitPeak -eq 'True')}
            })
        }
        # These trials require a stable worker set after warmup: one expanded worker or all exercise workers exited.
        if($components.Count -ne $records.Count){throw 'A worker cost was omitted from a measurement sample.'}
        $cost=Get-WorkerCost $components
        if([math]::Abs(([double]$sample.ParentCpuSeconds+$cost.CpuSeconds)-[double]$sample.TotalCpuSeconds) -gt 0.000001 -or
            [long]$sample.ParentWorkingSetBytes+$cost.WorkingSetBytes -ne [long]$sample.WorkingSetBytes -or
            [long]$sample.ParentPrivateBytes+$cost.PrivateBytes -ne [long]$sample.PrivateBytes -or
            $cost.ActiveWorkers -ne [int]$sample.ActiveWorkers){throw 'Parent/worker CSV does not reproduce aggregate costs.'}
        if($Scenario -in @('Collapsed','PostExpansion','Hidden','Fullscreen') -and $cost.ActiveWorkers -ne 0){throw 'Invisible media kept a worker alive.'}
        if($Scenario -eq 'Expanded' -and $Probe.Style -eq 1 -and $Probe.MediaCapability.Availability -eq 0 -and $cost.ActiveWorkers -ne 1){throw 'Expanded Notch omitted the media worker.'}
    }
    if(@($groups.Keys|Where-Object{$_ -lt 0 -or $_ -ge $samples.Count}).Count){throw 'Orphan worker component rows.'}
    return $true
}
Export-ModuleMember -Function Assert-WorkerAggregateEvidence
