#requires -Version 5.1
# Reproduce the approved, isolated framework-dependent x86 feasibility build. No downloads or machine changes.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $env:DOTNET_NOLOGO='true'
    $env:DOTNET_CLI_HOME=Join-Path (Get-Location) 'artifacts/tool-home'
    $env:NUGET_PACKAGES=Join-Path (Get-Location) 'artifacts/packages'
    $env:MSBuildEnableWorkloadResolver='false'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
    $null=New-Item -ItemType Directory -Force artifacts/offline-empty-feed
    $project='src/IsletMonitor/IsletMonitor.csproj'
    $common=@('-p:SelfContained=false','-p:PlatformTarget=x86','-p:MSBuildProjectExtensionsPath=obj/win-x86-probe/',
        '-p:IntermediateOutputPath=obj/win-x86-probe/Release/','-p:NuGetLockFilePath=obj/win-x86-probe/packages.lock.json',
        '-p:EnableAppHostPackDownload=false','-p:EnableTargetingPackDownload=false','-p:EnableRuntimePackDownload=false')
    & dotnet restore $project -r win-x86 --source artifacts/offline-empty-feed --disable-parallel -p:NuGetAudit=false @common
    if($LASTEXITCODE){throw 'Offline restore failed; no package download is authorized.'}
    & dotnet build $project -c Release -r win-x86 --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -p:OutputPath=bin/win-x86-probe/ -p:AppendRuntimeIdentifierToOutputPath=false @common
    if($LASTEXITCODE){throw 'x86 feasibility build failed.'}
    $exe=(Resolve-Path 'src/IsletMonitor/bin/win-x86-probe/IsletMonitor.exe').Path
    $bytes=[IO.File]::ReadAllBytes($exe)
    $header=[BitConverter]::ToInt32($bytes,60)
    if([BitConverter]::ToUInt16($bytes,$header+4) -ne 0x014c -or [BitConverter]::ToUInt16($bytes,$header+24) -ne 0x010b){throw 'Apphost is not PE32 x86.'}
    $text=[Text.Encoding]::UTF8.GetString($bytes)
    if(-not $text.Contains('PerMonitorV2') -or -not $text.Contains('asInvoker')){throw 'Expected apphost manifest text absent.'}
    [pscustomobject]@{Executable=$exe;PE='PE32 x86';DpiManifest='PerMonitorV2';Privileges='asInvoker';ActualProcessBitness='Must be checked by bounded runtime probe'}|ConvertTo-Json
} finally { Pop-Location }
