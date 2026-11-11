#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable,[string]$OutputDirectory='artifacts/ui-smoke',[switch]$Startup)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$root = (New-Item -ItemType Directory -Force $OutputDirectory).FullName
$modules = 'Cpu','Ram','Gpu','Network','Disk','Battery','Sensors'
$pages = @('Dashboard','Settings','About','Battery optimization') + @($modules | ForEach-Object { "Page$_" })
if (!$Startup) { $pages += 'Widgets' }
foreach ($theme in 'Dark','Light') {
    foreach ($surface in @('Top','Bottom','Hover') + $modules + $pages) {
        $folder = (New-Item -ItemType Directory -Force (Join-Path $root "$theme/$surface")).FullName
        $settings = Join-Path $folder 'settings.json'
        $report = Join-Path $folder 'probe.json'
        $capture = Join-Path $folder 'bar.png'
        $configuration = @{ Version=1; Theme=$theme; RevealAtTop=($surface -eq 'Hover'); Items=@('Cpu','Gpu','Ram','Network','Battery','Disk','Sensors') }
        if ($Startup) {
            $configuration.IntervalSeconds = 5
            $configuration.Modules = @{}
            foreach ($module in $modules) { $configuration.Modules[$module] = @{ UpdateSeconds=5 } }
        }
        $configuration | ConvertTo-Json | Set-Content -LiteralPath $settings
        $arguments = @('--r1-probe','--exit-after-seconds','3','--settings-file',('"' + $settings + '"'),
            '--probe-report',('"' + $report + '"'),'--capture',('"' + $capture + '"'))
        $popup = $surface -in $modules -or $surface -eq 'Hover'
        $page = $surface -in $pages
        if ($surface -eq 'Bottom') { $arguments += '--bottom' }
        if ($popup) { $arguments += @('--popup-module',$(if ($surface -eq 'Hover') { 'Cpu' } else { $surface })) }
        if ($page) {
            $pageName = $surface -replace '^Page',''
            if ($pageName -in 'Cpu','Ram','Gpu') { $pageName = $pageName.ToUpperInvariant() }
            $arguments += @('--dashboard-page',('"' + $pageName + '"'))
        }
        $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
        try {
            if (!$process.WaitForExit(13000)) { throw "$theme $surface timed out." }
            if ($process.ExitCode -ne 0) { throw "$theme $surface exited $($process.ExitCode)." }
            $probe = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
            if (!$probe.FirstFrame -or $probe.EverHidden -or !$probe.NaturalExit -or !$probe.TimersStopped -or !$probe.AllTrayRemoved -or
                !$probe.DashboardWorkersDrained -or !$probe.PopupWorkersDrained -or !$probe.GpuBarWorkersDrained -or $probe.PointerSize -ne 4) {
                throw "$theme $surface did not complete a visible x86 run with clean shutdown."
            }
            if ($popup -and ($probe.PopupOpens -ne 1 -or $probe.PopupCloses -ne 1 -or $probe.ChartWork -lt 1)) { throw "$theme $surface popup did not render." }
            if ($Startup -and ($probe.Samples -ne 1 -or $probe.ChartWork -gt 1 -or $probe.DashboardFrames -gt 1)) { throw "$theme $surface advanced past startup." }
            if ($page -and ($probe.DashboardOpens -ne 1 -or @($probe.DashboardCaptures).Count -ne 2)) { throw "$theme $surface page did not render." }
            if ($surface -eq 'Widgets' -and ($probe.DashboardFrames -lt 1 -or $probe.DashboardReads -lt 1)) { throw "$theme widgets did not receive a reading." }
            if (!$popup -and ($probe.PopupOpens -ne 0 -or $probe.ChartWork -ne 0 -or $probe.ProcessWork -ne 0)) { throw "$theme $surface performed unexpected popup work." }
            $images = if ($page) { 'bar.dashboard.100.png','bar.dashboard.150.png' } elseif ($popup) { 'bar.popup.100.png','bar.popup.150.png' } else { 'bar.png' }
            foreach ($image in $images) {
                if (!(Test-Path -LiteralPath (Join-Path $folder $image))) { throw "$theme $surface is missing $image." }
            }
            "$theme $surface passed (popup frames $($probe.ChartWork), page frames $($probe.DashboardFrames))."
        } finally { $process.Dispose() }
    }
}
