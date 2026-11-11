#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$exe=(Resolve-Path -LiteralPath $Executable).Path
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh evidence directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$root=(Resolve-Path -LiteralPath $OutputDirectory).Path
$binaries=Get-ChildItem -LiteralPath (Split-Path $exe) -File | Where-Object { $_.Extension -in '.exe','.dll','.json' }
$before=@($binaries | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash)
$before | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'hashes-before.json')
$results=@()
foreach($variant in @('with','without')){
    $folder=Join-Path $root $variant
    $null=New-Item -ItemType Directory -Path $folder
    $report=Join-Path $folder 'provider.json'
    $target=Start-Process -FilePath $exe -ArgumentList @('--provider-comparison',$variant,('"'+$report+'"')) -WindowStyle Hidden -PassThru
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $target.Id -Scenario Popup -DurationSeconds 30 -WarmupSeconds 0 -AllowShortRun -OutputDirectory $folder
    $tail=[System.Collections.Generic.List[object]]::new()
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    while(-not $target.HasExited -and [DateTime]::UtcNow -lt $deadline){
        $target.Refresh()
        if(-not $target.HasExited){$tail.Add([pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');WorkingSetBytes=$target.WorkingSet64;PeakWorkingSetBytes=$target.PeakWorkingSet64})}
        Start-Sleep -Milliseconds 100
    }
    $tail | Export-Csv -LiteralPath (Join-Path $folder 'tail.csv') -NoTypeInformation
    if(-not $target.HasExited){throw 'Diagnostic did not exit within its bound; no process was terminated.'}
    $target.WaitForExit()
    if($target.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $report)){throw "Provider diagnostic $variant failed: $($target.ExitCode)"}
    $probe=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if($probe.WpfLoaded -or $probe.Reads -ne 30 -or $probe.ProcessArchitecture -ne 'X86' -or $probe.ValidCoreReads -eq 0){throw 'Provider-only prerequisites failed.'}
    $summary=Get-Content -LiteralPath (Join-Path $folder 'summary.json') -Raw | ConvertFrom-Json
    $peaks=@($summary.LifetimePeakWorkingSetBytes)+@($tail | ForEach-Object {$_.PeakWorkingSetBytes})+@($probe.Records | ForEach-Object {$_.PeakWorkingSetBytes})
    $peak=($peaks | Measure-Object -Maximum).Maximum
    $results+=[pscustomobject]@{Variant=$variant;AttributionOnly=$true;OverallAccepted=$false;LifetimePeakBytes=$peak;Within90MB=($peak -le 90000000);ObservedMaxBytes=$summary.WorkingSetMaxBytes;CpuMachinePercent=$summary.CpuMachineNormalizedPercent;DurationSeconds=$summary.DurationSeconds;Reads=$probe.Reads;ValidCoreReads=$probe.ValidCoreReads;ProcessRows=$probe.ProcessRows;WpfLoaded=$probe.WpfLoaded;NaturalExit=$probe.NaturalExit}
    $target.Dispose()
}
$after=@($binaries | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash)
$after | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'hashes-after.json')
$same=($before | ConvertTo-Json -Compress) -eq ($after | ConvertTo-Json -Compress)
@{AttributionOnly=$true;OverallAccepted=$false;BinaryHashesUnchanged=$same;Variants=$results} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'comparison.json')
if(-not $same){throw 'Binaries changed during comparison.'}
$results | Format-Table -AutoSize
