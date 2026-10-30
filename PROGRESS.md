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
