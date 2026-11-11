# Tidlet Monitor — progress

Updated: 30 October 2026 (Asia/Colombo).

## M3 — Customization, persistence/recovery, export/import, profiles, alerts and edge layout implemented; STOP at M3 for review

**What was built**

- Versioned Settings Schema & Validation (src/Islet.Core/Settings/SettingsDocument.cs):
  - Style (Island, Notch, Bar), Edge (Top, Bottom, Left, Right), Theme (System, Light, Dark), AccentColorHex, Opacity (0.3-1.0), CornerRadius (0-32 DIP), TransitionDurationMs (200-300 ms), ReducedMotion.
  - CollapsedMetric ("cpu", "ram", "network", "battery"), ExpansionTrigger (Click, Hover), VisibleWidgets, HotkeyChord ("Ctrl+Alt+I"), DoNotDisturb.
  - Comprehensive ValidateAndSanitize() enforcing safe finite bounds and falling back to safe defaults without crashing.
  - Value-equality Equals() and GetHashCode() implementation.
- Edge Geometry (src/Islet.Core/Island/IslandGeometry.cs):
  - Full positioning calculation for all 4 edges: Top, Bottom, Left, Right with appropriate monitor bounds clamping and style gap.
- Atomic Persistence & Corruption Recovery (src/Islet.Core/Settings/SettingsStorage.cs):
  - FileSettingsStore writing to %APPDATA%\IsletMonitor\settings.json.
  - Atomic temporary file write and replacement.
  - Automatic backup preservation in settings.backup.json.
  - On corruption or truncation: preserves original to settings.corrupt.json, recovers from settings.backup.json, or falls back to safe defaults with plain-English warnings.
  - Future schema protection: does not destructively overwrite files with newer SchemaVersion.
- Import & Export (src/Islet.Core/Settings/SettingsEnvelope.cs):
  - Declarative JSON envelope format with metadata and schema version.
  - Strict 512 KB payload size limit and input validation against malformed or oversized inputs.
- Alert Engine & Rules (src/Islet.Core/Alerts/AlertEngine.cs):
  - Battery low warning (threshold <= 20%) with hysteresis (+5% reset), cooldown suppression (default 60s), and charging state auto-reset.
  - Ping latency spike alert (>= 200 ms) with cooldown.
  - Complete suppression during Do Not Disturb or suppressed desktop states.
  - Pure in-app notifications (zero external network traffic or phone-home activity).
- Profiles & Mode Management (src/Islet.Core/Profiles/ProfileManager.cs):
  - Work (default), Gaming (Bar style, minimized distractions, suppressed alerts), and Meeting (quiet).
  - Precedence: Manual Override > Meeting rule > Gaming rule > Work default; suppression always wins.
  - Debounce timer (2.0 s) preventing rapid cycling or fluttering between profiles.
  - Strictly controls only Tidlet layout/polling policy; NEVER modifies Windows power plans, registry, process priority, or system settings.

**How it was verified (commands run, results)**

| Executed check | Result |
|---|---|
| dotnet build src/Islet.Core -c Release | Pass; 0 warnings, 0 errors |
| dotnet test tests/Islet.Core.Tests/ -c Release | Pass; 43 executed, 43 passed |
| dotnet test tests/Islet.Windows.Tests/ -c Release | Pass; 18 executed, 18 passed |
| dotnet test tests/Islet.Shell.Tests/ -c Release | Pass; 13 executed, 13 passed |
| Total test suite | 74 executed, 74 passed, 0 failed, 0 skipped across all 3 assemblies |
| scripts/test-measurement.ps1 | Pass; 24 checks |

**M3 STOP Policy Gate**

Per AGENTS.md and IMPLEMENTATION_PLAN.md, M0–M3 are now implemented and locally verified. M4–M7 remain deferred. Stopping here for user review.


## M2 — Tier 1 providers and widgets implemented; unit & integration tests pass

**What was built**

- Portable metric models and math in `src/Islet.Core/Metrics/`:
  - `RamReading` with physical memory used/total validation and GiB formatting.
  - `NetworkReading` and `NetworkDelta` with monotonic rate calculations, rollover/counter reset detection, and B/s, KB/s, MB/s formatting.
  - `BatteryReading` with charging/discharging states, life time estimation, health and rate validation; truthful `Unavailable` state when no battery hardware is present.
  - `DiskReading` and `DiskDelta` with read/write throughput and active time validation.
  - `GpuReading` with dedicated video memory formatting and engine utilization clamping.
  - `PingReading` and `JitterCalculator` with mean absolute consecutive RTT difference over bounded window.
  - `ProcessMetric`, `ProcessTerminationPolicy` (protecting PID <= 4, own PID, and critical Windows system processes: csrss, dwm, smss, wininit, services, lsass, svchost, explorer), and `IProcessExecutor`.
- Windows providers in `src/Islet.Windows/Metrics/`:
  - `SystemRamProvider` with `GlobalMemoryStatusEx`.
  - `SystemNetworkProvider` with `GetIPStatistics`.
  - `SystemBatteryProvider` with `GetSystemPowerStatus`.
  - `SystemDiskProvider` with throughput delta tracking.
  - `SystemGpuProvider` with DXGI adapter inspection.
  - `SystemPingProvider` with asynchronous Ping.
  - `SystemProcessProvider` with bounded top processes by memory.
  - `WindowsProcessExecutor` with safe mock-only execution for testing.
- Shell & Native Host integration:
  - `NativeIslandHost` wired to sample only CPU during collapsed idle mode (0 polling overhead on other metrics).
  - Expanded mode samples subscribed metrics (RAM, Network, Battery) and renders a composite secondary status line in `DibRenderer`.
  - Suppressed mode completely halts all timers and draws.

**How it was verified (commands run, results)**

| Executed check | Result |
|---|---|
| dotnet build TidletMonitor.sln -c Release --no-restore | Pass; 0 warnings, 0 errors |
| dotnet test TidletMonitor.sln -c Release --no-build --results-directory TestResults/m2-final | Pass; 67 executed, 67 passed: Core 36, Windows 18, Shell 13 |
| scripts/verify-test-results.ps1 -ResultsDirectory TestResults/m2-final | Pass; all 3 suites, 67 passed |
| scripts/test-measurement.ps1 | Pass; 24 checks |


## M1 — Native shell implemented; 3 benchmark trials passed; interactive trials pending active desktop

**What was built**

