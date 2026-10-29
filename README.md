# Tidlet Monitor

An MIT-licensed Windows system monitor designed for a small top-edge island, plain-English readings and low resource use.

**Current status: native M1 shell implemented; final performance verification in progress.** The user approved replacing always-resident WPF rendering after its memory gate failed. The native comparison passed at 30.74 MB peak; the complete Island shell passed at 36.63 MB peak. Remaining style/scenario measurements and physical-hardware acceptance are tracked in [progress](PROGRESS.md).

## Build and test

Prerequisites: Windows 10/11, the exact .NET SDK **8.0.414** pinned by global.json, and PowerShell 7 for scripts. The developer scaffold is framework-dependent and requires the matching .NET 8 Windows Desktop runtime. No admin rights or driver is needed by the app.

Use a local repository directory that accepts writes. Set these environment variables for this terminal only before the following commands:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'

dotnet restore TidletMonitor.sln --locked-mode
dotnet build -c Release
dotnet test -c Release --logger trx --results-directory TestResults
pwsh -NoProfile -File scripts/verify-test-results.ps1
pwsh -NoProfile -File scripts/test-measurement.ps1
```

Tests use the disclosed Microsoft test SDK/framework/adapter only; application projects have no NuGet package references. Exact transitive versions are locked and described in [the inventory](docs/DEPENDENCIES.md). CI rejects zero discovered tests, skipped/incomplete suites and failures. The workflow is configured, but remote CI is not claimed passed until an actual run exists.

```powershell
dotnet run --project src/IsletMonitor -c Release --no-build
```

Use the tray menu to exit. To run the historical M0 scaffold baseline with natural exit:

```powershell
$app = Start-Process -FilePath src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe -ArgumentList '--scaffold','--exit-after-seconds','350' -PassThru
pwsh -NoProfile -File scripts/measure.ps1 -ProcessId $app.Id -Scenario ScaffoldIdle -DurationSeconds 300 -WarmupSeconds 30
```

The measurement script never changes system settings, kills processes, forces garbage collection or trims working sets. Short diagnostic runs require an explicit flag and cannot claim budget acceptance. Scenario names are operator-supplied; hidden/fullscreen behavior needs separate evidence.

## Running the M1 shell

The default app now starts the approved native island, without initializing WPF. Hover to reveal the preview and click to expand. Escape or an outside click collapses the panel. Ctrl+Alt+I toggles it; if that shortcut is already in use, the tray menu remains available.

Right-click the island or its tray icon for Island/Notch/Bar, display selection, Hide/Show and Exit. The app suppresses itself for Windows presentation/fullscreen states, borderless fullscreen, session lock, display-off and suspend. User-hidden mode stops metric, pointer, animation and foreground watches. All settings are temporary in M1; persistence/customization belongs to M3.

```powershell
dotnet run --project src/IsletMonitor -c Release --no-build
```

Developer checks:

```powershell
pwsh -NoProfile -File scripts/smoke-shell.ps1
pwsh -NoProfile -File scripts/smoke-shell.ps1 -IncludeFullscreenFixture
pwsh -NoProfile -File scripts/measure-native-shell.ps1 -Scenario Collapsed -Style Island -OutputDirectory artifacts/measurements/my-shell-trial
```

The optional fullscreen fixture is an owned, visible test window that closes itself; it is not an exclusive D3D game. Each acceptance measurement needs a fresh directory and at least five measured minutes. Do not run overlapping visible trials. Raw CSV, native diagnostics and independently recomputed checks are separate evidence.

The original WPF experiments remain available through --scaffold and --island-probe. --native-probe selects the minimal native comparison host, which differs from the complete shell in resident services. Each probe requires an explicit report path and timed exit. See docs/M1_NATIVE_PLAN.md and PROGRESS.md.


## Architecture

- Islet.Core: portable capabilities, settings contracts, CPU reading states/math and display boundary; no Windows or WPF APIs.
- Islet.Shell: native presentation/lifecycle controller and launch validation; legacy WPF experiments remain isolated.
- Islet.Windows: Win32 layered renderer, CPU provider, display/power/fullscreen/tray/hotkey adapters; no production NuGet dependency.
- IsletMonitor: application entry and composition.
- tests / scripts / docs: genuine tests, verification tooling, plans and evidence.

M1 currently adds only the real CPU feasibility source. No sample history, full widget set, settings persistence or fixers are implemented.

## Performance and compatibility

Targets for later island implementation: collapsed <=60 MB resident working set and <1% average normalized CPU over five minutes; expanded graphs <=100 MB; approximately zero hidden/fullscreen CPU. A plain M0 scaffold baseline is not a transparent-island pass.

See [PROGRESS](PROGRESS.md) for actual commands/results and [PERFORMANCE](docs/PERFORMANCE.md) for definitions. x64/x86 build coverage does not prove native-layout or x86 runtime behavior; M1 tests ran on both x64 and x86, and an x86 live app smoke passed; this does not prove Windows 10 or ARM64 coverage. Physical mixed-DPI monitors, missing/hybrid hardware and 8 GB reference-machine testing remain explicit gaps.

.NET 8 reaches end of support on 10 November 2026. This scaffold deliberately honors the requested target; its installed SDK/runtime are older servicing builds. Any target change or machine installation requires a separate decision. See [Microsoft's lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Privacy and safety

The native shell reads CPU times locally and keeps at most 60 history samples in memory. It writes diagnostics only to an explicitly requested report path. The normal app makes no network requests and writes no persistent settings in M1. The explicit M0 scaffold collects no sensor data. No accounts, telemetry, messaging, cloud sync, background service, driver or phone-home updater. Build/test tooling has separate Microsoft dependencies; its telemetry is opted out through process-local environment settings. No third-party test assets are shipped in the application.

Future monitoring stays local, capabilities explain unavailable values, and tests of process actions/fixers use mock executors. No machine setting has been changed to implement or benchmark this scaffold.

## Roadmap and contribution

Read [AGENTS](AGENTS.md), [the detailed plan](docs/IMPLEMENTATION_PLAN.md), [decisions](DECISIONS.md), [API register](docs/API_REGISTER.md) and [acceptance matrix](docs/ACCEPTANCE_MATRIX.md) before changes. Every milestone requires a build, real tests, 300-second measurement and progress update. The user performs all commits manually; do not stage or commit automatically.

M1 island → M2 metrics/widgets → M3 customization, then stop for review. Helper, diagnostics/fixers, creature and release packaging are later milestones.

Licensed under [MIT](LICENSE).
