#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory='', [string]$ExecutablePath='')
$ErrorActionPreference='Stop'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot ('../artifacts/r0-smoke/'+[guid]::NewGuid().ToString('N'))}
$null=New-Item -ItemType Directory -Force $OutputDirectory
$root=(Resolve-Path -LiteralPath $OutputDirectory).Path
if(-not $ExecutablePath){$ExecutablePath=Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe'}
$exe=(Resolve-Path -LiteralPath $ExecutablePath).Path
$checks=foreach($solid in @($false,$true)){
    $name=if($solid){'solid'}else{'transparent'};$report=Join-Path $root ($name+'.json')
    $arguments=@('--r0-probe','--exit-after-seconds','5','--probe-report',('"'+$report+'"'),'--capture',('"'+(Join-Path $root ($name+'.png'))+'"'));if($solid){$arguments+='--solid'}
    $p=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    try{
        if(-not $p.WaitForExit(15000)){throw 'R0 fixture did not exit naturally'};$p.Refresh();$data=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
        if($p.ExitCode -ne 0 -or -not $data.FirstFrame -or -not $data.TimersStopped -or $data.Samples -lt 2 -or $data.LegacyNativeRendererPresent -or $data.LegacyShellPresent -or $data.LoadedMediaAssemblies.Count){throw 'R0 surface/exclusion/lifetime check failed'}
        [pscustomobject]@{Mode=$name;Result='Pass';Samples=$data.Samples;DpiScale=$data.Scale;CaptureKind='WPF render target, not desktop screenshot'}
    }finally{$p.Dispose()}
}
$checks|ConvertTo-Json|Set-Content (Join-Path $root 'checks.json');$checks