# Tidlet Monitor

An MIT-licensed Windows system monitor designed for a small top-edge island, plain-English readings and low resource use.

**Current status: M0 scaffold.** The runnable window is a plain bootstrap; it does not yet contain an island, sensors, diagnostics or fixers. M1–M3 are planned; later milestones remain out of scope.

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

Close the window normally. For an owned benchmark host that exits itself:

```powershell
$app = Start-Process -FilePath src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe -ArgumentList '--exit-after-seconds','350' -PassThru
pwsh -NoProfile -File scripts/measure.ps1 -ProcessId $app.Id -Scenario ScaffoldIdle -DurationSeconds 300 -WarmupSeconds 30
```

The measurement script never changes system settings, kills processes, forces garbage collection or trims working sets. Short diagnostic runs require an explicit flag and cannot claim budget acceptance. Scenario names are operator-supplied; hidden/fullscreen behavior needs separate evidence.

## Architecture

- Islet.Core: portable identity, capability support and initial settings schema contracts; no Windows or WPF APIs.
- Islet.Shell: WPF presentation foundation.
- Islet.Windows: platform boundary, ready for later native implementations.
- IsletMonitor: application entry and composition.
- tests / scripts / docs: genuine tests, verification tooling, plans and evidence.

No sensor implementation, sample history or settings persistence is claimed in M0.

## Performance and compatibility

Targets for later island implementation: collapsed <=60 MB resident working set and <1% average normalized CPU over five minutes; expanded graphs <=100 MB; approximately zero hidden/fullscreen CPU. A plain M0 scaffold baseline is not a transparent-island pass.

See [PROGRESS](PROGRESS.md) for actual commands/results and [PERFORMANCE](docs/PERFORMANCE.md) for definitions. x64/x86 build coverage does not prove native-layout or x86 runtime behavior; native bindings arrive later. Physical mixed-DPI monitors, missing/hybrid hardware and 8 GB reference-machine testing remain explicit gaps.

.NET 8 reaches end of support on 10 November 2026. This scaffold deliberately honors the requested target; its installed SDK/runtime are older servicing builds. Any target change or machine installation requires a separate decision. See [Microsoft's lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Privacy and safety

The M0 application collects no sensor data and makes no network requests. No accounts, telemetry, messaging, cloud sync, background service, driver or phone-home updater. Build/test tooling has separate Microsoft dependencies; its telemetry is opted out through process-local environment settings. No third-party test assets are shipped in the application.

Future monitoring stays local, capabilities explain unavailable values, and tests of process actions/fixers use mock executors. No machine setting has been changed to implement or benchmark this scaffold.

## Roadmap and contribution

Read [AGENTS](AGENTS.md), [the detailed plan](docs/IMPLEMENTATION_PLAN.md), [decisions](DECISIONS.md), [API register](docs/API_REGISTER.md) and [acceptance matrix](docs/ACCEPTANCE_MATRIX.md) before changes. Every milestone requires a build, real tests, 300-second measurement and progress update. The user performs all commits manually; do not stage or commit automatically.

M1 island → M2 metrics/widgets → M3 customization, then stop for review. Helper, diagnostics/fixers, creature and release packaging are later milestones.

Licensed under [MIT](LICENSE).
