# Tidlet Monitor

A local, MIT-licensed Windows system monitor with a compact island, notch or bar and a clear, detailed dashboard. Built with C#/.NET 8 and a native Win32 layered renderer, following the approved renderer exception after the original transparent WPF experiment exceeded its memory budget.


The new Notch UI builds with 92 tests passing on x64 and actual x86 runtimes. **The memory gate failed after opening music:** 68.84 MB collapsed versus the 60 MB limit; average CPU was 0.086%. Expanded Notch passed at 71.05 MB / 0.261% CPU. Each complete trial measured at least 300 seconds on eight logical processors. Implementation is stopped for review of the [media memory proposal](docs/MEDIA_MEMORY_PROPOSAL.md); see [raw evidence](docs/evidence/notch-control-center/README.md). Previous dashboard measurements remain historical. The user performs all commits manually.

## The dashboard

Open the island to see processor, memory, network, disk, battery and top-app cards. Select a card or a module tab for its history chart and detail rows, alongside a combined summary. Switch between reference-inspired near-black and white themes with blue/red accents using the top-right button. The information layout is inspired by [Stats](https://github.com/exelban/stats); Tidlet uses its own Windows implementation and branding, with no copied source or artwork.

- CPU: whole-system utilization, idle headroom, up to 60 recent readings.
- Memory: physical RAM used/available/total and utilization history.
- Network: download/upload byte rates and a paired chart from the existing Windows provider.
- Disk: genuine read/write byte rates across physical disks, collected through Windows PDH in one background request at a time.
- Battery: charge level, charging state and estimated remaining time when Windows supplies it. A machine without a battery hides its overview card; its module still explains why.
- Apps: largest accessible process working sets, refreshed at most every four seconds while expanded. No process actions are added by this redesign.
- GPU: explicitly unavailable until a genuine per-adapter counter provider is connected. No generic adapter or fake utilization is shown.

Missing, warming-up and failed readings have explanations; no missing sensor is replaced with zero. Temperatures, fan speeds, clock speed, memory subcategories and battery health/rate are not invented. Expanded-only providers stop sampling when collapsed or hidden. Histories are bounded and reset on resume.

**Known provider gap:** the existing network implementation aggregates active BCL interfaces; required physical-adapter filtering through GetIfTable2 is unfinished, so virtual/VPN interfaces may duplicate traffic. This UI change does not establish full M2/M3 acceptance. Current settings schemas/persistence tests also do not prove a complete settings window or every customization option.

## Notch control center

Expanded Notch has a compact 620x320 DIP home: system music title/player controls, a 25-minute focus timer, live CPU/RAM bars and download/upload rates. Dark mode uses near-black surfaces throughout; white mode and blue/red accents remain available. The Monitor button opens the full overview; CPU/RAM/network and Disk/Apps/Power open their detailed pages. The full Notch header returns home.

Click the focus row to start, pause or resume; the reset arrow restores 25 minutes. Completion appears in the panel. It continues by elapsed time while hidden, without a background ticking loop, and resets when the app exits. No sound or notification is generated. Music controls are disabled when no player exists or the player does not support an action. Only participating Windows media apps can supply track data; there is no built-in music player.

```powershell
cd "C:\Users\bawan\OneDrive\Documents\Tidlet"
& ".\src\IsletMonitor\bin\Release\net8.0-windows\IsletMonitor.exe" --style Notch --state Expanded --theme Dark
```
## Run

Prerequisites: Windows 10/11, SDK **8.0.414** pinned by global.json, matching .NET 8 Windows Desktop runtime, and PowerShell 7 for developer scripts. The app needs no administrator rights or driver. The user-approved Microsoft.Windows.SDK.NET.Ref 10.0.17763.56 targeting pack supplies native Windows media interop; it is pinned for .NET 8. Music controls require Windows 10 1809 or later and a participating player; older Windows 10 receives an explanation. See DECISIONS.md for the SDK license and runtime inventory.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore TidletMonitor.sln --locked-mode
dotnet build -c Release
dotnet run --project src/IsletMonitor -c Release --no-build
```

Hover over the collapsed island, then click to open it. Escape or an outside click collapses it. **Ctrl+Alt+I** toggles the dashboard; the tray menu remains available if the shortcut is occupied. Right-click for module selection, Island/Notch/Bar, display selection, theme, hide/show or exit. Dashboard tabs and cards are clickable; native menus provide the keyboard route.

Fullscreen/presentation, session lock, display-off and suspend suppress the island. Explicitly hiding it stops metric/pointer/animation/foreground work. The complete expanded canvas scales to fit small/high-DPI screens. Two physical monitors at different DPI still require hardware validation.

Existing saved theme/accent preferences are read on normal launches. The dashboard theme button is session-only and does not overwrite settings. Broader settings/profile/alert UI acceptance remains separate.

For a direct dashboard launch:

```powershell
src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe --state Expanded
src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe --page Memory --theme Light
```

## Verify and measure

```powershell
dotnet test -c Release --logger trx --results-directory TestResults
pwsh -NoProfile -File scripts/verify-test-results.ps1
pwsh -NoProfile -File scripts/test-measurement.ps1
pwsh -NoProfile -File scripts/smoke-shell.ps1 -IncludeFullscreenFixture
pwsh -NoProfile -File scripts/smoke-dashboard.ps1
pwsh -NoProfile -File scripts/smoke-control-center.ps1
pwsh -NoProfile -File scripts/measure-native-shell.ps1 -Scenario Collapsed -Style Island -OutputDirectory artifacts/measurements/my-collapsed-trial
pwsh -NoProfile -File scripts/measure-native-shell.ps1 -Scenario Expanded -Style Island -OutputDirectory artifacts/measurements/my-expanded-trial
pwsh -NoProfile -File scripts/measure-native-shell.ps1 -Scenario Hidden -OutputDirectory artifacts/measurements/my-hidden-trial
```

Each measurement needs a fresh directory, 30 seconds of warmup and at least 300 measured seconds. Do not overlap visible trials or interact with the benchmark. The verifier rejects state/style changes, tampered summaries, incomplete observations, loaded WPF assemblies and leaked timers. It never changes system settings, forces garbage collection, trims memory or terminates a process. Test windows exit naturally. The fullscreen fixture is a borderless test window; exclusive games remain untested.

Dashboard smoke captures 16 actual rendered frames (eight pages, two themes) in ignored artifacts, using real data and no personal settings. Frames are the app's own rendered buffer, not a desktop screenshot. Missing-hardware and invalid-value tests use fixtures. Tests of process actions use mock executors.

Budgets remain **60 MB collapsed / 100 MB expanded**, collapsed average CPU **<1% over five minutes**, and approximately zero hidden/fullscreen CPU. See [PROGRESS.md](PROGRESS.md) and [performance definitions](docs/PERFORMANCE.md) for actual results; historic renderer measurements do not validate a newer binary.

## Architecture and scope

- Islet.Core: portable readings/validation, capabilities, presentation models/layout, settings/theme/profile/alert contracts; no Windows/WPF APIs.
- Islet.Shell: native presentation and lifecycle controller; isolated legacy WPF experiments.
- Islet.Windows: native drawing and Windows metrics, tray, display, power, fullscreen and hotkey adapters.
- IsletMonitor: composition and entry point.
- tests / scripts / docs: verification, performance evidence and engineering decisions.

The historical WPF experiments remain behind --scaffold and --island-probe. The minimal --native-probe comparison host differs from the full dashboard and requires a timed exit plus explicit report path. Hosted CI is configured; a passing remote run is not claimed without evidence.

No accounts, telemetry, cloud sync, messaging, phone-home updates, background service or driver. Readings stay local; diagnostics write only to an explicitly requested report path. Normal launches may read/recover existing local settings. No helper, compatibility export, fixers, creature or release packaging is introduced (M4-M7 remain deferred).

.NET 8 support ends **10 November 2026**. The requested target is retained; no machine installation or framework upgrade was performed. See [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

Read [AGENTS.md](AGENTS.md), [the original plan](docs/IMPLEMENTATION_PLAN.md), [UI plan](docs/UI_REDESIGN_PLAN.md), [decisions](DECISIONS.md) and [API register](docs/API_REGISTER.md). The user owns all staging and commits; this work does not perform Git writes.

Licensed under [MIT](LICENSE).