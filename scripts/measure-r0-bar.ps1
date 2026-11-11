#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[switch]$Solid,[switch]$SoftwareRendering,[switch]$VisualHost,[string]$ExecutablePath,[ValidateSet(0,4,8)][int]$ExpectedPointerSize=0)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if(($SoftwareRendering -or $VisualHost) -and -not $Solid){throw 'Software-only experiment requires -Solid.'}
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root
$root=(Resolve-Path -LiteralPath $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
if(-not $ExecutablePath){$ExecutablePath=Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe'}
$exe=(Resolve-Path -LiteralPath $ExecutablePath).Path
$report=Join-Path $root 'probe.json'
$args=@('--r0-probe','--exit-after-seconds','345','--probe-report',('"'+$report+'"'),'--capture',('"'+(Join-Path $root 'render-buffer.png')+'"'))
if($Solid){$args+='--solid'}
if($VisualHost){$args+='--visual-host'}
if($SoftwareRendering){$args+='--software-rendering'}
$hashes=foreach($file in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll','IsletMonitor.deps.json','IsletMonitor.runtimeconfig.json')){[pscustomobject]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $file) -Algorithm SHA256).Hash}}
$hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
$identity=[Security.Principal.WindowsIdentity]::GetCurrent();try{$admin=([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)}finally{$identity.Dispose()}
[ordered]@{Scenario='Bar';Milestone='R0 feasibility only';Transparent=(-not $Solid);SoftwareRendering=[bool]$SoftwareRendering;VisualHost=[bool]$VisualHost;WarmupSeconds=30;DurationSeconds=300;WorkingSetLimitBytes=75000000;CpuLimitPercent=1;LogicalProcessors=[Environment]::ProcessorCount;IsAdministrator=$admin;OtherTidletProcesses=@(Get-Process IsletMonitor -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id);StartedUtc=[DateTimeOffset]::UtcNow.ToString('O');CaptureKind='WPF render target, not a desktop screenshot';AppBarWorkAreaChanges=$false}|ConvertTo-Json|Set-Content (Join-Path $root 'context.json')
$p=Start-Process -FilePath $exe -ArgumentList $args -WindowStyle Hidden -PassThru
try{
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario Bar -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory $root
    $result=$LASTEXITCODE
    if(-not $p.WaitForExit(25000)){throw 'Feasibility fixture did not exit itself; no termination attempted.'}
    $p.Refresh();$data=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
    if($p.ExitCode -ne 0 -or -not $data.FirstFrame -or -not $data.TimersStopped -or $data.Samples -lt 100 -or $data.LegacyNativeRendererPresent -or $data.LegacyShellPresent -or $data.LoadedMediaAssemblies.Count){throw 'R0 lifecycle/source-exclusion proof failed.'}
    if($SoftwareRendering -and (-not $data.SoftwareRendering -or $data.ProcessRenderPreference -ne 'SoftwareOnly' -or $data.MenuCreations -ne 0)){throw 'Software rendering preference/lazy-menu proof failed.'}
    if($VisualHost -and (-not $data.VisualHost -or -not $data.AccessibilityPeerVerified)){throw 'Visual-host proof failed.'}
    if($ExpectedPointerSize -and ($data.PointerSize -ne $ExpectedPointerSize -or ($ExpectedPointerSize -eq 4 -and $data.ProcessArchitecture -ne 'X86') -or ($ExpectedPointerSize -eq 8 -and $data.ProcessArchitecture -ne 'X64'))){throw 'Measured process architecture does not match the requested experiment.'}
    [pscustomobject]@{Lifecycle='Pass';NumericalExit=$result;LegacyUiInBuild=$false;Samples=$data.Samples;Scale=$data.Scale;Capture='render-buffer.png'}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
}finally{$p.Dispose()}
exit $result

