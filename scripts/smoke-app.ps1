#requires -Version 7.0
[CmdletBinding()]
param([string] $Executable = (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$valid = Start-Process -FilePath $exe -ArgumentList '--exit-after-seconds','1' -PassThru -WindowStyle Hidden
if (-not $valid.WaitForExit(15000)) { throw 'App failed to exit itself in the smoke-test window; no termination attempted.' }
$valid.Refresh()
if ($valid.ExitCode -ne 0) { throw "App startup failed with exit code $($valid.ExitCode)." }
$valid.Dispose()
$invalid = Start-Process -FilePath $exe -ArgumentList '--exit-after-seconds','0' -PassThru -WindowStyle Hidden
if (-not $invalid.WaitForExit(15000)) { throw 'Invalid-argument app did not exit; no termination attempted.' }
$invalid.Refresh()
if ($invalid.ExitCode -ne 2) { throw 'Invalid benchmark duration was not rejected.' }
$invalid.Dispose()
[pscustomobject]@{ Result='Pass'; Cases=2; RealProcessTermination=$false } | ConvertTo-Json

exit 0