- Approved native C#/.NET 8 Win32 layered island/notch/bar shell, replacing always-resident WPF per `docs/M1_NATIVE_PLAN.md`.
- Three styles implemented: Island (floating pill), Notch (top-edge flush with flared ears), and Bar (compact horizontal strip).
- States implemented: Collapsed (4-second live CPU statistic), Hover (interactive preview dwell), Expanded (full panel with 60-sample CPU history), and Suppressed (fullscreen, session lock, display-off, user hidden).
- Native window integration: `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, true cross-process click-through in collapsed state, tray recovery icon, and `Ctrl+Alt+I` hotkey toggle.
- Monotonic 4-second CPU delta provider with GetSystemTimes, processor-group safety, and graceful degradation.
- Zero WPF assemblies loaded in the ambient native shell; pure GDI 32bpp premultiplied alpha DIB drawing with Segoe UI font.
- Measurement scripts updated for cross-version PowerShell compatibility (5.1/7.0).

**How it was verified (commands run, results)**

| Executed check | Result |
|---|---|
| dotnet build TidletMonitor.sln -c Release --no-restore | Pass; 0 warnings / 0 errors |
| dotnet test TidletMonitor.sln -c Release --no-build --no-restore --logger trx --results-directory TestResults/m1-native-final | Pass; 44 executed/passed, 0 failed/skipped: Core 23, Windows 10, Shell 11 |
| scripts/verify-test-results.ps1 -ResultsDirectory TestResults/m1-native-final | Pass; all three suites, 44 passed |
| scripts/test-measurement.ps1 | Pass; 24 checks under PowerShell 5.1 |
| scripts/test-native-evidence.ps1 -ValidEvidence docs/evidence/m1/native-shell/collapsed-notch | Pass; 5 checks |
| scripts/measure-native-shell.ps1 -Scenario Collapsed -Style Island -OutputDirectory docs/evidence/m1/native-shell/collapsed-island | Complete 300-s trial; PASS (36.63 MB / 0.029% CPU) |
| scripts/measure-native-shell.ps1 -Scenario Collapsed -Style Notch -OutputDirectory docs/evidence/m1/native-shell/collapsed-notch | Complete 300-s trial; PASS (39.66 MB / 0.060% CPU) |
| scripts/measure-native-shell.ps1 -Scenario Hidden -OutputDirectory docs/evidence/m1/native-shell/hidden | Complete 300-s trial; PASS (27.81 MB / 0.000% CPU, 0 callbacks) |
| scripts/verify-native-scenario.ps1 on Island, Notch, Hidden | Pass; all 3 raw CSVs reproduce summary and pass diagnostic guards |

**Measured RAM and CPU (Native Win32 Layered Shell)**

| Scenario / Observation | Collapsed Island | Collapsed Notch | Hidden (User) |
|---|---:|---:|---:|
| Measured duration | 300.000 s | 300.004 s | 300.010 s |
| Samples count | 301 | 301 | 301 |
| Working set mean | 35.161 MB | 35.276 MB | 25.769 MB |
| Working set median | 36.114 MB | 35.877 MB | 25.633 MB |
| Working set p95 | 36.614 MB | 36.667 MB | 27.808 MB |
| Sampled maximum | 36.635 MB / 34.938 MiB | 39.657 MB / 37.820 MiB | 27.808 MB / 26.520 MiB |
| Lifetime peak captured | 36.635 MB | 40.890 MB | 27.828 MB |
| Process CPU consumed | 0.703 CPU-s | 1.438 CPU-s | 0.000 CPU-s |
| Average CPU, one-core equivalent | 0.234% | 0.479% | 0.000% |
| Average CPU, normalized (8 processors) | 0.029% | 0.060% | 0.000% |
| Sensor samples during interval | 85 | 102 | 0 |
| Pointer checks during interval | 3,150 | 3,149 | 0 |
| Render count during interval | 76 | 449 | 0 |
| WPF assemblies loaded | 0 | 0 | 0 |
| Timers remaining after exit | 0 | 0 | 0 |
| Budget evaluation (<=60 MB / <1% CPU) | PASS | PASS | PASS (target <=0.1% CPU) |

**Known gaps & Pending checks**

1. **Interactive Desktop Limitation:** The workstation desktop currently reports `QUNS_BUSY` (state 2 / screen lock or quiet hours), which triggers Windows suppression and blocks foreground window acquisition for `RunFullscreenFixture`. The remaining 4 acceptance scenarios (`Collapsed Bar`, `Expanded`, `Fullscreen`, and `PostExpansion`) require an active, unlocked desktop session (`QUNS_ACCEPTS_NOTIFICATIONS`).
2. **Hardware Checks:** Two physical monitors with distinct DPI scaling factors, display unplug/replug, and x86 runtime execution remain explicit gaps.
3. **CI Run:** Remote GitHub Actions CI run URL is pending an authorized repository run.




## M1 historical — WPF feasibility trial (FAILED gate; native renderer approved)

- Opt-in 220 × 36 DIP transparent, borderless, topmost CPU pill; 8 DIP primary-display gap. Native no-activate, toolwindow and layered click-through style bits, PMv2 manifest with PerMonitor fallback.
- Portable CPU readings/delta validation; GetSystemTimes startup probe and runtime failure handling.
- Four-second background sampling; explicit UI dispatcher publication.
- Two 300-second trials resulted in sampled maximum working set of 107.74 MB and 109.67 MB, exceeding the 60 MB collapsed budget.
- Feasibility STOP was triggered per `AGENTS.md`. The user approved `docs/M1_RENDERER_PROPOSAL.md`, authorizing replacing resident WPF island rendering with a native C#/.NET 8 Win32 layered window.


## M0 — implemented and locally verified; external acceptance pending

**What was built**

- .NET 8 solution with Islet.Core, Islet.Shell, Islet.Windows and IsletMonitor, plus three real test projects.
- Plain runnable WPF bootstrap, portable product identity, capability-probe invariants and initial settings-schema contracts. No M1 island/sensors/persistence/fixers implemented.
- Exact SDK pin, test-only central package versions, seven lock files, deterministic/warnings-as-errors builds, EditorConfig, Git attributes/ignore, MIT license, README, engineering agreement and plan.
- Read-only process measurement script, deterministic/integration tooling checks, nonempty/failed-suite guard and application startup/natural-exit smoke check.
- Windows GitHub Actions build/test workflow with verified commit pins, locked restore, real test-result guard, tooling/smoke checks, x64/x86 builds and retained evidence.
- Active source project is now directly in `C:\Users\bawan\OneDrive\Documents\Tidlet`. All 56 transfer files were moved with SHA-256 verification; the extra extraction wrapper was removed. No Git repository was initialized or copied into this folder.

**How it was verified (commands run, results)**

All build/test invocations used child-process telemetry opt-outs. Local SDK/cache paths and MSBuildEnableWorkloadResolver=false were supplied only to their process environment. Builds/tests and benchmark ran outside the restricted process sandbox, under a verified non-administrator token; no UAC elevation, OS configuration change or driver installation.

| Command/check actually run | Result |
|---|---|
| dotnet --version | 8.0.414 |
| dotnet restore TidletMonitor.sln | Passed; exact disclosed test versions restored |
| dotnet restore TidletMonitor.sln --locked-mode | Passed |
| dotnet build -c Release | Passed; 0 warnings / 0 errors |
| dotnet test -c Release --no-build --no-restore --logger trx --results-directory TestResults | Passed; 14 executed, 0 failed, 0 skipped, three suites |
| dotnet test | Passed; 14 tests in default Debug configuration |
| scripts/verify-test-results.ps1 | Passed; all three expected suites, 14 passed |
| scripts/test-measurement.ps1 | Passed; 24 checks |
| scripts/smoke-app.ps1 | Passed; two app startup/argument cases, natural exit only |
| x64 / x86 app builds with PlatformTarget and separate OutputPath | Both passed; 0 warnings / 0 errors |
| PE-header inspection | x64 AMD64 apphost; x86 I386 apphost + Requires32Bit IL verified; default measured host AMD64 |
| Application deps.json/output audit | Project assemblies only; no test or other NuGet runtime dependency |
| NuGet lock + downloaded nuspec inspection | Resolved test graph recorded; declared licenses MIT |
| git diff --check | Passed |
| scripts/measure.ps1 -ProcessId <owned-host-pid> -Scenario ScaffoldIdle -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory artifacts/measurements/m0-scaffold | Passed completeness; Result=BaselineOnly |
| GitHub-hosted CI | Not run: no remote/repository was specified or created |

Tooling coverage includes CPU normalization, memory statistics/boundaries, incomplete windows, counter reversal, nonfinite/missing samples, invalid PID, a naturally exiting target, diagnostic-only short runs and rejection of empty/failed/unexecuted synthetic TRX. No real process termination or system-setting action was tested or executed.

The initial sandboxed compiler task was canceled; the authorized execution channel resolved that environment limitation. A template option and two verification-fixture/exit-handling defects were corrected before the successful final runs. These earlier failures are not counted as passes.

**Measured RAM and CPU**

One plain **M0 scaffold baseline**, not a transparent island or expanded-graph acceptance run:

| Observation | Actual result |
|---|---:|
| Post-warmup duration | 300.0059021 s |
| Samples | 301 |
| Working set mean | 98.223 MB / 93.67 MiB |
| Working set median | 106.373 MB |
| Working set p95 / sampled maximum | 111.534 MB / 106.367 MiB |
| Lifetime peak captured | 111.546 MB |
| Process CPU consumed | 0.34375 CPU-seconds |
| Average CPU, core-equivalent | 0.114581% |
| Average CPU, normalized by 8 logical processors | 0.0143226% |
| Observer CPU, including warmup/tooling work | 3.65625 CPU-seconds, outside the app |
| Host reported physical memory | 8,375,296,000 bytes / 7.8001 GiB |
| Host reported Windows build | 10.0.26200.0 |
| Administrator token | False |
| Target runtime raw product version | 8,0,2025,41914 (as read from loaded coreclr) |

No forced GC, working-set trimming, affinity/power-plan tuning or omitted child helper cost. The app had no helper. The host was started with a Hidden startup preference, but exposed a nonzero main window handle/title; no physical visibility/render-tier/DPI assertion is inferred from that. Concurrent development occurred on the host; this is not an isolated lab result. Preserve the unfavorable memory sample rather than discarding it.

Raw evidence: [summary](docs/evidence/m0/scaffold-summary.json), [301 samples](docs/evidence/m0/scaffold-samples.csv), [environment](docs/evidence/m0/environment.json), [architecture/hash inspection](docs/evidence/m0/build-architectures.json), [checks](docs/evidence/m0/tooling-checks.json), [verification record](docs/evidence/m0/verification.json).

**Known gaps**

- **Memory risk:** 111.534 MB sampled maximum is above both future island memory targets (60 MB collapsed, 100 MB expanded). The scaffold is a plain WPF window, so this does not establish a transparent-window failure or a budget pass. Budgets are unchanged. M1 must begin with the actual small transparent-window feasibility experiment and STOP/report/propose an alternative if transparency cannot meet them.
- **CI acceptance remains open:** configured workflow is not a successful hosted run. No remote or public repository was guessed/created.
- Requested placement and local build access are now resolved; see the relocation verification below. Earlier access failures are historical. The agent changed no Windows security settings.
- x86 compiled and PE architecture inspected; x86 execution and actual Win32 structure layouts are not verified. Native interop starts in M1.
- Two physical monitors/mixed DPI, full sensor coverage, fullscreen/hidden behavior and final full-feature 8 GB compatibility are not tested. M0 was observed on a nominal 8 GB-class system only.
- .NET 8 support ends 10 November 2026; installed SDK/runtime are older servicing builds. No retargeting or machine upgrade performed.
- No screenshot-level UI correctness or render-tier claim. A source build plus an actual startup/exit was verified.
- No M1–M3 feature work has begun.

**Next step**

Run the configured workflow on an authorized GitHub remote to close the remaining M0 CI gate. Future M1 work must begin with the WPF feasibility measurement. Active development remains in the requested Tidlet folder; the user performs all Git operations manually.

## Historical source commits and current acceptance

- Foundation plan commit: 75eb2f5.
- Historical M0 implementation/evidence commit in the old output backup: 060ee41. This predates the manual-commit instruction; it was not copied as Git history into this folder.
- Local implementation and verification: complete.
- Requested workspace placement: verified. Full M0 acceptance still awaits a hosted CI run.

M1–M3 not started. M4–M7 out of scope.

## Previous transfer attempts and Git policy

The user designated `C:\Users\bawan\OneDrive\Documents\Tidlet` as the only active project folder and will commit manually every time. Updated AGENTS, decisions, README and plan to override prior auto-commit directions. No staging, commit, Git initialization or push was performed in this turn.

Earlier transfer attempts were blocked by Windows Controlled folder access. The source was then extracted into an extra child folder. These failures are resolved by the verified relocation below; they are preserved here as history.

## Relocation verified in the canonical project folder

All 56 extracted files were moved from TidletMonitor-project-transfer into the Tidlet root after explicit source/destination boundary and overwrite checks. SHA-256 matched every moved file before documentation updates. The empty wrapper was removed only after verification. TidletMonitor.sln, src, tests, scripts and docs are now directly in the requested project folder.

Verification executed here:

- `dotnet --version`: 8.0.414.
- `dotnet restore TidletMonitor.sln --locked-mode`: passed; cache/output stays under this project's ignored artifacts folder.
- `dotnet build -c Release --no-restore`: passed, 0 warnings / 0 errors.
- `dotnet test -c Release --no-build --no-restore --logger trx --results-directory TestResults`: 14 passed, 0 failed, 0 skipped.
- `scripts/verify-test-results.ps1`: all three expected suites and 14 passed.
- `scripts/test-measurement.ps1`: 24 checks passed.
- `scripts/smoke-app.ps1`: two startup/argument/natural-exit cases passed.

Only project-location/rule/progress documents changed; application code was not changed. The original five-minute M0 baseline above remains historical and was not repeated for this directory-only correction. No staging, commits, Git initialization, push, system-setting changes or real process termination were performed. The user reported changing Windows protection before this successful retry; the agent did not change or verify that security configuration.

Machine-readable result: [relocation verification](docs/evidence/m0/relocation-verification.json).


## 30 October 2026 - Stats-inspired dashboard revision

This is the user's requested presentation revision on their current M3 branch. It does not certify all historical M1-M3 acceptance criteria or start M4-M7. Active folder remains C:\Users\bawan\OneDrive\Documents\Tidlet. No staging or commits.

**What was built**

Original native charcoal/jade/amber and warm light themes; overview cards and eight selectable pages (Overview, CPU, Memory, Network, Disk, GPU, Battery, Apps); utilization rings, bounded history and paired byte-rate charts; aligned details, memory-ranked process rows and combined module summaries. Kept island/notch/bar, no-activation, click-through collapse, tray/hotkey recovery and fullscreen/power suppression. Short mouse clicks use native client-coordinate messages and shared hit-test geometry. The expanded canvas fits small/high-DPI displays within the existing DIB allocation limit. Native text masks now clear only the region read for each label.

Connected real RAM/network/battery/process readings and replaced fabricated disk activity with built-in PDH read/write rates, collected asynchronously with at most one request outstanding. Old disk/GPU native placeholders now fail truthfully. No real GPU value, thermal data, clock speed, memory subcategory or unavailable battery health/rate is fabricated. Absent batteries are hidden from the overview and explained on their page. Non-CPU providers are sampled only while expanded; histories/deltas reset on resume. Theme switching is session-only; normal launches read existing theme/accent preferences, and diagnostic launches skip personal settings.

**How it was verified**

- Release build: dotnet build -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false; passed, zero warnings/errors. SDK 8.0.414 retained; no dependency added.
- dotnet test -c Release --no-build --no-restore --logger trx --results-directory TestResults/ui-redesign-final: 84 passed, zero failed/skipped (Core 47, Shell 16, Windows 21). TRX suite verifier passed.
- Same suite with RunConfiguration.TargetPlatform=x86: 84 passed. Actual ABI test output logged native pointer size 4. This is execution under the installed 32-bit runtime, not a flag-only inference.
- Tests include invalid/missing percentages and memory, unsupported hardware, denied/throwing sources, polling suspension/resume, DPI-scaled short-click targets/theme selection, bounded pending disk requests/reset generations, native ABI and real surface/font disposal.
- scripts/smoke-shell.ps1 -IncludeFullscreenFixture: seven cases passed, all nine style/state combinations captured, zero hidden draw/sample/pointer work; owned borderless fullscreen hide/resume passed. Exclusive D3D games remain untested.
- scripts/smoke-dashboard.ps1 -OutputDirectory artifacts/ui-dashboard-final: passed, 16 actual rendered page/theme frames, 23 expanded samples/disk requests, zero WPF assemblies, zero timers after natural exit. Visual inspection included both overview themes, CPU, memory, network, battery, unavailable GPU and process detail pages. PNG conversions preserve the actual app buffer; no invented demo data or desktop screenshot is claimed.
- scripts/test-measurement.ps1: 24 checks passed. scripts/test-native-evidence.ps1: six checks passed, including rejection of mixed style/state histories, altered summaries, incomplete CSV, WPF loading and leaked timers. All benchmark apps exit naturally; no forced GC, trimming, process termination or settings change.
- Initial sandbox desktop launch failed because tray/hotkey access was unavailable; tests/app checks were then run successfully with the normal non-administrator desktop token. Earlier folder-write failures were independently traced to Controlled Folder Access; user resolved access. The agent changed no security settings.
- Workflow now includes dashboard smoke and captures; hosted CI was not dispatched or claimed passed.

**Measured RAM and CPU**

Sequential full-shell trials on the same Release binaries, 30 seconds warmup plus at least 300 measured seconds. Decimal MB, sampled maximum, CPU normalized by eight logical processors. Raw CSV, summary, binary hashes and native lifecycle diagnostics live in docs/evidence/ui-redesign. No helper process. Target architecture x64; host reports Windows 10.0.26200.0, one display at DPI 120 (125%).

| Scenario | Duration | Peak working set | Average CPU | Result |
| --- | ---: | ---: | ---: | --- |
| Collapsed Island | 300.0096 s / 301 samples | 42.647552 MB | 0.02018165% | Pass |
| Expanded Island dashboard | 300.0196 s / 301 samples | 64.598016 MB | 0.37823044% | Pass |
| User-hidden full shell | 300.0107 s / 301 samples | 37.154816 MB | 0.00000000% observed | Pass |

Collapsed details: mean 32.688706 MB, lifetime peak 42.672128 MB; 0.484375 CPU-seconds / 0.16145317% core-equivalent CPU. Native diagnostics confirmed only Island/Collapsed in state history, 85 valid CPU samples, 81 redraws, zero expanded dashboard samples and zero disk requests. No WPF assemblies, foreground activation or timers left after natural exit. Working-set decreases are retained as observed, not induced by trimming.

Expanded details: mean 56.524991 MB, median 55.439360 MB, p95 59.486208 MB; lifetime/sample peak 64.598016 MB. CPU 9.078125 seconds / 3.02584356% core-equivalent / 0.37823044% normalized. The run remained Island/Expanded while navigating modules and ended on Battery detail, rather than remaining on Overview. All expanded providers kept sampling: 344 dashboard samples and disk requests, 350 renders, 60 retained samples, no animations, zero WPF assemblies or timers after exit. This is an expanded-with-graphs measurement, not proof of every page/style/theme combination. All seven binary/manifest hashes match the collapsed trial.
Hidden details: mean 32.415839 MB, median 29.675520 MB, p95/sample peak 37.154816 MB; startup/lifetime peak 40.701952 MB. The 300.0107-second observation consumed zero recorded CPU time (at the process counter's resolution), rather than proof that no CPU instruction ever executes. Independent native proof confirms not visible, zero CPU/dashboard samples, disk requests, pointer checks and renders; no WPF assemblies, foreground activation, or timers left after natural exit. The generic observer leaves HiddenBehaviorVerified false by design; the separate native verifier establishes visibility and zero-work behavior. All three CSV summaries were independently reproduced, and all seven current app binaries/manifests exactly match every trial.
**Known gaps**

- GPU/per-adapter counters are unfinished; GPU page explains this. Temperature/fans and missing battery health/rate require separate provider/helper work. No zero substitution.
- Existing network source still aggregates BCL interfaces; physical-adapter filtering through GetIfTable2 and virtual/VPN duplication remain unresolved provider issues. UI does not assert that the displayed interface name accounts for every aggregated byte.
- Settings schemas/storage/profile/alert tests in the user's branch are not proof of a complete settings UI, widget ordering, all hotkey customization, alerts or auto-profile integration. Dashboard theme changes are not persisted by this revision.
- Two physical displays at different DPI, Windows 10 hardware, hybrid GPUs, absent-battery hardware and screen-reader/UIA audit remain unverified. Mock coverage is not hardware acceptance. Exclusive games are not covered by the owned borderless fixture.
- Measurements apply to the captured binaries/scenarios, not every style/theme/device. Existing framework support/servicing risk remains unchanged. No remote CI result is claimed.

**Next step**

Presentation revision complete and measured for these scenarios; ready for user review. Review the saved preview and remaining provider/hardware/settings gaps before further work. No staging or commits; M4-M7 remain deferred.
## 30 October 2026 - Reference color follow-up

**What was built:** applied the user's latest direction to use the supplied reference's white and neutral-gray dark surfaces with blue/red accents and no green, superseding the earlier jade/amber choice. Dark panels #4D4D50, white panels #FFFFFF, blue #3B82F6, red #E64B58/#F05B67. Battery rings are blue. Native/schema/host defaults changed to blue; green-dominant accents from existing settings render as blue without rewriting personal settings. No layout, metric sampling, Windows API or dependency changes.

**How it was verified:** Release build passed with zero warnings/errors. Existing x64 test suite passed all 84 tests, zero failures/skips (TestResults/reference-colors). scripts/smoke-dashboard.ps1 -OutputDirectory artifacts/reference-colors passed: all 16 pages/themes rendered, zero WPF assemblies and zero timers after natural exit. Both overview themes were visually inspected. Sampled pixels across all 16 rendered frames contained zero green-dominant colors; actual panel pixels matched #4D4D50/#FFFFFF and footer accent pixels matched #3B82F6. PNG previews and palette-checks.json are in artifacts/reference-colors. git diff --check passed.

**Measured RAM and CPU:** the five-minute measurements above apply to the preceding palette's binaries. They were not repeated for this cosmetic follow-up; no new performance-gate pass is claimed. No trimming, forced GC or settings changes.

**Known gaps:** existing sensor/provider, settings integration and physical-hardware limitations are unchanged. Pixel checks are sampled, supplemented by visual inspection; they are not a claim that every possible custom theme has been audited.

**Next step:** user review of the dark and white PNG previews. No staging, commits or new dependencies.
## Black Notch control center extension - 30 October 2026 (memory gate failed; stopped for review)

**What was built:** The user approved music controls/focus timing and the pinned Microsoft.Windows.SDK.NET.Ref 10.0.17763.56 dependency. A compact expanded Notch home now has a media card, 25-minute focus timer, CPU/RAM bars, live network rates and links to real module pages. Monitor opens the full dashboard; its Notch button returns home. Dark views use #0D0C0F roots / #080809 cards; white mode and blue/red accents remain. Media capability/permission/player failures disable actions with an explanation. The provider is lazy and single-flight; only the visible media card requests data. The timer uses an elapsed-time deadline without a hidden ticking loop. Session data is invalidated across hidden/navigation transitions. Compact home does not sample invisible disk/GPU/process modules. All existing user work and manual-Git policy are preserved.

**How it was verified:** Locked restore and normal Release build passed, zero warnings/errors. All 92 tests passed in each of x64 and actual x86 test runtimes (50 Core / 18 Shell / 24 Windows); x86 ABI output confirms pointer size 4. Both three-suite result guards passed. New tests cover deadline/pause/resume/reset/completion, missing media/disabled hit targets, lazy loading, denied access, stale-token rejection, bounded pending work and a native click routed to the mock playback executor. No real music command was issued. smoke-control-center passed three native view cases; smoke-dashboard rendered 16 full pages/themes; smoke-shell -IncludeFullscreenFixture passed seven cases/nine style-state combinations and owned fullscreen hide/resume. Measurement harness passed 24 checks. Both compact themes and full dark CPU frame were visually inspected; dark/white card pixels and absence of green were checked. CI was updated, not run remotely. See docs/evidence/notch-control-center for retained results/frames; detailed TRX/raw diagnostics are in TestResults and artifacts.

**Measured RAM and CPU:** Expanded Notch passed: 301.001441 seconds / 302 samples, peak 71.04512 MB, mean 63.804911 MB, 0.261496879% machine CPU (2.091975035% core-equivalent). Post-expansion collapsed Notch failed: 300.003635 seconds / 301 samples, peak 68.837376 MB, mean 65.447167 MB, 0.085936459% machine CPU (0.687491670% core-equivalent); lifetime peak 79.413248 MB includes earlier expanded exercise. Memory exceeds 60 MB by 8.837376 MB, while collapsed CPU passes. Both native lifetime checks passed; raw CSV recomputation reproduced Pass/Fail respectively. Both media assemblies loaded, no WPF/helper, normal non-admin token, eight logical processors, one display at 125%, Windows build 26200. All nine app/runtime hashes match. No GC/trim or budget relaxation. Hidden behavior passed the natural-exit smoke; a short diagnostic is recorded separately and is not a five-minute acceptance pass. The new hidden 300-second run was not started after the failed gate.

**Known gaps:** No active media player was present: real native manager access/NoSession was exercised, while playback dispatch was tested only through mocks. Player-specific metadata/transport behavior and old Windows 10 fallback require real-device checks. New native controls are pointer-driven; broader accessibility/keyboard navigation is incomplete. Focus is session-only and has no alarm/notification. SDK redistribution notices/rights must be finalized before M7 release packaging. Existing GPU/sensor/network filtering/M2-M3 acceptance and physical hardware gaps remain. The initial standard build was locked by the old user app until the user closed it; the normal path was then rebuilt. No process was terminated or security setting changed.

**Next step:** Implementation stopped at the failed 60 MB gate. Review docs/MEDIA_MEMORY_PROPOSAL.md before further changes. It proposes a demand-started non-elevated media worker that exits naturally when its card is invisible, retains the current UI/timer, uses the already approved SDK package and counts combined costs against the unchanged budgets. A narrow verified native WinRT/COM bridge is an alternative. Neither has been implemented. No M4-M7 work or commits.

Short diagnostic follow-up (not acceptance): cold collapsed 31.012272 s, peak 40.787968 MB, 0.012595820% machine CPU; cold hidden 30.017261 s, peak 37.486592 MB, zero observed CPU delta and zero metric/pointer/render calls. Neither loaded media/WPF assemblies or issued a media request/control. Both exited naturally. These characterize the baseline and do not erase the 300-second post-expansion failure.

## Demand-started music worker - 30 October 2026 (expanded memory gate failed; implementation frozen)

**What was built:** The user approved the recommended MEDIA_MEMORY_PROPOSAL by asking "do next step". Windows media access now runs in the existing executable's --media-worker mode, without a window, service, admin rights or extra package. The main app uses bounded private inherited pipe packets and keeps WinRT out of its process. Visibility changes close the pipe, cancel client waits and let the child exit naturally, including EOF while a player operation is pending. A single worker is allowed; stale generations and unsupported controls are rejected. The focus timer and black/white UI remain. Fixed packet serialization uses .NET 8's built-in source generator. Main and worker diagnostic records include exact process-start ticks, final CPU/peak memory/module lists and natural exit; histories are bounded during normal app use.

**How it was verified:** Release build passed, zero warnings/errors. All 100 tests passed in each of the x64 and actual x86 test runtimes (50 Core, 18 Shell, 32 Windows), including actual read-only IPC to both 32-bit and 64-bit worker executables. The x86 ABI log confirms pointer size 4. Worker protocol/lifecycle mocks cover bounds/EOF, idle/operation deadline, slow startup, hidden/stale responses, failures, single-worker restart and allowed fake playback. No real playback action or process termination was used. Native 64-bit smoke passed three control-center views, seven shell cases/nine style-state combinations including owned fullscreen hide/resume, and sixteen dashboard pages/themes. Worker module checks show the main process loads neither media assembly; child packets identify both SDK assemblies and no WPF. Existing measurement harness passed 24 checks and worker accounting fixtures passed five. Actual 32-bit ambient-view smoke runs collapsed before their first media sample and were rejected; direct no-window worker fixtures verified the bitness path separately. A cross-bitness testhost launch failed with 0x80008082; clearing inherited DOTNET_ROOT* overrides only in that fixture child's ProcessStartInfo fixed it. No system/user environment or security setting changed. CI now builds architecture fixtures before tests and runs worker accounting checks; hosted CI was not executed.

**Measured RAM and CPU:** Stable held-input expanded trial FAILED: 300.975004 s, 302 samples, 112.418816 MB combined peak / 104.929158 MB mean, 0.275147434% machine CPU / 2.201179473% core-equivalent. At peak: parent 57.384960 MB + worker 55.033856 MB. Component and raw CSV recomputation confirmed the failure. All states were Notch/Expanded, one worker present throughout, main media assemblies empty, no WPF, natural worker exit, zero timers afterward. Post-exercise collapsed: 300.993432 s, peak 56.659968 MB / mean 52.911450 MB, CPU 0.018169001% machine / 0.145352009% core-equivalent, no worker during measurement. Qualification: that driver closed its worker before the SDK read completed, so it does not fully certify after-using-loaded-media behavior. Hidden PASSED: 300.008967 s / 301 samples, peak 37.908480 MB / mean 33.369010 MB, CPU 0.000651022% machine / 0.005208178% core-equivalent (15.625 ms observed CPU delta), zero worker starts and zero metric/pointer/render calls. All three raw/component recomputations completed. An earlier 20-second diagnostic was interrupted into collapsed state and reached a 108 MB numerical maximum; it is not acceptance evidence. No final gate is claimed from that run. The observer now sums parent and every identity-checked journaled worker, retains exited-worker CPU, and conservatively includes worker exit peaks seen between observations. Raw parent/worker components and a verifier guard omissions. No trimming/forced GC or budget increase.

**Known gaps:** Real music metadata/commands for an active player remain untested; automated commands use mocks. Existing hardware, settings/M2-M3, network-filtering, advanced-sensor/GPU, keyboard/accessibility and SDK release-notice gaps remain. Two physical mixed-DPI displays, Windows 10 and exclusive D3D fullscreen are not newly validated.

**Next step:** Finish the already planned frozen-binary observations, then review docs/MEDIA_BRIDGE_PROPOSAL.md. The worker meets its isolation/lifecycle design but exceeds 100 MB combined when expanded, so no further implementation or budget relaxation is made. A narrow verified native WinRT/COM provider is proposed, not implemented. No commits or M4-M7 work.

Final worker verification: the input-hold diagnostic added one Shell test; all 101 cases (50 Core / 19 Shell / 32 Windows) passed in each of TestResults/media-held-isolated-x64 and media-held-isolated-x86 after isolating architecture intermediates. Both actual 32-bit/64-bit held-input native view suites passed. Main app defaults remain interactive; the report records HoldDiagnosticInput=true for the idle trials. Worker accounting fixtures passed eight checks and native evidence guards passed eight mutation checks. All 27 binary-hash comparisons match, as do recorded source hashes. The style-exercise collapsed qualification remains explicit: its worker closed before SDK initialization, so a complete integrated post-loaded-media idle gate remains unverified. Expanded combined memory remains failed at 112.418816 MB; implementation is stopped for review of docs/MEDIA_BRIDGE_PROPOSAL.md. No commits or M4-M7 work.

## 30 October 2026 - approved native media bridge (validation in progress)

What was built: native Windows media-session ABI bridge on a demand-started MTA thread, no second process or managed SDK projection on normal launches. Existing worker remains available as the historical comparison fixture. User approved this bridge followed by the desktop UI revision, and Lucide 1.62.0 static assets. Desktop redesign remains pending bridge gates; detailed plan is docs/DESKTOP_UI_PLAN.md.

How verified so far: Release solution build passed, no warnings/errors. Full suite 110/110 passed on both x64 and x86 test hosts (TestResults/native-bridge-complete-x64 and -x86). Synthetic unmanaged fixtures cover title/artist HSTRING ownership, byte controls/results, INT64 timeline, mock-only playback callbacks, stale identity/token, async denial/cancellation and native reference balance. Read-only real probes report NoSession, with repeated teardown. Control-center smoke passed three views, no real playback commands, no WPF/projection in ambient host. Script accounting/math checks: 8 + 24 passed.

Measured: expanded Notch, 300.0001653 seconds / 301 samples, peak 57.012224 MB, CPU 0.2337238296% machine / 1.8697906364% one core (8 logical processors). Native scenario diagnostics passed, one visibility-scoped thread stopped, zero workers, no loaded media projection/WPF and no timers at exit. Source/binary hashes and CSV retained in docs/evidence/native-bridge/expanded. This measurement predates subsequent diagnostic-only frame/exercise fixes; it is a baseline, not evidence of the pending desktop redesign.

Known gap/rejected trial: initial after-use run exited in warmup because repeated BMP exports encountered a file lock. No performance pass claimed; retained at docs/evidence/native-bridge/post-expansion. Diagnostic exports now write each key once, and the exercise waits for a completed media read before closing. Full 300-second after-use and hidden trials are running on that diagnostic revision. They overlap as separate instances; neither is a child/helper cost of the other, and both have private reporters/measurements. All auxiliary costs of each tested instance remain counted. No forced GC or trimming. Live populated-player controls, mixed-DPI physical hardware, Windows 10 and hosted CI remain unverified. No Git operations performed.

Bridge baseline validation completed: after-use collapsed 55.873536 MB / 0.0168711614% CPU over 300.9943934 seconds; hidden 38.4 MB / zero recorded CPU seconds over 300.9983209 seconds. Both native lifecycle checks passed; collapsed finished a real media read before closure. Expanded baseline remains 57.012224 MB / 0.2337238296%. Raw replay passed. See docs/evidence/native-bridge/README.md for diagnostic binary differences and overlapping independent trial context. Desktop redesign now proceeds under the user's approval; these numbers do not certify it.

## R0 replacement UI - validation running

What was built: new R0-R2 plan/agreement; legacy island/native/media UI excluded from production; old sources preserved; 24 DIP WPF CPU/RAM feasibility fixture and lifecycle diagnostics; Windows battery-provider changes preserved. Six supplied references copied to ignored docs/reference. README/CI updated for the active entry point. See docs/R0_RETIRED_UI.md for intentionally retired UI tests. No new dependencies, Git operations or AppBar work-area changes.

How verified: Release solution build passed with zero warnings/errors. Active tests: 74 passed (57 Core / 2 Shell / 15 Windows), TestResults/r0-bar. Measurement harness: original 24 and new 6 exact bar/popup boundary checks passed. Five-minute transparent WPF trial is running; early ~111 MB readings exceed 50 MB, so no R1/R2 implementation proceeds. Full numbers and proposal follow after natural exit.

### R0 feasibility result - STOP for budget review

What was built: R0 cleanup/scaffold only, with source/test exclusions explicitly inventoried in docs/R0_RETIRED_UI.md. No R1 bar/Reserve/tray or R2 popup implementation. Current reference images are ignored. No new dependency, AppBar work-area change, real process termination, stage/commit/tag/branch or push.

How verified: Release solution and isolated x86 app builds passed, zero warnings/errors. Active suite 74/74 passed in each of TestResults/r0-bar and r0-bar-x86 (Core 57, Shell 2, Windows 15), both TRX guards passed. Old UI tests are retired, not claimed green. Harness checks: 24 original plus 6 new Bar/Popup byte and CPU boundaries passed. CI source updated, hosted CI not executed. The 345-second fixture exited naturally with its first frame, 115 live CPU/RAM sample attempts, owned timers stopped, no legacy native/island types in compiled assemblies and no managed media projection loaded. Raw CSV recomputation reproduced the failure.

Measured RAM and CPU: transparent WPF bar, 30-second warmup plus 300.0044968 measured seconds / 301 samples, peak 111.497216 MB, mean 100.955908 MB, p95 111.398912 MB. CPU 1.265625 seconds = 0.0527335846% of eight-logical-processor machine / 0.4218686765% core equivalent. Memory FAILS the new <=50 MB bar limit by 61.497216 MB; CPU passes. No trimming or forced collection. Evidence: docs/evidence/r0-wpf-bar/transparent. This is actual 125% render-target evidence, not R2 100%/150% screenshots or a desktop capture.

Known gaps: solid WPF fallback unmeasured; R1/R2 not implemented, including AppBar reservation/crash recovery and popup data sources/pressure formula. Physical mixed-DPI hardware and hosted CI remain unverified. Legacy sources remain on disk, but a Git preservation tag/branch was not created under the manual-Git agreement. Existing battery-provider changes were preserved. No later dashboard/settings/network/battery popup features are claimed.

Next step: review docs/R0_BUDGET_PROPOSAL.md. Proposed smallest next experiment is a full solid-WPF fallback trial under the same limit; alternative is a specifically approved native bar/popup renderer exception with no return to island/notch. Work is stopped; budget unchanged and R0 not marked accepted.

## WPF retained: solid-background feasibility result

What was built: no new UI feature or renderer switch. The user clarified that RAM, not file size, matters and requested WPF Bar. Retained WPF and executed the previously proposed solid fallback with --solid; all seven recorded binaries/manifests exactly match the transparent run. No package/dependency, system setting, AppBar reservation, trimming, forced GC or Git operation.

How verified: scripts/measure-r0-bar.ps1 -Solid -OutputDirectory docs/evidence/r0-wpf-bar/solid ran the complete bounded fixture and observer. Raw CSV recomputation agrees. Natural exit code 0, first frame rendered, owned timers stopped, no legacy native renderer/island shell in the compiled assemblies, no media projection loaded. Existing 74 active tests per architecture remain the last executed test results; binaries were unchanged and tests were not unnecessarily repeated. A render-target PNG is retained at actual 125% scale, not a desktop screenshot or R2 comparison.

Measured RAM and CPU: 301.002362 seconds / 302 samples after a 30-second warmup. Solid WPF peak 112.783360 MB, mean 99.447801 MB, p95 112.746496 MB; CPU 1.03125 seconds = 0.0428256606% machine normalized / 0.3426052849% one core. CPU passes; RAM FAILS <=50 MB by 62.783360 MB. Transparent historical peak was 111.497216 MB / 0.0527335846% CPU. Disabling transparency did not establish a memory-budget pass; differences between runs do not isolate the cause. These are running-process working-set numbers, not software download/disk size.

Known gaps: R1/R2 remain unimplemented and stopped at the memory gate; no popup result or 100%/150% R2 screenshots exist. Read-only module inventory contains graphics modules, but PE image spans are not resident RAM attribution or proof of a leak. No profiler was attached. Hardware/per-monitor/Reserve recovery and hosted CI remain unverified.

Next step: keep WPF as directed and review docs/WPF_MEMORY_NEXT_EXPERIMENT.md, which proposes a current-process software-rendering trial with no registry/system changes, the same live readings and unchanged 50/90 MB limits. It may affect CPU and has not been implemented or measured. Work stops pending budget review; no acceptance or higher memory allowance is inferred.

## Approved software-rendering WPF feasibility result - gate failed

**What was built:** Application-local software-rendering preference for bounded solid diagnostic launches, applied before Application construction, plus deferred context-menu creation. Normal launches retain the default preference. User approval is recorded in DECISIONS.md. No dependency, system setting, AppBar reservation, forced collection, working-set trimming or Git operation.

**How it was verified:** Release solution and isolated x86 application builds passed, zero warnings/errors. Active suite 75/75 passed on both x64 and x86 (57 Core / 3 Shell / 15 Windows), both TRX guards passed. Command: powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -OutputDirectory docs/evidence/r0-wpf-bar/software-only. Raw CSV replay reproduced the result and seven binary hashes match Release output. Natural app exit 0, first frame, 115 sample attempts, owned timers stopped, zero menu creations, chart/process work and reservations; no managed media projection or compiled legacy UI types. Lifecycle passes; numerical gate failed (2), enclosing command returned 1.

**Measured RAM and CPU:** 30-second warmup, then 300.9961386 seconds / 302 samples. Working-set peak 84.627456 MB, mean 80.620252 MB, p95 84.578304 MB; lifetime peak 84.643840 MB. CPU delta 0.859375 seconds = 0.0356887884% machine normalized over eight logical processors / 0.2855103072% core equivalent. CPU passes; RAM FAILS <=50 MB by 34.627456 MB. Historical solid trial peak was 112.783360 MB; the combined experiment reduced observed peak but does not isolate which change caused it. Raw CSV, hashes, context, module inventory, probe and render target retained under docs/evidence/r0-wpf-bar/software-only.

**Known gaps:** Actual pipeline unverified despite SoftwareOnly preference. Context-menu mouse/keyboard opening not interactively exercised. This tiny CPU/RAM fixture is not R1 or R2. Capture is at actual 125% and is a WPF render target, not desktop or requested 100%/150% comparison. Reserve/crash recovery, two physical mixed-DPI monitors, fullscreen behavior, popups and hosted CI remain unverified. Retired UI tests are not counted as passing. No claim that mapped DLL image spans equal resident RAM.

**Next step:** STOP at unchanged RAM gate. Review docs/WPF_VISUAL_HOST_PROPOSAL.md for a narrow WPF HwndSource/DrawingVisual feasibility experiment. It retains WPF and adds no package or machine setting, but is not implemented or approved. R1/R2 feature work remains paused.

## Lighter WPF visual host - completed, RAM gate failed

**What was built:** The explicitly approved R0 experiment uses WPF HwndSource with UIElement/DrawingVisual content, a combined accessible CPU/RAM reading name, bounded dispatcher lifetime, three-second live metric sampling and a demand-created context menu. No Application/Window is initialized on this branch; the original fixture remains available and normal launch stays unchanged. Native monitor/work-area reads and positioning affect only its own HWND. No new dependency, default architecture switch, machine configuration, AppBar reservation, GC/working-set manipulation or Git operation.

**How it was verified:** Normal Release solution plus isolated x64/x86 solution builds passed with zero warnings/errors. An older user instance initially locked Release files; the user exited it, and the normal build then passed. All 77 active tests passed on each architecture (57 Core / 5 Shell / 15 Windows), both TRX guards passed. Initial x86 run aborted before tests due to the wrong runtime host; process-local x86 runtime selection fixed the corrected run, without persistent environment changes. Successful test evidence: TestResults/r0-visual-host and TestResults/r0-visual-host-x86-corrected. Six-second desktop smoke rendered at 125%, verified its peer name and stopped naturally. Full command: powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -VisualHost -ExecutablePath src/IsletMonitor/bin/visual-host/IsletMonitor.exe -OutputDirectory docs/evidence/r0-wpf-bar/visual-host. Raw CSV replay agrees; all seven measured binary hashes match the isolated output. Normal Release binaries have distinct hashes and are not claimed numerically equivalent. Full app exited naturally with code 0, first frame, 115 sample attempts, owned timers stopped, zero menu creations/chart/process work/reservations. No PresentationFramework, managed media projection or compiled legacy UI types appeared. Lifecycle passes; numerical gate returned failure 2, enclosing command returned 1.

**Measured RAM and CPU:** 30-second warmup plus 300.002634 measured seconds / 301 samples. Peak/lifetime peak 76.337152 MB, mean 69.769985 MB, p95 73.580544 MB. CPU delta 0.640625 seconds = 0.0266924740% machine normalized across eight logical processors / 0.2135397918% core equivalent. CPU passes; RAM FAILS the unchanged 50 MB gate by 26.337152 MB. Prior software-only Window peak was 84.627456 MB. Cross-run differences are observations, not proof of an absolute WPF memory floor or a leak. Evidence retained under docs/evidence/r0-wpf-bar/visual-host, including raw replay, source/binary hashes and lifecycle report. Other Tidlet instances were absent at launch; separate desktop/build/test activity is disclosed in the experiment document.

**Known gaps:** Actual rendering pipeline remains unverified despite SoftwareOnly preference. No external screen-reader, interactive menu/keyboard, physical DPI transition, Reserve recovery or fullscreen acceptance. Root peer exposes a combined reading rather than separate module peers. The capture is a 125% WPF render target, not desktop screenshots or R2 100%/150% comparison. x86 test success is not x86 application memory evidence; PlatformTarget alone does not establish a 32-bit apphost. No R1/R2 features, popup budget or hosted CI pass are claimed. No post-menu memory pass exists.

**Next step:** STOP at RAM gate, retain WPF and the same limits. docs/WPF_X86_COMPARISON_PROPOSAL.md proposes verifying a genuine x86 apphost with the existing SDK and measuring the unchanged fixture; it is not implemented or approved. Do not begin R1/R2 or another trial without the required budget review.

## Genuine x86 WPF comparison - completed, RAM gate failed

**What was built:** Approved comparison using the existing SDK's genuine PE32/I386 win-x86 apphost, framework-dependent .NET 8 WPF visual host. Entry-point diagnostics now report actual runtime architecture and pointer size; the observer can reject a mismatch. Added scripts/build-r0-x86.ps1 for offline reproduction. Original fixtures, normal launch and default architecture remain unchanged. No new dependency, runtime installation, Git operation, admin action, machine setting, forced collection, working-set trim or R1/R2 feature.

**How it was verified:** Normal Release solution and genuine win-x86 application builds passed with zero warnings/errors. All 77 active tests passed in each actual x64/x86 runtime (57 Core / 5 Shell / 15 Windows), both TRX guards passed: TestResults/r0-genuine-x86-x64 and TestResults/r0-genuine-x86-tests. The first local-only restore failed when SDK resolution requested uncached runtime packs; explicitly disabling all apphost/targeting/runtime pack downloads allowed framework-dependent restore using installed/cached content. No remote feed or package download. Native PE header 0x014c / PE32 0x010b; manifest includes PerMonitorV2/asInvoker. Six-second smoke and full trial both reported X86 with four-byte pointers. Full command: powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -VisualHost -ExpectedPointerSize 4 -ExecutablePath src/IsletMonitor/bin/win-x86-probe/IsletMonitor.exe -OutputDirectory docs/evidence/r0-wpf-bar/genuine-x86. Raw CSV replay agrees; seven measured binary hashes matched before the reproducible build script was run successfully after natural exit. The script also passed with zero warnings/errors; subsequent hash verification remains recorded separately. Full trial: first frame, 115 sample attempts, root peer name verified, owned timers stopped, natural app exit 0, zero idle menu/chart/process work or reservations, no legacy UI/media projection. Lifecycle passed; numerical measurement failed (2), enclosing command returned 1.

**Measured RAM and CPU:** 30-second warmup plus 300.0027221 seconds / 301 samples. Peak/lifetime peak 60.882944 MB, mean 58.714908 MB, p95 60.821504 MB. CPU delta 0.96875 seconds = 0.0403642171% machine normalized over eight logical processors / 0.3229137367% core equivalent. CPU passes; RAM FAILS <=50 MB by 10.882944 MB. Previous x64 visual host peak was 76.337152 MB. This is the lowest observed WPF peak so far, not proof that architecture alone caused the difference: x86 loaded installed CoreCLR/WPF 8.0.14; x64 used a different runtime patch. Existing installed apphost pack is 8.0.20 under SDK 8.0.414. All costs of the measured process are included. Evidence: docs/evidence/r0-wpf-bar/genuine-x86, including raw CSV, source/binary hashes, architecture/manifest proof, module paths, assets/lock graph and lifecycle report.

**Known gaps:** Actual rendering pipeline remains unverified despite SoftwareOnly preference. No external screen-reader, interactive keyboard/menu, mixed-DPI physical transition, Reserve/fullscreen recovery, Windows 10 or ARM64 acceptance. PNG is an actual 125% WPF render target, not a desktop screenshot or requested R2 100%/150% comparison. Normal Release binaries are not the measured x86 output. No new R1/R2, popup budget, after-menu memory or hosted CI pass is claimed. Generic observer metadata retains architecture TODO/null runtime version; architecture.json and in-process probe provide separate verified x86/runtime evidence. Module image spans do not equal resident RAM.

**Next step:** STOP under the unchanged 50 MB limit. docs/WPF_RAM_BUDGET_DECISION.md requests explicit review of a 75 MB bar budget and genuine x86 WPF baseline for R1, keeping popup <=90 MB and CPU <1% unchanged. This is a proposed requirement/default-architecture decision, not approval or an original-budget pass. Alternatively retain 50 MB and stop feature implementation. No further experiment or feature work proceeds until that review.

## R1 bar - implemented; resource gate passed, hardware acceptance gaps remain

**What was built:** Genuine x86 WPF HwndSource/DrawingVisual bar, 24 DIP, alpha-translucent dark surface, fixed CPU/RAM/upload/download/battery items, physical GetIfTable2/LUID deltas, missing-battery hiding, reorderable validated JSON settings, top/bottom, alignment, primary/all/selected monitors, Overlay/Reserve, native tray/context menu, pause/hide/default-item controls, foreground/fullscreen/session/lid/display-off suppression and slower battery-saver polling. No old island/notch/media/creature renderer, new package, system setting, real termination or Git operation. Dashboard/full settings remain visibly deferred to R4. Read-only API sources and limits are in API_REGISTER.md.

**How verified:** Genuine x86 app and Release solution builds passed with zero warnings/errors. 87 active tests passed on each x64/x86 runtime (Core 61, Shell 8, Windows 18), both TRX guards passed: TestResults/r1-final-x64 and r1-final-x86. Tests cover settings recovery/validation, placement/hit targets/negative coordinates, absent battery, physical/virtual adapter filtering, counter reset/adapter change, native ABI sizes, shared sampling across fake monitors, zero reads while hidden and the timer-cadence regression. Original measurement harness 24 and approved 75/90 MB boundary harness 6 checks passed outside the sandbox; initial sandbox invocations were denied by AuthorizationManager, no security/policy setting was changed. Owned Reserve test observed work area top 0 -> 30 -> 0 physical pixels, and owned maximized DWM frame top 30. Ten-second corrected run took four samples; prior timer-reset/virtualized-observer runs are retained separately. Full command: powershell -NoProfile -File scripts/measure-r1-bar.ps1 -Reserve -OutputDirectory docs/evidence/r1-bar/reserve-300s. Raw replay agrees and seven measured hashes match. Natural exit, first frame, 115 samples, EverHidden=false, owned timers stopped, native tray removal issued and zero chart/process work. No helpers.

**Measured RAM and CPU:** 30-second warmup plus 301.0054949 measured seconds / 302 samples. Peak 63.426560 MB, mean 61.372375 MB, p95 63.115264 MB, lifetime peak 63.627264 MB. CPU 1.140625 seconds = 0.0473672831% machine normalized across eight logical processors / 0.3789382650% core equivalent. PASS against explicitly approved 75 MB bar cap and <1% CPU. No forced collection/trim or discounted runtime pages. This is the isolated x86 R1 build, not a numerical claim about normal x64 Release output. Actual DPI is 125%; capture is a render target, not a desktop screenshot.

**Known gaps:** Real crash/kill recovery is best-effort code only and not certified; no real process was terminated. Two physical mixed-DPI displays, top-taskbar conflict on another configuration, actual screen-off/lid/session/fullscreen events and five-minute hidden run remain unverified. Fake monitor/suppression tests do not replace them. Tray cleanup result currently reflects issuing removal and logical state, not an external tray enumeration. Alpha transparency is implemented; acrylic/Mica blur and full high-contrast/light/accessibility polish are deferred gaps. Normal project default architecture promotion is pending the R2 integration. Hosted CI not run. R1 is not marked fully accepted.

**Next step:** Resource feasibility now permits R2 CPU/RAM popup implementation. Retain <=90 MB open-popup and <=75 MB closed/after-popup gates. Hide unsupported rows instead of macOS-only invented breakdowns. Measure R2, label 100%/150% render comparisons honestly, and stop for review after R2. No commit; user owns Git.

## R2 CPU/RAM popup preview - implemented, STOP for memory/acceptance review

**What was built:** Reusable borderless WPF HwndSource popup kit with 13-DIP corners, theme JSON tokens (dark active/light equivalents stored), Windows font glyphs, own ring/pressure gauges, fixed two-minute area history, core bars, detail rows, used/available stack, scrolling process list and accessible pane/header actions. CPU shows validated System/User/Idle, reported frequency, uptime, valid logical-core readings, optional verified EfficiencyClass grouping and warming-aware own 1/5/15-minute averages. No invented temperature ring. RAM uses Windows physical/commit/cache/page-file APIs with documented overlap and pressure formula; unsupported modified/compressed breakdowns are omitted. Basic physical RAM fallback remains available when advanced queries fail. Processes use visibility-scoped PDH Working Set/ID Process plus creation-identity/monotonic CPU deltas, with denied/exited rows skipped and no kill action. Anchoring/clamping uses the clicked item and monitor; toggle/deactivation/Esc/close paths and explicit Task Manager boundary are implemented. Normal project output is now genuine x86; old fixtures remain diagnostics, not the product UI. No R3-R5 features, package addition, real process termination, system/security change, forced collection/trim or Git operation.

**How it was verified:** Normal Release solution and isolated x86 builds passed, zero warnings/errors. Final held-diagnostic revision passed all 93 active cases on each x64/x86 runtime (Core 65 / Shell 9 / Windows 19), TestResults/r2-held-x64 and r2-held-x86; TRX guards verify the suites. Tests add native structure/pointer layouts, invalid pressure/commit/physical readings, PID reuse/resets/interval checks, fixed-window warmup/wrap, zero-allocated-byte ring update/average loops, and provider creation/read/disposal across visible/closed popup phases. Real CPU/RAM ten-second smokes returned repeated frames, six process rows and drained workers; CPU exposed eight logical core readings. Task Manager launch was not actually invoked by tests. CPU/RAM current-render PNGs were inspected and linked beside supplied references in ignored docs/reference/r2-comparison.html. Physical desktop was 125%; 100%/150% files are RenderTargetBitmap exports, not physical-monitor desktop screenshots.

The initial five-minute CPU attempt was rejected: its event history recorded seven CPU/RAM opens/closes, and it ended closed. Numerical window peak 84.770816 MB and lifetime 92.917760 MB are exploratory mixed-state values with in-window diagnostic exports, not CPU/RAM/after-close acceptance. Evidence is retained at docs/evidence/r2-popups/cpu-300s/rejected-scenario.json. Bounded diagnostic launches now hold the selected view against incidental clicks/deactivation while preserving tray Exit and the automatic time limit. Normal launches remain interactive. No optimization or budget relaxation was made as part of this instrumentation correction.

**Measured RAM and CPU:** Frozen normal Release x86 build, CPU popup held open: 30-second warmup then 300.0163839 measured seconds / 301 samples. Window peak 88.498176 MB, mean 76.912225 MB, p95 80.658432 MB; CPU delta 8.625 seconds = 0.3593553745% machine normalized across eight logical processors / 2.8748429962% core equivalent. Numerical five-minute memory window passes 90 MB. However, lifetime startup peak was 91.062272 MB, exceeding the unchanged 90,000,000-byte cap by 1.062272 MB; no startup exemption is assumed. Overall R2 acceptance is therefore blocked. One CPU open at 0.0018 s, one close at 344.9942 s, 343 presented frames / 344 queries; no hidden interruption, zero outstanding workers, cleanup drained, timers stopped and native tray deletion succeeded. Raw CSV replay agrees; all seven hashes match. Evidence: docs/evidence/r2-popups/cpu-held-300s (raw-check.json distinguishes numerical pass from overall non-acceptance). The UI draw counter recorded 70,434,840 cumulative allocated bytes across 343 frames (~205 KB/frame), not retained working set; worker allocations are separate. Only numeric ring operations, not WPF presentation as a whole, are verified allocation-free.

**Known gaps:** Full RAM, post-popup <=75 MB and hidden/fullscreen 300-second trials are not executed after the startup breach. RAM final review was an already planned ten-second read-only visual run, not resource acceptance (docs/evidence/r2-popups/ram-review-final/review-check.json). Current CPU/RAM render comparisons are available at 100%/150% export scale; real physical 100%/150%, two mixed-DPI monitors, Windows 10, crash/kill recovery, lid/display-off/fullscreen behavior, external input/screen-reader coverage and hosted CI remain unverified. R1 normal-exit Reserve restoration remains the observed single-display result, not crash certification. Acrylic/Mica, light/high-contrast/translation polish and full Dashboard/Settings remain deferred. Pressure is a documented heuristic; cache is not a disjoint physical category. Top process percentages currently round to whole percent; partial valid logical samples feed optional group averages. Long-running-query/stale-state presentation and per-row accessibility require further review. R2 is not marked complete/accepted.

**Next step:** STOP implementation under AGENTS.md's budget rule and the requested R2 review stop. docs/R2_MEMORY_REVIEW.md proposes a dependency-free correction: remove unnecessary theme reflection deserialization and reuse immutable drawing resources/point storage, then test cold-open, full CPU/RAM, after-close and hidden paths under unchanged 75/90 MB limits. It is not implemented or approved. All active files remain in the user's Tidlet project; no commit or other Git operation.

## R2 correction in progress — 30 October 2026

**What was built:** User-approved correction: direct JsonDocument theme parsing, frozen reusable frame/chart/core drawing resources, per-popup bounded last-value text cache with DPI invalidation, direct history geometry streaming. Layout and provider definitions unchanged. Updated active scope/plan for remaining R3-R5 UI, contingent on resource gates. Measurement wrapper now rejects lifetime popup peaks above 90 MB and changed binary hashes, in addition to numerical/lifecycle checks.

**How verified so far:** Isolated Release build `-p:OutputPath=bin/r2-correction/` succeeded with zero warnings/errors. Active suites passed 93/93 each on actual x64 and x86 runtimes (TestResults/r2-correction-x64 and r2-correction-x86). Measurement regression scripts passed 24+6 checks. Normal Release replacement failed because existing IsletMonitor PID 22936 locks Islet.Shell.dll; no process termination attempted. Isolated output remains inside this project.

**Measured RAM and CPU:** Full corrected CPU trial running; no acceptance claim yet. Historical failed startup peak remains 91.062272 MB until the new result is complete.

**Known gaps:** RAM/after-close/hidden full measurements pending. R3-R5 UI not implemented yet. Physical mixed-DPI/fullscreen/accessibility and hosted CI remain unverified.

**Next step:** Complete CPU resource/lifetime/lifecycle review, then RAM and after-close/hidden gates; stop again if a cap fails.

## R2 corrected CPU evidence — complete; remaining gates pending

**What was built:** Approved drawing/theme correction described above. Existing newer Network/Battery source edits were discovered on resume and preserved; their review findings are in docs/R3_EXISTING_EDITS_REVIEW.md. They are not included in the frozen bin/r2-correction application used for these measurements. Do not treat those edits or the newer normal Release build as certified by this trial.

**How verified:** Corrected CPU repeat completed with one open/close, 342 frames / 343 reads, workers drained, timers stopped, tray deletion successful, no hidden interruption and seven unchanged binary hashes. New drawing regression passed within 11 Shell tests on each actual x64/x86 runtime (TestResults/r2-drawing-x64 and r2-drawing-x86). Earlier full active suites passed 93 each; later added test cases are reported separately. Normal Release build now succeeds with zero warnings/errors after the old instance exited. No process was terminated.

**Measured RAM and CPU:** CPU: 300.9902198 seconds / 302 samples, working-set maximum 78.819328 MB, mean 61.490630 MB, p95 75.444224 MB; lifetime peak 81.391616 MB. CPU delta 8.65625 seconds = 0.3594905013% machine / 2.8759240103% core equivalent across eight logical processors. Both 90 MB and CPU gates pass for this trial. UI allocated 49,170,204 cumulative bytes across 342 frames (~143,773 bytes/frame), versus historical ~205 KB/frame; worker allocations are outside that UI counter, but whole-process working set includes them. Cross-run OS activity prevents a causal savings guarantee. Evidence: docs/evidence/r2-popups/correction-cpu-repeat-300s.

**Known gaps:** First correction run exited early (287 samples); retained as Incomplete/rejected-scenario.json, not a pass. Full RAM/after-close/hidden trials remain pending. Current Network/Battery and Dashboard/Settings completion are not accepted. Physical hardware/CI gaps remain.

**Next step:** Complete remaining unchanged R2 resource gates before adding further UI.

## R2 correction — open-popup trials pass; STOP at closed-bar memory gate

**What was built:** Direct theme parsing, frozen drawing resource reuse, bounded last-value text caching and direct chart streaming. Added a rendered-pixel regression for changing values/names and DPI, and lifetime/hash enforcement to the measurement wrapper. User clarified Network/Battery popup additions were experimental and requested removal: removed those popup records/views/provider routes and their construction-only tests; preserved genuine bar network/battery providers and legacy sources. Backup is local ignored artifacts/retired-network-battery-experiment. Polished replacements must reuse the CPU/RAM kit after resource review. A fullscreen validation script was prepared and syntax checked, but not executed after the budget failure. No Dashboard/Settings completion is claimed.

**How verified:** Final `dotnet build TidletMonitor.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` passed with zero warnings/errors. `dotnet test TidletMonitor.sln -c Release --no-build --no-restore` and the genuine x86 variant passed 94/94 each (65 Core / 10 Shell / 19 Windows); TRX: TestResults/r2-cleanup-final-x64 and r2-cleanup-final-x86. New drawing test verifies restored pixel equality after repeated data and render-DPI changes. Measurement-script regression checks passed 24+6. Three full resource trials retained raw CSV, unchanged seven-binary hashes and lifecycle proof; replay matches summaries. Those trials cover frozen bin/r2-correction; final cleanup source/normal Release was built/tested, not remeasured after the required stop. Hosted CI remains unexecuted.

**Measured RAM and CPU:** CPU popup: 300.9902198 seconds, observation peak 78.819328 MB, lifetime peak 81.391616 MB, average machine CPU 0.3594905013% — PASS. RAM popup: 300.0010444 seconds, observation peak 79.896576 MB, lifetime 82.391040 MB, CPU 0.4029933887% — PASS. After CPU close: 300.9917463 seconds / 302 samples, peak 77.950976 MB versus 75 MB, mean 57.469864 MB, p95 77.619200 MB; CPU 0.0298492404% — RAM FAIL, CPU pass. The initial legitimate popup lifetime peak was 78.491648 MB. Closed at 15 seconds, 14 frames / 15 reads before close and no subsequent chart/process work; workers drained, timers stopped, tray deletion succeeded. Eight logical processors used for CPU normalization. Evidence: docs/evidence/r2-popups/correction-cpu-repeat-300s, correction-ram-300s, correction-after-cpu-300s. Earlier interrupted 287-sample run remains Incomplete, not a pass. No forced GC/trim or excluded helper costs.

**Known gaps:** Overall R2 remains unaccepted because the post-popup bar exceeds 75 MB by 2.950976 MB. After-RAM and hidden full trials were not run after this failure. Dashboard/Settings and other polished module UI remain pending. Physical mixed-DPI, real games/fullscreen, crash recovery, external screen-reader and hosted CI acceptance remain gaps. Current exports are render targets, not physical-DPI screenshots. The cleanup source has no experimental popup Ping route; no automated playback/termination/fixer/system-security action or Git operation occurred.

**Next step:** STOP under AGENTS.md budget rule. docs/R2_POST_POPUP_MEMORY_REVIEW.md offers a bounded close-path release experiment at unchanged caps, or an explicit bar RAM revision to 90 MB. Neither is approved. User's overall UI completion approval persists, but does not silently alter the resource limit.

## Common 90 MB policy and pending validation — 30 October 2026

**What was built:** User explicitly approved bar <=90,000,000 bytes and popup <=90,000,000 bytes; CPU <1%, hidden CPU/activity and no-trim/accounting rules unchanged. Updated active rules, execution plan, measurement boundary/context and regression tests. Historical 75 MB failure is preserved unchanged. The meter also now enforces popup CPU <1% and hidden RAM <=90 MB.

**How verified:** Measurement regressions passed 24+8 checks. Full after-RAM trial on the current normal Release passed lifecycle and hashes; no heavy work after close. Fullscreen fixture C# compiled and x64 MSG size checked at 48 bytes without creating a window during that compile check. Fullscreen trial is now executing; no success claim yet. Initial diagnostic reflection SizeOf overload failed and was corrected by passing a boxed structure; this did not affect the app/measurement.

**Measured RAM and CPU:** AfterRam: 301.0049209 seconds / 302 samples, working-set max 79.294464 MB, mean 65.422817 MB, lifetime 82.112512 MB; average machine CPU 0.0337411427% across eight logical processors. 13 frames / 14 requests before close at 15 seconds, then zero additional chart/process work. Evidence docs/evidence/r2-popups/approved90-after-ram-300s. This is a new run under 90 MB, not a relabeled historical 75 MB result.

**Known gaps:** Fullscreen result pending; remaining UI unimplemented. Physical mixed-DPI/crash/Windows10/screen-reader/hosted CI limitations persist.

**Next step:** Complete fullscreen validation, then implement R3-R5 using the approved plan and resource gates.

## R2 common-90 MB validation completed; R3 module implementation in progress

**What was built:** Recorded explicit 90 MB bar/90 MB popup approval; boundary tests enforce bar, popup CPU and hidden RAM limits. Added independent per-sample EnumWindows/IsWindowVisible suppression proof. R3 replacement Network/Battery/Disk/GPU views reuse the CPU/RAM kit, with mirrored byte-rate history, real per-disk PDH/volume rows, reported battery capacity and DXGI/PDH per-adapter GPU readings. Physical network service shares exact cumulative deltas with the bar; no added network request. Optional module entries remain detail launchers until the later settings/live-item integration. Sensor helper remains absent.

**How verified:** R2 AfterRam full trial passed current normal Release at 79.294464 MB / 0.0337411427% CPU with no post-close heavy work. First owned-fullscreen run had numerical pass but failed strict scenario proof after losing foreground; retained as not accepted. New visibility-observed run passed every sample (302 windows observations hidden), 693 fixture foreground checks/zero failures, zero app metric/chart/process reads, successful timer/tray cleanup, X86 pointer size 4. Real games remain untested. First R3 Release builds passed zero warnings/errors; first x64 suite passed 98 tests (68 Core/10 Shell/20 Windows), with an additional exact-network-total test added afterward. Four ten-second live smokes exited naturally, drained all workers and exported 100/150 render scales; no resource pass is claimed from them. All four 100% exports were visually inspected. Network/Battery/Disk popup heights were tightened afterward; new render/resource checks pending.

**Measured RAM and CPU:** R2 hidden-window run: 300.9819738 seconds, max 52.006912 MB, lifetime 52.015104 MB, machine CPU 0.0071380936% / core equivalent 0.0571047488% over eight logical processors. Evidence docs/evidence/r2-popups/approved90-hidden-visibility-300s. R3 five-minute resource trials not yet run. Actual short smoke returned separate Intel Iris Xe and NVIDIA GTX 1650 identities and valid engine samples; NVIDIA zero was a reported idle sample, not a placeholder. Battery reported full/design mWh and cycles; disk returned physical-disk rates and separate C: volume capacity. Private address visuals/probes remain in ignored artifacts/r3-smoke-first.

**Known gaps:** Full R3 resource/long lifecycle gates, x86 new ABI/math tests, no-GPU/denied fixtures, settings/dashboard/theme/accessibility and network opt-in controls remain pending. Wi-Fi/ICMP/public-IP/per-process network are currently off; no fake internet status is displayed. Battery capacity explicitly identifies the first detected battery; aggregate multi-pack capacity/rates are not certified. Per-disk volume mapping is not inferred. Physical mixed-DPI/crash/Windows10/hosted CI and real-game acceptance remain gaps.

**Next step:** Complete tests and R3 resource gates, then finish Dashboard/Settings/polish under the common 90 MB limit. No dependency, Git, machine/security setting, real process termination or playback command.

## R3 verified; R4 application memory STOP — 31 October 2026

**What was built:** Replacement Network/Battery/GPU/Disk kit popups, lazy sidebar application window, device overview, settings exchange and local compatibility export. Latest five references are saved under ignored docs/reference. They clarify wide application module pages: compact popups belong only to top-bar clicks. Current embedded popup pages must be replaced; the requested wide pages are not implemented yet.

**How verified:** Frozen R3 built with zero warnings/errors and passed 99 tests each on actual x64/x86. Four full trials retained 302 samples each, unchanged seven-binary hashes and natural worker/timer/tray cleanup. Evidence docs/evidence/r3-popups/{gpu,battery,disk}-300s; Network remains private in ignored artifacts/r3-measure-network-300s. Current R4 isolated Release builds passed zero warnings/errors; 101 tests passed each on actual x64/x86 (70 Core/10 Shell/21 Windows), TRX TestResults/r4-budget-review-{x64,x86}. Corrected eight-second Dashboard/Settings/CPU smokes exited naturally and drained workers. Older first smoke failed during shutdown; early Stopping and IsDisposed guard corrected that observed failure.

**Measured RAM and CPU:** R3 observation/lifetime peaks, average machine CPU: GPU 80.744448/81.952768 MB, 0.2718838099%; Network 77.266944/80.945152 MB, 0.2939486528%; Battery 67.719168/71.041024 MB, 0.1596326378%; Disk 74.141696/76.984320 MB, 0.2692879291%. All four pass for frozen R3. R4 full-app trial: 300.9826999 s/302 samples, observation max 89.681920 MB, lifetime **94.892032 MB**, CPU 0.0467219545%. Lifetime RAM FAILS 90 MB. Evidence docs/evidence/r4-dashboard/overview-300s. Static overview scenario also rejected: two opens and 18 module reads/11 frames occurred, so do not call this an isolated overview pass. Hashes unchanged, natural exit, drained workers, stopped timers and tray deletion passed. No GC/trim or auxiliary host.

**Known gaps:** R4/R5 unfinished; appearance/unit preferences not fully applied, optional GPU/Disk bar items remain detail launchers, ICMP/WLAN/public-IP off, first-battery capacity only. Wide application pages, navigation stress/after-close/hidden resource tests, light/high-contrast/accessibility/translation polish remain pending. Physical mixed-DPI, real games, Windows10, screen-reader and hosted CI are unverified.

**Next step:** STOP under AGENTS.md. Review docs/R4_APPLICATION_MEMORY_REVIEW.md for a bounded wide-page/caching/lifecycle correction with unchanged 90 MB limits. No Git, machine/security setting or further feature implementation after failure.

## Approved wide CPU/RAM correction — tested; resource STOP — 31 October 2026

**What was built:** User approved correction at unchanged 90 MB. Application CPU/RAM now use dedicated full-width cards, summary rings/bars, 120-second histories and scrollable per-core/pressure/page-file/process sections. They no longer instantiate PopupVisual; compact popups remain on the bar. Windows memory labels avoid fake macOS Wired/App mappings. Homogeneous CPUs are not falsely labeled P/E. Frozen resources and bounded last-text entries reduce repeated formatting; old/new presenter disposal is serialized within window navigation. Application visual work still allocates; no whole-UI allocation-free claim. Held-input diagnostic mode and page history distinguish fixed-page trials. Tail observation includes final capture lifetime peaks. Other application module pages currently show a pending-conversion explanation; no completed R4/R5 claim.

**How verified:** Isolated Release builds succeeded with zero warnings/errors. Active tests passed **103/103 on each actual x64 and x86 runtime** (70 Core / 12 Shell / 21 Windows), TRX TestResults/r4-wide-correction-{x64,x86}. New tests verify pixels across changing readings/DPI, wide content beyond popup width, predecessor wait and no reads after disposal. CPU/RAM eight-second smokes exited naturally, drained workers and each recorded one held page. Both 100% exports and CPU 150% export visually inspected; comparison docs/reference/r4-wide-comparison.html is ignored and labels render exports, not physical screenshots. Measured bin/r4-wide-correction remained unchanged; tests used separate validation output.

**Measured RAM and CPU:** Full corrected CPU application trial: **300.007616 seconds / 301 samples**, observation max **94.842880 MB**, mean 77.746067 MB, p95 86.667264 MB; lifetime including post-observation tail/captures **94.949376 MB**. Average machine CPU **0.3496004998%**, core equivalent 2.7968039985% across eight logical processors. RAM FAILS 90 MB by 4.949376 MB; CPU passes. 345 reads/344 frames; one open and page history [CPU], input held, no fullscreen interruption, workers drained, timers stopped, tray removed, seven binary hashes unchanged. Evidence docs/evidence/r4-dashboard/wide-cpu-300s, including raw samples, tail CSV, probe and rejected scenario-check. No forced GC, working-set trim, new helper or hidden process costs.

**Known gaps:** Full corrected RAM/overview/navigation/after-close/hidden trials were not started after the failure. Remaining wide module conversions, actual processor model in CPU summary, module settings layout, theme/units/high-contrast/keyboard/icon polish and cross-window navigation stress remain incomplete. Original R3 popup passes do not certify this application. Physical mixed-DPI/Win10/games/screen-reader/CI gaps remain.

**Next step:** STOP again as required. docs/R4_OPAQUE_WINDOW_PROPOSAL.md proposes changing only the large application window to the opaque WPF path, retaining the layout and 90 MB limit, then repeating full tests. This is a proposal, not implemented or claimed to pass. Normal Release remains the earlier R3 build; corrected development binary stays isolated. No Git or machine/security setting changes.

## Approved opaque application comparison — complete; RAM STOP — 31 October 2026

**What was built:** User explicitly approved opaque WPF comparison. Changed only the large application HwndSource to UsesPerPixelOpacity=false, with opaque theme-color HwndTarget.BackgroundColor. Wide CPU/RAM layout, bar and compact popups, x86 architecture and software-rendering preference retained. Per-window corners were deliberately not changed; square outer fallback remains. Actual target opacity is recorded false. No framework/package, DWM hook, global graphics/security setting, forced collection or memory trimming.

**How verified:** Isolated Release build passed zero warnings/errors. Both actual x64/x86 test runs passed **104/104** (70 Core / 13 Shell / 21 Windows), TestResults/r4-opaque-{x64,x86}. New test verifies actual opaque target and zero metric-source construction/reads for overview. Eight-second CPU smoke exited naturally with provider draining and 100/150 render exports. Inspected 100% export; these export RootVisual without the HWND background and are not physical screenshots. Full CPU trial held one page, 343 reads/342 frames, target opaque, no fullscreen interruption, workers drained, timers stopped, tray removed and seven binary hashes unchanged. Source output bin/r4-opaque-comparison was frozen; tests built separately.

**Measured RAM and CPU:** **300.9904587 s / 302 samples**, working-set observation max **86.368256 MB**, mean 56.161626 MB, p95 83.095552 MB; complete reported lifetime/tail peak **91.037696 MB**, exceeding 90 MB by **1.037696 MB**. CPU **0.3932329799% machine**, 3.1458638393% core equivalent, eight logical processors. Numerical observation passes but lifetime wrapper rejects overall; this is a RAM failure, not acceptance. Evidence docs/evidence/r4-dashboard/opaque-cpu-300s includes raw CSV, summary, tail CSV, hashes, scenario-check and probe. The lower peak than the previous transparent run does not establish a causal savings guarantee across separate OS sessions/activity.

**Known gaps:** RAM/overview/navigation/after-close/hidden resource trials not run after failure. Remaining application module conversions, settings/theme/units/accessibility polish and physical screenshots/Win10/mixed-DPI/real-game/CI acceptance remain incomplete. Normal Release stays on R3; latest opaque application is isolated development output.

**Next step:** STOP under AGENTS.md. docs/R4_APPLICATION_NEXT_DECISION.md offers a bounded 860x580 DIP initial-window comparison at unchanged 90 MB, or explicit 100 MB application-window allowance while preserving 90 MB bar/popups. Neither option is approved; no feature work follows this failure. No Git or machine settings changed.

## Approved 860x580 comparison — CPU gate passes; RAM/overview pending

**What was built:** User selected smaller 860x580 DIP initial window at unchanged 90 MB (Option A). Only application initial dimensions changed; opaque WPF, wide cards/scrolling, bar/popups and x86/software preference retained. Initial placement now derives from the same width/height constants, avoiding duplicate mismatched dimensions. The unselected 100 MB option is not approved.

**How verified so far:** Release build zero warnings/errors; active tests passed 104 each on actual x64/x86, TestResults/r4-small-{x64,x86}. Bounded CPU smoke exited naturally with opaque target and drained worker; PNG header verified exactly 860x580 at 100%, and the render export was visually inspected. Full CPU trial held one page (343 reads/342 frames), lifecycle/hash checks passed. Current measured build is isolated bin/r4-small-comparison. X86 validation used separate output; early CPU observation overlapped validation workload, so do not infer a causal RAM reduction or universal guarantee from cross-run differences.

**Measured RAM and CPU:** CPU: 300.9872154 seconds/302 samples, observation peak 75.231232 MB, mean 44.335606 MB, p95 49.577984 MB; whole lifetime/capture-tail peak **87.199744 MB**; average machine CPU **0.3938861235%** / core equivalent 3.1510889881% across eight logical processors. PASS for this run. Evidence docs/evidence/r4-dashboard/small-cpu-300s. No forced GC/working-set trim, hidden helper or excluded application window.

**Known gaps / Next step:** RAM and static overview full trials pending, then navigation/after-close/hidden validation before remaining module-page conversion. This CPU pass alone does not finish R4/R5 or replace earlier failures. Physical mixed-DPI/Win10/games/screen-reader/CI gaps remain. No Git/system-setting action.

## Smaller-window static gates complete; lifecycle checks next

**What was built / How verified:** Same frozen bin/r4-small-comparison application and 860x580 layout. Three full static CPU/RAM/overview trials now pass with single held pages, unchanged seven-binary hashes, natural exit, drained workers, stopped timers/tray cleanup and actual opaque target. Prior 104 tests each architecture remain the executed suite for this baseline. Added separately built diagnostic navigation/close timers, bounded page/close event history, active counters and serialization of worker disposal across window reopening. Those newer changes need their own tests/resource checks and are not certified by the frozen static trials.

**Measured RAM and CPU:** CPU 300.9872154 s: observation max 75.231232 MB / lifetime 87.199744 MB / CPU 0.3938861235%. RAM 300.9909975 s: 78.225408 / 88.547328 MB / 0.3406715262%. Dashboard 300.9849933 s: 76.824576 / 76.832768 MB / 0.0532107094%; zero module metric reads/frames. All 302 samples each; RAM 344 reads/343 frames, CPU 343/342. Raw summaries/probes/hash/lifetime-tail evidence docs/evidence/r4-dashboard/small-{cpu,ram,dashboard}-300s. Earlier large-window failures remain unchanged. CPU's early observation overlapped validation workload; no cross-run causal or all-hardware memory guarantee.

**Known gaps / Next step:** Execute bounded navigation/close diagnostics, full navigation/after-close/hidden gates, then remaining application pages/settings/polish. No complete R4/R5 or physical mixed-DPI/Win10/games/screen-reader/hosted CI claim. No Git, dependency or system setting changes.

## Lifecycle validation — static passes retained; navigation capture-tail RAM STOP

**What was built:** Active read/frame counters, bounded page/close events, diagnostic navigation/close timers, worker disposal serialization across application-window reopening, and wrapper checks for navigation/after-close behavior with tail lifetime accounting. Prepared a hidden fixture option that opens CPU/RAM before the fullscreen stimulus; this option has not run. Normal UI remains 860x580 opaque WPF; remaining module pages are not converted.

**How verified:** Corrected Release builds zero warnings/errors; **105 tests passed each on actual x64/x86**, TestResults/r4-lifecycle-corrected-{x64,x86}. Old fixed-deadline lifecycle test failed in both concurrent runs; targeted rerun passed. Updated test waits for two actual mock reads (ten-second deadline) and drains pending work before checking no further reads; no assertion or production budget waived. Failed results remain in TestResults/r4-lifecycle-final-{x64,x86}. New reopening test confirms previous source disposal before the next factory and correct active/closed counts. Bounded live smokes passed: navigation 11 pages/15 requests/9 frames; after-close 9 requests/8 frames before close, unchanged at baseline and exit; workers drained. Full navigation held one window, visited 87 pages across CPU/RAM/Dashboard/Sensors, made 200 requests/156 frames, closed at 345.0044852 seconds and passed lifecycle/activity/hash checks.

**Measured RAM and CPU:** Static smaller CPU/RAM/Dashboard passes above remain valid for their frozen baseline. New navigation: 300.006890 s/301 samples, observation max 83.898368 MB, mean 78.473196 MB, p95 83.189760 MB; CPU 0.2897068881% machine / 2.3176551045% core equivalent across eight processors. Observation-only numerical PASS, but tail lifetime **93.863936 MB** versus 90 MB is **FAIL**. Tail starts with lifetime 88.551424 MB; period includes automatic dual-scale exports and last navigation. Sampled tail working-set max 91.226112 MB. Exact allocation cause not isolated. Evidence docs/evidence/r4-dashboard/navigation-300s including tail CSV and rejected scenario-check. Opaque target, workers drained, timers/tray cleaned, seven hashes unchanged. No GC/trim, cost-shifting helper or excluded application window.

**Known gaps:** Full after-close/hidden trials not started after failure; only short smokes passed. New diagnostics are not certified by older static build measurements. Remaining application pages/settings/theme/accessibility and physical/CI gaps remain. Normal Release stays R3; lifecycle build is isolated bin/r4-lifecycle-corrected.

**Next step:** STOP under AGENTS.md. docs/R4_CAPTURE_MEMORY_PROPOSAL.md proposes one scale per bounded diagnostic run, then separate complete 100/150 navigation trials at unchanged 90 MB. This is not implemented/approved and does not exempt current export costs. No Git/system-setting action.

## Single-scale diagnostic correction — implemented; full measurements pending

**What was built:** User requested continuation after the pending capture proposal. Added explicit --dashboard-capture-scale 100|150 for bounded diagnostic runs only; one requested export replaces the dual-export burst for these runs. Default interactive drawing, viewport, polling and navigation cadence unchanged. Legacy dual-scale path remains available. Capture records contain requested scale, actual PNG dimensions and start/completion times. Wrapper checks exactly one requested image and validates its PNG header/dimensions/time, while retaining cold/lifetime/tail accounting at 90 MB.

**How verified:** Initial test build failed on a missing System.IO import in the new test; corrected. Release builds then passed zero warnings/errors. All **106 tests passed on each actual x64/x86 runtime** (70 Core / 15 Shell / 21 Windows), TestResults/r4-capture-correction-{x64,x86}. Regression creates separate hosts and confirms only one image per requested scale, recorded dimensions and readable PNG metadata. Separate eight-second CPU smokes exited naturally and drained workers: 100% 860x580, 150% 1290x870; one image each. Inspected 150% render export; these are RootVisual render targets, not physical screenshots or HWND-background captures. Measurement wrapper syntax passed. Frozen measured output bin/r4-capture-correction; x86 tests use separate output.

**Measured RAM and CPU:** Full sequential 100/150 navigation trials pending; no resource pass yet. Earlier dual-scale navigation lifetime failure 93.863936 MB remains unchanged. No forced collection/trim, helper or excluded capture costs.

**Known gaps / Next step:** Complete full single-scale trials; stop on any failure. Only after both pass, full after-close CPU/RAM and owned-window hidden-work tests before more module-page conversion. Remaining UI/physical/CI gaps remain. No Git, new dependency or system setting changes.

## Single-scale 100% trial complete — pre-capture lifetime RAM STOP

**What was built / How verified:** Diagnostic single-scale correction above; production layout/polling/navigation unchanged. Release builds zero warnings/errors and 106 tests each actual x64/x86 passed. Full 100% navigation used one opaque window, 87 page visits, 201 requests/157 frames; activity, timer/tray/worker cleanup and seven-binary hash checks passed. Exactly one 860x580 PNG was exported at 343.1071631–343.1452850 seconds, with recorded dimensions independently verified from its PNG header. Capture proof passed. 150% short smoke passed previously; its full trial did not start because the 100% run failed.

**Measured RAM and CPU:** 300.001333 seconds/301 samples; observation max **88.408064 MB**, mean 82.658641 MB, p95 86.220800 MB; lifetime/tail **91.525120 MB**, exceeding unchanged 90 MB by 1.525120 MB. Average machine CPU **0.2239573382%**, core equivalent 1.7916587057% across eight logical processors. Observation-only numerical Pass but overall lifetime RAM FAIL. A read-only live check already observed this peak well before the capture step, so changing export scales alone does not provide the required margin. This does not isolate the responsible allocation phase or prove a causal saving versus earlier runs. Evidence docs/evidence/r4-dashboard/single-100-navigation-300s (raw CSV, tail CSV, metadata, hashes, probe, rejected scenario-check and one render export). No forced GC/trim or excluded helper/window costs.

**Known gaps:** Full 150% navigation, after-close and hidden trials not run after failure. Remaining wide pages/settings/theme/accessibility and physical/CI gaps persist. Source and binaries stay in this project; normal Release remains R3, newer measured build is bin/r4-capture-correction. No new dependency, system/security setting or Git action.

**Next step:** STOP. docs/R4_MEMORY_ATTRIBUTION_PROPOSAL.md proposes a bounded diagnostic-only phase recorder using existing .NET APIs, with actual working set/peak, separate managed-heap/commit metrics and module-loading checkpoints. Keep 90 MB, no memory manipulation, and report attribution before another production correction. Profiling is proposed, not implemented/approved.

## Diagnostic memory attribution complete — stopped for review

**What was built:** Approved opt-in bounded BCL memory phase recorder and source decorator, application lifecycle checkpoints and attribution-only measurement mode. Normal launches do not enable the recorder. No production UI correction or dependency added. Detailed findings and the next proposed controlled comparison: docs/R4_MEMORY_ATTRIBUTION_FINDINGS.md.

**How verified:** Isolated Release builds zero warnings/errors; 108 tests passed each on actual x64/x86 (70 Core/15 Shell/23 Windows), TestResults/r4-memory-profile-{x64,x86}. Added recorder/decorator tests. Twelve-second smoke exited naturally with 40 phase records. Full command: powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/memory-attribution-300s -Page CPU -Scenario Navigation -CapturePercent 100 -MemoryProfile -Executable src/IsletMonitor/bin/r4-memory-profile/IsletMonitor.exe. Retained 361 phase records, raw observer/tail CSV, hashes, probe and capture. Lifecycle/activity/hash/single-export checks passed, 204 requests/160 frames; process exited naturally and workers drained. AttributionOnly=true and OverallAccepted=false.

**Measured RAM and CPU:** 301.0010888 seconds/302 samples; observation max 91.099136 MB, mean 86.755437 MB, p95 88.985600 MB; complete lifetime/tail peak **97.660928 MB**, fails unchanged 90 MB. CPU **0.2524129159% machine**, 2.0193033269% core equivalent/eight logical processors. Recorder overhead included; do not compare directly to the earlier uninstrumented peak as a regression. First cap crossing was recorded by 4.471 seconds, before export. WPF drawing/provider/inventory work overlaps, so no exact PDH/DXGI resident cost or leak is proven.

**Known gaps:** Full 150%, after-close and application-hidden acceptance remain unrun after failure. Remaining wide module pages/settings/polish incomplete. Normal Release remains R3. Physical screenshots/mixed-DPI/Win10/games/accessibility/CI gaps remain.

**Next step:** STOP. Proposed two bounded 30-second provider-only diagnostic variants isolate CPU wildcard process counter initialization from WPF/inventory; no production feature removal or budget waiver. Await explicit review before that experiment or further production changes. No forced GC/trim, helper, machine setting, package or Git operation.

## Approved provider comparison complete — production correction pending

**What was built:** Explicit diagnostic-only CPU source variants with/without wildcard process counters, preserving the normal default and aggregate/per-core readings. Provider-only dispatch bypasses WPF/inventory; bounded recorder and fresh-process external comparison script retain complete tail peaks. No production behavior correction implemented.

**How verified:** Release builds zero warnings/errors; 109 tests pass each actual x64/x86 (70 Core/15 Shell/24 Windows), TestResults/r4-provider-{x64,x86}. PowerShell syntax check passed. Executed powershell -NoProfile -File scripts/compare-provider-memory.ps1 -Executable src/IsletMonitor/bin/r4-provider-comparison/IsletMonitor.exe -OutputDirectory docs/evidence/r4-dashboard/provider-comparison. Both sequential fresh x86 runs: 30 valid CPU reads/29 valid core reads, no loaded WPF, natural exit and disposal, unchanged exe/dll/json hashes. Process row totals 174 versus zero. Raw CSV/tail/phase reports and comparison retained.

**Measured RAM and CPU:** With process counters: 30.998135 s/32 samples, observation max 33.873920 MB, complete lifetime 37.736448 MB, average machine CPU 0.4347539779%. Without: 30.9955977 s/32 samples, observation max 30.380032 MB, complete lifetime 34.156544 MB, CPU 0.2898597113%. Difference 3.579904 MB in one pair; no five-minute product pass, both AttributionOnly=true/OverallAccepted=false. Without-process variant still spikes during core counter registration; shared PDH initialization is a candidate, not exact causal attribution or a proven leak. Diagnostic/report costs counted.

**Known gaps:** Earlier WPF lifetime failures remain; physical UI, full after-close/hidden/150% acceptance and remaining application pages/polish incomplete. Single ordered comparison cannot establish reproducible savings or product compliance. Normal Release remains R3.

**Next step:** docs/R4_PROVIDER_COMPARISON_RESULTS.md proposes visibility-driven core/process detail sampling while retaining aggregate metrics and all features, followed by full uninstrumented navigation at unchanged 90 MB and stop on failure. Await explicit review before production correction. No dependency/helper/system-setting/forced-GC/trim/Git action.

## Visibility correction built/tested — lifetime RAM gate still fails

**What was built:** Portable detail-interest flags; application viewport requests only intersecting core/process sections. Worker configures/rebuilds queries before reads, resetting baselines and releasing handles/buffer safely. Aggregate CPU/RAM continue at the same cadence. Stable warming sections preserve scroll access; old capability maps and superseded detail results are isolated. Popup defaults and all features retained. Report counters distinguish core/process detail requests.

**How verified:** Corrected Release builds zero warnings/errors; **112 tests passed each actual x64/x86** (70 Core/17 Shell/25 Windows), TestResults/r4-visible-corrected-{x64,x86}. Initial test wrongly assumed partially visible cores were offscreen; corrected deterministic viewport/core count, original failed TRX retained TestResults/r4-visible-x64. Mock visibility/scroll/order/disposal/no-later-work and existing navigation lifecycle tests pass; pure CPU/RAM counter-selection tests pass. Executed powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/visible-details-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-visible-corrected/IsletMonitor.exe. No memory profiling enabled. One window/87 page visits, 200 requests/156 frames; 91 core-detail requests, zero process requests in unscrolled navigation. Lifecycle/hash/activity/single-export checks pass, workers drained, opaque target, natural app exit zero.

**Measured RAM and CPU:** **300.0063513 seconds / 301 samples**; observation max **86.347776 MB**, mean 81.386826 MB, p95 85.946368 MB. Whole lifetime/tail **91.734016 MB**, fails unchanged 90 MB by 1.734016 MB; CPU **0.1269504373% machine**, 1.0156034987% core equivalent/eight processors. Observation summary Pass does not override scenario-check OverallAccepted=false for lifetime failure. Peak predates capture. No causal savings guarantee from separate builds/runs. Full source/observer/capture costs counted; no helper, forced GC/trim or excluded window.

**Known gaps:** No full static/after-close/hidden/150% trial followed failure. Scroll visibility is mock-verified; full scrolled process resource costs are not certified. Remaining application pages/settings/polish and physical mixed-DPI/Win10/games/accessibility/CI checks remain incomplete. Native source rejects invalid cores; renderer fill needs its own finite/range guard test as noted in the next proposal. Normal Release remains R3.

**Next step:** STOP. docs/R4_VISIBLE_RENDERING_PROPOSAL.md proposes omitting offscreen text/geometry construction while retaining layout/features, plus invalid core-fill guard coverage, then full navigation at the same 90 MB. Not approved/implemented. No Git/package/helper/machine-setting action.

## Viewport drawing correction completed — RAM gate fails; budget decision pending

**What was built:** Approved offscreen drawing culling in wide CPU/RAM application pages; full layout and scroll access preserved. Offscreen cached labels are removed on scroll within the existing bound. Invalid core-bar fills now require finite 0–100 values; native source filtering is backed by renderer validation. Existing data-visibility queries, popup defaults, units/intervals and all features remain. No allocation-free WPF claim.

**How verified:** Release builds zero warnings/errors; **113 tests passed each actual x64/x86** (70 Core/18 Shell/25 Windows), TestResults/r4-viewport-{x64,x86}. Added invalid-core rendering comparisons and scroll round-trip visible-content check; existing lifecycle, mock sampling/ordering/viewport/counter-plan tests remain green. Actual eight-second CPU/RAM smokes each made eight reads/seven frames, natural exit, workers drained; inspected 860x580 render exports under artifacts/r4-viewport-smoke. These are render exports, not physical DPI screenshots. Executed powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/viewport-render-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-viewport-render/IsletMonitor.exe. One window/87 page visits, 199 reads/155 frames, 90 core-detail requests/zero offscreen process requests. Lifecycle/activity/hash/single-export checks pass, opaque target, natural exit zero and workers drained. No profiling enabled.

**Measured RAM and CPU:** **300.9850745 seconds / 302 samples**; observation max **86.708224 MB**, mean 83.433865 MB, p95 86.249472 MB; whole lifetime/tail **93.814784 MB**, fails unchanged 90 MB by **3.814784 MB**. CPU **0.0940920826% machine**, 0.7527366610% core equivalent/eight processors. Observation-only summary Pass does not override rejected scenario-check lifetime gate. Peak predates capture. Separate runs do not establish exact causal memory savings/regressions. Raw observer/tail CSV, contexts, hashes, probe, capture and rejected scenario verdict retained.

**Known gaps:** No full static/after-close/hidden/150% run after failure. Full scrolled process memory, remaining application pages/settings/polish and physical mixed-DPI/Win10/game/accessibility/CI acceptance remain incomplete. Normal Release remains frozen R3. No helper, new package, machine setting, GC/trim or Git action.

**Next step:** STOP. docs/R4_WPF_BUDGET_DECISION.md proposes explicit 100 MB whole-lifetime allowance for application-use sessions while retaining 90 MB fresh bar/popup and post-close observation, or retaining 90 MB and pausing implementation. No policy/gate changes made or existing failures relabeled; user review required.

## Approved application-session budget implemented — new validation pending

**What was built:** Explicit Application observation scenario (100 MB and CPU <1%) and shared session-memory gate: application lifetime <=100 MB; fresh bar/popup <=90 MB; after-close/hidden current observations <=90 MB with prior application peak still reported against 100 MB. Dashboard/hidden wrappers record policy, retain startup/tail peaks and hash verification; hidden fixture also requires stable foreground. Historical failures untouched. No C# source/binary change from the previously built/tested 113-test viewport baseline.

**How verified:** Measurement policy/boundary suite 18 checks passes; existing generic measurement 24 and worker aggregation 8 pass (50 checks total). Syntax checks pass for changed observer/dashboard/hidden scripts. Initial sandbox attempts were denied by AuthorizationManager; normal-user execution outside sandbox passed without changing execution policy or machine settings. Existing frozen app's executed Release/113-tests-each-architecture results remain in TestResults/r4-viewport-{x64,x86}; no new C# build/test run is claimed for this scripts-only change.

**Measured RAM and CPU / known gaps / next step:** Fresh full navigation now running using bin/r4-viewport-render, evidence docs/evidence/r4-dashboard/app100-navigation-100-300s. No pass inferred from the cap approval or earlier failed trial. Continue sequential static/after-close/hidden validation only after accepted new navigation; stop on failure. Remaining pages/polish and physical hardware/CI gaps persist. No dependencies/Git/helper/system setting/GC/trim action.

## New application-budget navigation gate passes

**What was built / How verified:** Same frozen bin/r4-viewport-render app, unchanged C# source and prior executed 113 tests per architecture. New measurement policy passed 50 script checks. New full navigation command: powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/app100-navigation-100-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-viewport-render/IsletMonitor.exe. Lifecycle/activity/hash/single-export checks pass; OverallAccepted=true and AttributionOnly=false. One window, 200 reads/156 frames, natural cleanup. Old 90 MB failures unchanged.

**Measured RAM and CPU:** 300.9938242 seconds/302 samples; observation max **87.150592 MB**, mean 83.307852 MB, p95 86.130688 MB; complete reported lifetime/tail **91.389952 MB**, passes the newly approved 100 MB application-session gate. Average CPU **0.1200450295% machine**, 0.9603602359% core equivalent/eight processors. Memory-policy.json explicitly records 100 MB current/lifetime while the application is open. No pass inferred for subsequent scenarios.

**Known gaps / Next step:** Sequential static CPU, RAM (150% render export), overview, after-CPU-close and hidden-after-CPU trials started. Status artifacts/r4-app100-validation-sequence.json; stop at first failed numerical/scenario check. After-close and hidden current observation retain 90 MB. No page extension, normal Release replacement, dependencies, Git or system settings changed. Remaining scrolled/full physical DPI/hardware/CI and UI-completion gaps persist.

## Static CPU interrupted by auto-hide — resource limits passed; scenario rejected

First static CPU run under 100 MB: 300.983982 seconds/302 samples; observation max 84.844544 MB, mean 70.986094 MB, p95 84.238336 MB; lifetime/tail 88.653824 MB; average CPU 0.2005142005% machine / 1.6041136036% core equivalent. Numerical/resource policy passes, but open-view acceptance fails: EverHidden=true, application closed at 237.14273 seconds, 237 reads/236 frames, no final application capture. This mixed visible/hidden observation is not a certified 300-second CPU-page result. Exact suppression cause is not recorded; source can suppress on fullscreen, notification state, screen-off, lock or lid-close. No cause is guessed.

The sequential wrapper stopped and did not run RAM/overview/after-close/hidden. Natural exit zero, workers drained, timers/tray cleaned and hashes unchanged; memory-policy accepted, OverallAccepted=false due lifecycle/capture. Evidence docs/evidence/r4-dashboard/app100-static-cpu-300s and artifacts/r4-app100-validation-sequence.json preserved.

User then confirmed "Desktop can stay visible" for the retry. Repeat the same frozen build/policy in a fresh CPU folder, then resume the approved remaining validation only after a valid pass. No budget increase, suppression override, fake input, screen-awake setting, forced GC/trim, dependency/Git or system change. This is a scenario retry after explicit desktop clarification, not relabeling the interrupted run.

## Static CPU retry passes; RAM 150% trial running

After the user confirmed the desktop could stay visible, the fresh static CPU retry passed all numerical/lifecycle/activity/hash/capture checks: **300.9797699 s / 302 samples**, observation max **77.795328 MB**, mean 72.318501 MB, p95 74.862592 MB; complete reported lifetime/tail **87.617536 MB**; average CPU **0.2887704464% machine** / 2.3101635709% core equivalent. One held CPU page, 344 reads/343 frames, natural exit and drained workers, verified single 100% export. Evidence docs/evidence/r4-dashboard/app100-static-cpu-retry-300s; original interrupted run stays rejected.

Sequential validation moved to RAM with a single 150% render export; no new RAM result is claimed yet. Overview, post-close and hidden remain queued behind accepted prior results. App source/binary unchanged. No machine settings, fake input, GC/trim, dependency or Git action.

## Static RAM with 150% export passes

RAM held-page trial passes new application policy and all lifecycle/activity/hash/capture checks: **300.9799693 seconds / 302 samples**, observation max **81.383424 MB**, mean 73.366752 MB, p95 77.705216 MB; complete reported lifetime/tail **84.983808 MB**; machine CPU **0.1395182131%**, core equivalent 1.1161457049%. One held RAM page, 345 reads/344 frames, natural exit and workers drained. Exactly one verified 150% render export at 1290x870, not a physical 150% desktop screenshot. Evidence docs/evidence/r4-dashboard/app100-static-ram-150-300s. Overview trial now running; post-close/hidden remain pending. No new app code or settings changes.

## Static overview passes; post-close observation starts

Overview trial accepted: **300.0083663 seconds / 301 samples**, observation max **79.298560 MB**, mean 71.405609 MB, p95 79.228928 MB; whole reported lifetime/tail **79.306752 MB**; average machine CPU **0.0292960580%** / 0.2343684640% core equivalent. Expected overview activity check requires zero module reads and frames; lifecycle/hash/single 100% capture accepted. Evidence docs/evidence/r4-dashboard/app100-static-dashboard-300s.

Post-CPU-close trial now running with **90 MB current observation** and **100 MB prior application lifetime** reported separately. Hidden-after-CPU remains pending. No UI feature extension or new binary is claimed during these trials.

## Application baseline resource gates complete under approved policy

**What was built / How verified:** Explicit application 100 MB lifetime versus fresh bar/popup/post-close-current 90 MB policy; 50 measurement checks passed. All six new full baseline trials accepted. Detailed commands, raw evidence and limitations in docs/R4_APP100_VALIDATION.md. Same frozen viewport app and prior executed 113 tests each architecture; no new C# suite claimed for scripts-only policy. Invalid first CPU attempt and failed initial foreground assertion remain retained, not accepted.

**Measured RAM and CPU:** Navigation observation/lifetime 87.150592/91.389952 MB, CPU 0.1200450295%; held CPU 77.795328/87.617536 MB, 0.2887704464%; held RAM (150% render export) 81.383424/84.983808 MB, 0.1395182131%; overview 79.298560/79.306752 MB, 0.0292960580%; post-close 82.112512/86.802432 MB, 0.0299476906%; hidden 78.344192/85.884928 MB, 0.0032445732%. All observations >=300 seconds. Overview zero module reads/frames; after-close 16/15 and hidden eight/seven reads/frames unchanged from baseline. Hidden: zero metric/chart/process work, EnumWindows proof, 822 foreground checks/zero failures, workers drained, timers/tray cleanup and natural exit.

**Known gaps:** Render exports are not physical mixed-DPI screenshots; only one 125% monitor observed. Full scrolled process costs, remaining module-page/settings/theme polish, Windows 10/real games/screen readers/hosted CI remain unverified. Normal Release stays R3. Interactive fixture waited for actual own foreground; computer-use window inventory issued no click/input; complete extended tail and 420-second natural exit counted. External observer/fixture tooling is not product helper code.

**Next step:** Continue already approved wide GPU/Disk/Network/Battery application presentation and remaining settings/polish, then new tests/resource gates. Stop on a future failure. No dependency/helper/Git/system-setting/GC/trim action.

## Remaining wide module pages — build/tests/previews complete; resource gate running

**What was built:** Dedicated wide GPU/Disk/Network/Battery application pages with actual typed readings, separate adapters/physical counters/volumes, bounded lazy histories, viewport culling and explicit missing-data reasons. Compact popups remain bar-only. No public-IP or connectivity request added. See docs/R4_MODULE_PAGES_VALIDATION.md.

**How verified:** Final isolated Release builds have zero warnings/errors and 115 tests pass each on actual x64/x86 runtimes (70 Core, 20 Shell, 25 Windows). Intermediate impossible-rate history test failure retained; invalid rates now rejected before insertion and corrected suites pass. Four 12-second live previews exited naturally with drained workers; all exports visually inspected (GPU 150%, others 100%). They are render exports, not physical DPI screenshots. Private Network evidence stays ignored.

**Measured RAM and CPU:** First full new GPU application trial accepted: 300.9758841 seconds/302 samples; observation max 85.651456 MB, lifetime/tail 91.451392 MB, average machine CPU 0.1842963271%. Held GPU 344 reads/343 frames; single capture, lifecycle, hash and cleanup gates pass. Evidence docs/evidence/r4-dashboard/modules-gpu-300s. Disk/Network/Battery full trials running sequentially, stopping on first failure; no pending result inferred.

**Known gaps / Next step:** Settings behavior, light/system/high-contrast/accessibility/translation polish and hardware/physical mixed-DPI/CI gaps remain. Normal Release stays frozen R3. Complete remaining resource gates before extending settings. No dependency/helper/Git/system-setting/GC/trim action.

## Remaining wide-page resource gate passes

**What was built / How verified:** Same isolated r4-module-checked build, final 115 tests each architecture and inspected live previews. Four sequential full static application trials accepted, including lifetime/tail, activity, binary hash, capture and natural cleanup gates. See docs/R4_MODULE_PAGES_VALIDATION.md; Network raw evidence remains ignored/private.

**Measured RAM and CPU:** GPU observation/lifetime 85.651456/91.451392 MB, CPU 0.1842963271%; Disk 80.146432/88.555520 MB, 0.1992101738%; Network 78.426112/82.018304 MB, 0.2770765744%; Battery 75.685888/78.786560 MB, 0.2147804952%. All observations exceed 300 seconds with 302 samples. Application-use 100 MB and CPU <1% pass.

**Known gaps / Next step:** Continue settings behavior/pause/unit correction per docs/R4_SETTINGS_BEHAVIOR_PLAN.md, then theme/accessibility/translation polish and new measurements. Full scrolled paths/physical DPI/Win10/games/readers/hosted CI remain gaps. Normal Release frozen R3; no dependency/helper/Git/system-setting/GC/trim action.

## Settings behavior correction — tested; new measurement blocked

**What was built:** Pause now preserves application access without module queries; pending sources dispose before resume. Settings changes propagate from all entry points; module headers provide bar visibility/scoped preferences. Network bits/bytes formatting applies consistently to bar/popups/wide page without altering raw data. Compact popups remain bar-only. Full details/commands in docs/R4_SETTINGS_BEHAVIOR_VALIDATION.md.

**How verified:** Release builds zero warnings/errors, 120 tests pass each actual x64/x86 runtime (71 Core, 24 Shell, 25 Windows). Added mock pause/pending-resume/settings notification/popup rejection and formatting/redraw coverage. Measurement wrapper syntax passed; new Paused scenario requires open application and zero reads/frames. Isolated build bin/r4-settings-behavior; normal Release untouched.

**Measured RAM and CPU:** No new settings-build measurement executed. Navigation launch was blocked before execution: automatic approval review timed out, then its permitted retry disconnected before completion. This is an infrastructure review failure, not a product budget verdict. No approval bypass attempted. Prior module-page numbers remain valid only for their frozen binary.

**Known gaps / Next step:** Retry documented full navigation when automatic review is available, then paused/units/settings/after-close/hidden resource gates, followed by theme/icon/accessibility/translation polish. These changes are not resource-certified or a completed R4/R5. No dependency/helper/Git/machine setting/GC/trim action.

## Warm start — built/tested; resource gate running

**What was built:** Static shaped loading states for cold bar/application/popups/overview, shared two-minute session-only recent real readings on reopen, visible previous/refreshing labels, and immediate presentation of completed background reads without extra polling. Cache strips process/core/network-address details, bounds device arrays, expires without extending warming samples, and never invents history. Source failures replace loading honestly. See docs/WARM_START_PLAN.md and docs/WARM_START_VALIDATION.md.

**How verified:** Corrected Release builds zero warnings/errors; 125 tests pass each actual x64/x86 (73 Core, 27 Shell, 25 Windows), including expiry/privacy/invalid values, loading-state transitions, first-frame timing/cadence and disposal. Initial new-test syntax build error corrected. Two/eight-second live previews naturally exited with drained workers and inspected 100% render exports; real data was already visible in the first 1.18-second export, not claimed as cold-skeleton capture.

**Measured RAM and CPU:** New full navigation trial running in docs/evidence/r4-dashboard/warm-start-navigation-300s; no result inferred. Same approved 100 MB application lifetime, 90 MB fresh bar/popup/post-close current and CPU rules. Normal Release untouched.

**Known gaps / Next step:** Complete relevant resource gates; stop on failure. Settings/theme/accessibility/physical DPI/Win10/games/readers/hosted CI gaps remain. No dependency/helper/Git/system-setting/GC/trim action.

Warm-start interim evidence: original warm-start build navigation passed at 88.707072 MB complete lifetime / 0.2452815587% CPU (300.9933783 seconds, 302 samples), and CPU popup passed 89.305088 MB / 0.3147152551% (300.9913278 seconds, 302 samples). Popup observer corrected to count capture/shutdown tail and retain explicit policy/raw tail; historical results unchanged. Final consistency change marks valid first GPU/Disk baselines as warming, preserving recent readings during priming. Final builds/tests again pass 125 each actual architecture. Final GPU/paused/after-close sequential measurements now running under unchanged policy, no result inferred. Details docs/WARM_START_VALIDATION.md; normal Release still R3.

Final warm-start GPU trial accepted: 300.9802375 seconds/302 samples; observation max 87.134208 MB, lifetime/tail 90.726400 MB, CPU 0.1868893468%, 344 reads/344 frames, lifecycle/hash/capture/cleanup pass. First Paused attempt failed before process launch because the script on disk lacked the parameter, contradicting the earlier settings report. Corrected the script and positively verified its ValidateSet/body and syntax; preserve the rejected setup sequence. Fresh Paused retry running, AfterClose queued. No paused/resource pass or budget failure inferred from a parameter validation failure. Details docs/WARM_START_VALIDATION.md.

## Warm-start feature — scoped validation complete

**What was built:** Cold loading layouts and recent real session readings with previous/refreshing state, followed by immediate quiet presentation of completed background samples at unchanged polling cadence. GPU/Disk/CPU/network baselines remain warming; true unsupported/failed readings are explained. Cache expires after two minutes, is bounded/session-only, strips processes/core/network addresses and inserts no fabricated history. The ordinary bar is populated during rebuilds and has explicit first-sample loading marks.

**How verified:** Final Release builds zero warnings/errors, 125 tests pass each actual x64/x86 (73 Core, 27 Shell, 25 Windows). Final first-frame/cadence/privacy/expiry/loading/error/disposal coverage passes. Live previews inspected. Complete >=300-second resource runs accepted with hashes, raw observations, capture/tail and natural cleanup. Original warm-start navigation/popup and final GPU/paused/post-close/hidden results retain separate binary identities. The first Paused script setup rejection remains preserved and documented; corrected parameter metadata/body positively verified before retry. See docs/WARM_START_VALIDATION.md.

**Measured RAM and CPU:** Initial navigation lifetime 88.707072 MB / 0.2452815587% CPU; initial CPU popup lifetime 89.305088 MB / 0.3147152551%. Final GPU lifetime 90.726400 MB / 0.1868893468%; paused lifetime 77.828096 MB / 0.0350399212%, zero metric reads/chart frames; after-close current max 84.250624 MB, lifetime 86.646784 MB / 0.0434754185%, reads/frames held16/16. Final hidden current max86.548480 MB, lifetime90.091520 MB / 0.0045423004% machine CPU (0.0363384033% core), zero hidden metric/chart/process work, application reads/frames held8/8. Hidden actual-window proof, 826 foreground checks/zero failures, 420-second complete natural exit tail, workers/timers/tray/hash checks all pass. No actual game certification.

**Known gaps:** Session cache resets on app exit; cold launches use labelled placeholders, not invented diagnostics/history. Broader theme/icon/accessibility/translation polish and physical mixed-DPI/Win10/games/readers/hosted CI remain incomplete. Normal Release remains the older R3 build. Local popup probe/images with running-app names are git-ignored; numeric evidence remains reviewable. No dependency/helper/Git/machine setting/GC/trim action.

**Next step:** User can run src/IsletMonitor/bin/warm-start-final/IsletMonitor.exe to inspect the implemented behavior. Remaining UI polish continues under the existing approved scope and resource rules; no R4/R5 blanket completion is claimed.

## Small UI revision — green battery and right-aligned bar

**What was built:** Normal battery glyph fill is green in bar/popup/application, with low-battery warning colors retained. Default bar alignment is Right with existing edge padding; saved user app alignment changed from Center to Right, preserving other preferences. Existing warm-start functionality remains in the source.

**How verified:** App-only Release build in bin/bar-right-green passed with zero warnings/errors. Eight-second live bar/Battery application preview exited naturally with workers drained; both render exports visually inspected at artifacts/bar-right-green-preview. Right-edge readings and green fills observed. Popup source change built but was not separately previewed. See docs/BAR_VISUAL_TWEAK_PLAN.md.

**Measured RAM and CPU:** Not repeated, explicitly per user's UI-only request. Prior build-specific resource results remain unchanged; no new numerical resource acceptance claimed.

**Known gaps / Next step:** Full suites intentionally not rerun. Exit old Tidlet via tray and run src/IsletMonitor/bin/bar-right-green/IsletMonitor.exe to use this revision. Normal Release not overwritten. No dependency/Git/Windows machine setting action; only the requested application preference was saved.

## UI-only traffic spacing revision

**What was built:** Red upload dot on the top row and blue download dot below, both positioned close to the speed column. Idle dots retain dimmed matching colors. Speed values keep their fixed right edge and other bar items retain their layout. Reused frozen brushes; no metric/cadence/settings change. See docs/NETWORK_BAR_SPACING_PLAN.md.

**How verified:** App-only Release build in bin/bar-traffic-dots passed with zero warnings/errors. Six-second live preview exited naturally, timers stopped and tray removed; artifacts/bar-traffic-dots-preview/bar.png visually inspected. Full suites explicitly not rerun per user request.

**Measured RAM and CPU / Known gaps:** Five-minute trials not repeated for this UI-only tweak; no fresh resource claim. Other previously documented UI/hardware gaps remain.

**Next step:** Exit the previous instance and run src/IsletMonitor/bin/bar-traffic-dots/IsletMonitor.exe. No dependency, Git or Windows settings action.

## Undo latest traffic-dot UI change

**What was built:** User requested undo. Restored original network dot spacing and gray idle colors, including prior drawing helper. Kept the earlier green battery and right alignment. Updated README and marked traffic spacing plan reverted; historical evidence retained.

**How verified:** App-only Release rebuild in bin/bar-traffic-reverted succeeded with zero warnings/errors. Full tests, visual smoke and performance measurements not repeated for this exact source-level UI reversion. The earlier bar-right-green build remains available with its original short visual verification.

**Measured RAM and CPU / Known gaps / Next step:** No fresh resource claim. Exit the dot-spacing build and reopen bin/bar-right-green/IsletMonitor.exe for the prior appearance. No Git, dependency, settings or system action.

## Clarified UI spacing — move speed values, keep dots fixed

**What was built:** Kept traffic dots at their existing x/y positions; shifted only the right-aligned speed values 28 DIP left, giving more space before Battery. Red remains upload, blue download; dim matching idle colors. Green battery, right alignment and fixed item widths preserved. Earlier dot-moving change remains reverted.

**How verified:** App-only Release build in bin/bar-speed-spacing passed with zero warnings/errors. Six-second live preview exited naturally with timers stopped/tray removed; artifacts/bar-speed-spacing-preview/bar.png visually inspected. See docs/NETWORK_SPEED_POSITION_PLAN.md.

**Measured RAM and CPU / Known gaps:** Full suites/five-minute trials not rerun per explicit UI-only request. No fresh resource claim or unrelated change.

**Next step:** Exit previous Tidlet and run src/IsletMonitor/bin/bar-speed-spacing/IsletMonitor.exe. No dependency, Git, app-setting or Windows setting action.

## Live GPU top bar and application toggle

**What was built:** Replaced GPU Details placeholder with real usage from the existing provider on a lazy serialized worker, driven by existing visible bar cadence. Highest valid reported adapter usage for hybrid systems, separate adapter details retained. No fake zeros; warming/denied/unsupported reasons preserved. Saved sampling settings now track application/tray/import changes. GPU header toggle saves show/hide; disabling/pause/hide/exit stops/disposes bar sampling. Enabled GPU in the user's current layout after CPU. No new default module list or dependency. See docs/GPU_BAR_PLAN.md and docs/GPU_BAR_VALIDATION.md.

**How verified:** Corrected Release solution build zero warnings/errors; three sampler tests plus the GPU header toggle test pass x64/x86 test hosts. Toggle hit-area assertion then added and passed in both again via Shell-only test build. Full suites not repeated. Fourteen-second live preview shows real hybrid readings and GPU5%, naturally exited with worker drained, bar export inspected. One complete fresh GPU-enabled bar trial accepted with architecture/activity/hash/tail/policy/cleanup proof.

**Measured RAM and CPU:** 301.0005157 seconds/302 samples; observation max76.435456 MB, lifetime/tail76.439552 MB, average machine CPU0.0434748009% (0.3477984074% core/eight processors), passes fresh bar90 MB/<1%. 115 GPU/bar requests, final actualGPU5.10%, worker drained; no closed-popup chart/process work. Evidence docs/evidence/gpu-bar/live-300s.

**Known gaps / Next step:** Full suites/hosted CI, new GPU-enabled hidden/co-open-application resource trials and physical hardware scenarios remain unverified. Existing provider/bindings reused; mock stop/re-enable tests cover disposal. Exit old instance, run src/IsletMonitor/bin/bar-gpu/IsletMonitor.exe, use GPU page top-right toggle. Normal Release not overwritten. No driver/helper/new dependency/Git/system setting action; requested app item preference only.

## Application module switches: show/hide every top-bar item

**What was built:** Unified header/settings/context visibility updates; removed the last-item restriction and allowed empty layouts to persist/import. Empty bar hides and skips Reserve registration while tray/application remain available. Bar polling stops until an item is enabled; empty layout does not count as fullscreen suppression. Existing GPU/color/spacing changes retained. See docs/MODULE_TOGGLE_PLAN.md.

**How verified:** Release solution build to bin/module-toggles passed with zero warnings/errors. Four focused tests passed in each x64/x86 configuration, covering all seven header switches, empty-layout round-trip, bar hide/polling/resume and GPU toggle. Eight-second live empty-layout preview naturally exited: zero bar samples/GPU requests, eight Dashboard reads, workers drained, native Hidden=true and TrayPresent=true. CPU application render export inspected with its switch off.

**Measured RAM and CPU:** No new five-minute resource trial for this scoped correction; previous results remain tied to previous binaries. No fresh numerical resource acceptance claimed.

**Known gaps / Next step:** Full suites, physical empty-layout Reserve recovery and mixed-DPI scenarios not repeated. Exit the previous instance and run src/IsletMonitor/bin/module-toggles/IsletMonitor.exe. No Git/dependency/system-setting action or saved-user-preference change.

## Correct application switches blocked by window dragging

**What was built:** Native WM_NCHITTEST classified the toggle and settings button as title-bar drag space, swallowing real mouse input. Excluded interactive header elements from that region while retaining empty-title dragging. The earlier accessibility-only validation missed this path; the regression now checks the same native hook decision and sends WPF mouse-up to the actual hit element across all seven modules.

**How verified:** Isolated Release solution build (bin/toggle-mouse-fix) zero warnings/errors; four focused tests pass in x64 and x86 configurations. Tests cover native client/caption classification for toggle/settings/close/empty title at 100/125/150% scales with negative screen origins, all-module mouse off/on, persistence and empty-bar polling. Normal Release replacement first failed on PID 28288 file locks; user exited it, then normal Release solution build passed zero warnings/errors and the same four focused tests passed x86. No Windows tests matched this filter; not counted. Results TestResults/toggle-mouse-fix-*.

**Measured RAM and CPU:** No new five-minute resource trial for this input-only correction; no fresh resource acceptance claim.

**Known gaps / Next step:** No physical cursor injection or mixed-monitor acceptance, no broad suite rerun. Normal Release now contains the correction; prior isolated executables retain their old behavior. README run path updated. No Git/dependency/system-setting action or forced process exit. Details docs/MODULE_TOGGLE_PLAN.md and docs/API_REGISTER.md.

## Compact visibility with persistent module order

**What was built:** User clarified that remaining items must close the gap when a module is off, and re-enabled modules return to the same order. Added validated optional ItemOrder metadata, migrated lazily from existing visible order, while keeping Items as the compact enabled sequence. Header/settings/context toggles share it. Explicit Move earlier updates remembered order; restoring defaults clears the remembered order. No fixed empty slots or metric changes.

**How verified:** Release solution build to bin/toggle-order passed zero warnings/errors. Ten focused tests passed in each x64/x86 test-host configuration: seven Core policy tests plus three Shell toggle/lifecycle tests. New tests cover all alignments, compact spacing, hide/show each module, export/import, all-off then reverse re-enable, explicit reorder with hidden module, invalid order metadata. Windows assembly had no matches, not counted. Results TestResults/toggle-order-x64 and toggle-order-x86.

**Measured RAM and CPU:** No full five-minute rerun for this scoped settings/layout correction; historical measurements remain tied to their original binaries.

**Known gaps / Next step:** Physical monitor scenarios and full suites not repeated. Corrected executable bin/toggle-order ready; normal Release replacement awaiting user exit of running app. No dependency, Git, API or machine-setting action; existing saved visible order is preserved as migration baseline, not reconstructed from past toggles.

Normal Release update: after the user closed the running instance, the normal solution Release build passed with zero warnings/errors. Current executable is src/IsletMonitor/bin/Release/net8.0-windows/IsletMonitor.exe; this supersedes the pending replacement note above.

## Optional tools foundation — development stage, not five completed modules

**What was built:** Latest user request authorizes independent Screenshot/Clipboard/Git/Eye Care/AI Limits modules; subsequent clarification places icons on the LEFT and preserves monitor readings on the right. Added optional tool settings/order, compact strip/physical-space overflow, tool-specific context menus, shared lightweight tool surface and application Tools pages, lazy backend lifecycle and suppression. Manual AI percentage/reset input with own JSON persistence is implemented. Git has an experimental read-only CLI/watch backend; it is not qualified by malicious-repository/live acceptance yet. Clipboard secret/history policies, usage math, Git porcelain parsing, solid pixel redaction and display reset exist as portable tested building blocks. Screenshot/Clipboard/Eye Care native backends explicitly report implementation pending. No tool starts by default. See docs/TOOLS_IMPLEMENTATION_PLAN.md and docs/TOOLS_API_REGISTER.md.

**How verified:** Intermediate tools-dev Release build zero warnings/errors; active suite passed 144 tests (86 Core,27 Windows,31 Shell) x64. Eight-second owned preview with synthetic manual42%/68% showed individual icons and Manual popup; naturally exited, one open/close, timers stopped/tray removed, no fullscreen interruption. That preview preceded the left-side correction and card contrast improvement. Final tools-foundation Release build zero warnings/errors; 144 tests passed x86; final x64 and resource gates pending at this entry. No real clipboard, private logs, desktop capture, display change, paste, Git mutation or Git subprocess used in tests. Normal Release remains the previous monitor build.

**Measured RAM and CPU:** Not yet measured for this development stage; older monitor measurements do not certify tools. A bounded five-minute fresh tool-popup gate is next, before adding heavier native backends.

**Known gaps / Next step:** Remaining requested tool actions/backends, log formats, hotkeys, image editor/pins, encryption, schedules, live Git security acceptance, full light/system themes and physical hardware validation remain pending. This scaffolding must not be advertised as the completed feature set. No dependency/Git/system-setting action; no normal Release overwrite during feasibility.

Foundation resource trial manual-popup-300s: 300.9694492 seconds/302 samples, observation max78.413824 MB/lifetime79.339520 MB/machine CPU0.0772244079% (0.6177952629% core/eight processors), numerical resource limits pass. Pure held-popup lifecycle FAIL: seven popup opens/closes from recorded manual bar interactions, with final popup closed184.867s. This does not certify a continuously open popup. Retained raw evidence, no relabeling. Correct diagnostic-only bar mouse/key routing and rerun in fresh directory; normal app stays interactive. No budget failure or relaxation.

Accepted foundation rerun: scripts/measure-tools.ps1 Scenario Popup (tools-foundation x86 Release) held exactly one Manual popup, both diagnostic input holds active, 300.9864851 s, x86/natural exit/tray cleanup and unchanged binary hashes. Current max 78.409728 MB, lifetime 78.417920 MB, average machine CPU 0.0356899330%. Passed 90 MB/<1%; evidence docs/evidence/tools-foundation/manual-popup-held-300s. Full 144 tests passed both x64/x86 on the preceding foundation build; 15 focused tests passed x86 after the diagnostic-only input correction. No completion claim for the five modules or their resource costs.

## Monitor-only cleanup — 10 November 2026

**What was built:** User cancelled the five-tool extension. Removed Screenshot, Clipboard history, Git, Eye Care and AI Limits source, UI and diagnostic/test entry points. Restored monitor-only sidebar and bar. The monitor's current alignment, remembered item order, readings, popups and settings remain. Legacy Tools fields are ignored and omitted on export/save. Historical evidence is retained; user files and preferences were not rewritten.

**How verified:** Isolated monitor-only Release solution build passed with zero warnings/errors. `dotnet test` passed 135 active tests in each x64/x86 configuration (80 Core, 30 Shell, 25 Windows), including migration, monitor order, toggles and popup lifecycle. A bounded eight-second live fixture loaded old settings with every retired tool enabled, kept CPU/GPU/RAM/Network/Battery in order, and exited naturally with eight Dashboard reads, drained workers and tray removed. Visual inspection of bar and Dashboard render exports confirmed there are no tool icons or tool pages. See docs/MONITOR_ONLY_CLEANUP.md.

**Measured RAM and CPU:** Fresh GPU-enabled x86 bar passed: 300.996924 seconds, 302 samples, observation max 76.795904 MB, lifetime peak 76.800000 MB, average machine CPU 0.041529%. No Dashboard opens, closed-popup chart or process work, fullscreen interruption, or cleanup failure. Evidence: docs/evidence/monitor-only/gpu-bar-300s.

**Known gaps / Next step:** Normal Release build passed with zero warnings/errors. All seven runtime files match the measured build by SHA-256. Reopen the normal Release executable. This run validates the bar, not a new full application/popup resource matrix. No dependency, Git mutation, machine-setting change, real capture, clipboard history collection or user-file deletion.

## Blue glass monitor popup appearance — 10 November 2026

**What was built:** Restyled the monitor detail popups with a navy translucent WPF surface, thin borders, inset cards, brighter gradient gauges and chart traces. Follow-up made the actual per-pixel background visibly translucent while preserving opaque text and metric accents. Removed the unused decorative gear glyph. Existing monitor features, data, popup controls, bar and Dashboard retained. No dependency, wallpaper capture, blur, new tool, Git operation or system-setting action. See docs/POPUP_GLASS_REFRESH.md.

**How verified:** Both isolated Release builds passed with zero warnings/errors; four focused x86 popup drawing/lifetime tests passed for each. All seven module previews exited naturally in the first build. Revised CPU/RAM previews exited cleanly at both render scales with background alpha182/255. No new full test matrix.

**Measured RAM and CPU / Next step:** First theme trial stopped after 249 samples and 85.06 MB observed peak; incomplete, not accepted. Final translucent CPU popup trial in progress at unchanged 90 MB. Update normal Release only after the final resource gate. No physical monitor/DPI or compositor blur claim.

## Taskbar-style bottom placement — 10 November 2026

**What was built:** Bottom now uses a compact 34-DIP navy glass strip, bounded live CPU sparkline, GPU meter, colored traffic and green battery. Overlay uses the selected monitor work area, with a gap above the native taskbar. Top placement, saved order, monitor scope and controls remain. Popups use the translucent style and open upward. No Windows shell replacement, dependency, system-setting or Git change. See docs/BOTTOM_MONITOR_BAR.md.

**How verified:** Isolated Release builds passed with zero warnings/errors; 17 focused x86 tests passed. Live 12-second preview verified native placement and popup bounds at 125%, with GPU readings, popup frames and clean shutdown. No full suite or new Reserve/auto-hide hardware proof.

**Measured RAM and CPU:** Combined live-GPU bottom strip and translucent CPU popup ran 300.9672224 seconds (302 samples). Observation max 89.661440 MB; whole-process lifetime peak 92.348416 MB, over 90 MB. Average machine CPU 0.320581%. Lifecycle, native position, shutdown, GPU worker cleanup and hash checks passed. Overall gate failed, exit 2. Evidence: docs/evidence/taskbar-glass/bottom-cpu-300s. Both earlier incomplete popup trials retain their original results.

**Known gaps / Next step:** Normal Release remains unchanged. Stop per AGENTS.md; see docs/TASKBAR_GLASS_MEMORY_REVIEW.md for a same-cap comparison. No test pass is claimed for the resource gate, no further work was run after detecting the overage, and the test build remains isolated.

## Top-edge hover reveal — 10 November 2026

**What was built:** Added an opt-in **Top · reveal on hover** placement. The glass strip drops when the pointer reaches the selected screen's top edge and retracts after it leaves. An open popup holds the strip visible. Retracting all strips stops bar sampling. Saved placements remain valid.

**How verified:** Isolated Release solution build passed with zero warnings or errors. Twenty focused x86 tests passed. The native 125% DPI probe verified the popup opens below the strip and the fixture exits naturally with workers drained. WPF render exports and measurements are in docs/evidence/top-edge-reveal/cpu-300s.

**Measured RAM and CPU:** Over 300.997 seconds with 302 samples, the live-GPU strip with CPU popup open averaged 0.377002% machine CPU and reached 89.141248 MB during observation. Lifetime RAM peaked at 91.725824 MB against the 90 MB cap. The overall gate failed with exit 2.

**Known gaps / Next step:** Normal Release is still the prior build. Real mixed-DPI desktop behavior and sustained hidden CPU were not measured. See docs/TOP_EDGE_REVEAL.md for the proposed same-cap provider comparison.

## Waveform application icon — 10 November 2026

**What was built:** Embedded the supplied waveform artwork in the executable and registered its small icon in the system tray. The ICO contains ten sizes from 16 to 256 pixels.

**How verified:** Isolated Release solution build passed with zero warnings or errors. Windows extracted identical 16×16 pixels from the ICO and the compiled apphost. All four focused BarNativeTests passed. The six-second x86 smoke probe registered the tray icon, removed it on exit, and exited naturally.

**Measured RAM and CPU / Known gaps / Next step:** No new performance run for the icon change. The normal Release executable remains held at the earlier top-edge memory gate. The verified icon build is in src/IsletMonitor/bin/app-icon.

## Dashboard glass styling — 10 November 2026

**What was built:** Dark navy, teal and violet glass styling for Dashboard, Settings and application module cards. Added a cyan navigation marker and a generic device illustration with real system information below. Gradients are frozen and static on the existing opaque host.

**How verified:** Isolated Release solution build passed with zero warnings or errors. All 16 active x86 ApplicationPageTests passed. Native x86 Dashboard, CPU and Settings probes exported 100% and 150% renders and exited naturally.

**Measured RAM and CPU / Known gaps / Next step:** The resource script refused to start while the prior app-icon build was running. The visual refresh is available in src/IsletMonitor/bin/dashboard-glass. No performance pass or normal Release promotion is claimed.

## Charcoal monitor popups — 10 November 2026

**What was built:** Applied an opaque charcoal panel, thin gray borders, left-aligned headings, and solid gauge and chart accents to the monitor popups. RAM shows physical and reported commit bars, and Battery uses a compact charge graphic.

**How verified:** Isolated Release solution build passed with zero warnings or errors. Four focused x86 tests passed. Seven x86 popup probes rendered both scales and cleaned up normally. See docs/POPUP_CHARCOAL.md.

**Measured RAM and CPU / Known gaps / Next step:** No fresh five-minute measurement because the existing Tidlet instance was running. Preview build in src/IsletMonitor/bin/popup-charcoal. Normal Release was not promoted.

## Charcoal Dashboard — 10 November 2026

**What was built:** Solid charcoal sidebar, content and module cards with thin gray borders, a blue selected navigation row, and hardware inventory accents. The Dashboard retains real device information and the existing opaque window host.

**How verified:** Release solution build completed without warnings or errors. Sixteen focused x86 ApplicationPageTests passed. Dashboard, CPU and Settings rendered at 100% and 150% and closed naturally.

**Measured RAM and CPU / Known gaps / Next step:** The prior Tidlet instance was running, so the five-minute trial was skipped. The visual refresh is available in bin/dashboard-charcoal. See docs/DASHBOARD_CHARCOAL.md.

**Follow-up validation:** All 139 active x86-configured tests passed. The 301.003-second navigation run measured 76.80 MB observed maximum, 79.18 MB lifetime peak, and 0.0435% machine CPU. Environmental suppression closed the Dashboard at 10.00 seconds, with four reads and four frames. Scenario failed exit 2 despite passing the numerical limits. See docs/evidence/dashboard-charcoal/navigation-300s. Repeat with the desktop visible and awake for the entire run.

## Charcoal monitor strip — 11 November 2026

**What was built:** Matched the charcoal reference with a CPU ring, GPU history line, amber-to-red RAM meter, red/blue traffic dots, and green battery. The meters fit all existing placements and fall back to compact text when space is limited.

**How verified:** Isolated Release solution build completed without warnings or errors. Seventeen focused x86 tests passed. Top, bottom, and hover-reveal previews rendered with live GPU data at 125% DPI. The hover CPU popup opened below the strip.

**Measured RAM and CPU:** The live-GPU bottom strip passed the bar-only gate. Lifetime peak was 78.147584 MB and average machine CPU was 0.054506% over 301.001 seconds and 302 samples. See docs/evidence/charcoal-monitor-strip/bottom-gpu-300s.

**Known gaps / Next step:** The isolated preview is available at src/IsletMonitor/bin/charcoal-strip. This pass does not replace the earlier failed hover-popup or interrupted Dashboard runs. Normal Release was not promoted.

## Light mode — 11 November 2026

**What was built:** Wired the existing Light preference into the strip, Dashboard, module cards, and monitor popups. Light uses pale neutral surfaces, dark labels, blue selection, and contrast-adjusted charts.

**How verified:** Isolated Release solution build passed without warnings or errors. All 32 x86-configured shell tests passed, including an in-place theme change. Five native Light previews opened and exited cleanly.

**Measured RAM and CPU:** The five-minute trial reached 92.139520 MB observed and 94.711808 MB lifetime, with 0.208329% average machine CPU. It exceeded 90 MB and also failed bar-only scenario validation after Network, GPU and RAM popups opened. Evidence is under docs/evidence/light-mode/bottom-gpu-300s.

**Known gaps / Next step:** The verified visual preview is in bin/light-mode. Normal Release was held. System still uses the dark fallback. See docs/LIGHT_MODE.md for the proposed controlled comparison.

## Desktop settings autosave — 11 November 2026

**What was built:** Made automatic saving explicit in Dashboard Settings and report rejected settings without claiming success. The same charcoal design is used in the current Dark strip across placements.

**How verified:** Isolated Release build passed with zero warnings or errors. Ten focused x86 tests passed, including changing Theme through the open Dashboard, reloading it from disk, and verifying placement and module order. Native Top, hover-reveal and Bottom previews confirmed the charcoal background and clean shutdown. The running older Dashboard preview explained why the newer strip was not visible.

**Measured RAM and CPU / Known gaps / Next step:** This follow-up did not run a new five-minute trial. Use src/IsletMonitor/bin/settings-preview for the preview. The earlier Light-mode resource verdict and normal Release remain as recorded.

## Windows power mode — 11 November 2026

**What was built:** Added a Power mode selector to the Dashboard Battery page with a Windows Settings fallback. It shows the configured mode and rechecks the displayed power source before applying a user selection.

**How verified:** Isolated Release build passed without warnings or errors. All 18 focused x86 application-page tests passed. A read-only Windows preview displayed the current plugged-in mode at 100% and 150% render scales. Automated change and fallback actions used a fake.

**Measured RAM and CPU / Known gaps / Next step:** No new five-minute trial while the existing Tidlet instance was running. The native setter and Settings launch were not exercised on this machine. See docs/POWER_MODE_CONTROL.md and the isolated bin/power-mode preview.

## Monitor UI audit — 11 November 2026

**What was built:** Fixed strip resize and TwoLine behavior, popup scrolling and offscreen drawing, repeated Sensors reads, popup source cleanup, Dashboard row hit targets, keyboard navigation and history resets during visibility changes. Updated the README and screenshots.

**How verified:** Isolated Release solution build passed without warnings or errors. All 143 active tests passed in both x86 and x64 configurations. The 40-case native x86 render matrix passed across Dark and Light. Power actions were tested with mocks.

**Measured RAM and CPU:** No fresh five-minute trial because the older Tidlet instance was running. Short CPU popup presentation allocations fell from 1,337,840 to 1,175,076 bytes in Dark and 1,383,952 to 1,157,112 bytes in Light. Sensors stopped after one frame in both themes.

**Known gaps / Next step:** Run fresh visible bar, popup and Dashboard budget trials with the earlier instance closed. See docs/UI_AUDIT.md. The audited build is isolated in src/IsletMonitor/bin/ui-audit.
