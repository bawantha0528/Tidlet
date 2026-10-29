# M0–M3 acceptance matrix

Date: 29 October 2026. M0 has local evidence. M1 native shell is implemented with 44 passing tests; 3 trials passed (Island, Notch, Hidden); visible and fullscreen trials require an active interactive desktop. Later milestones remain pending. Requested workspace placement is verified. Full M0 acceptance still needs an authorized hosted CI run. Add exact commit/build/test/run/device evidence before changing a status to Pass. “Simulated” proves logic only and never satisfies a physical-device check.

| ID | Requirement / cases | Planned evidence | Current status |
|---|---|---|---|
| P0 | AGENTS and detailed plan before feature code | All 56 transfer files hash-verified and relocated directly into Tidlet; Release build/tests rerun here | Pass: canonical project root verified |
| M0.1 | Four correct projects/reference direction; Core portable | Release build, architecture assertions | Pass: Release build and foundation tests |
| M0.2 | Real dotnet test discovery/execution, zero-test failure | Nonzero test counts, TRX, verifier exit code | Pass: 14 real tests / three suites under M0 go-ahead |
| M0.3 | GitHub Actions build/tests pass | Actual authorized repository run URL and artifact | Configured with pinned actions; hosted run pending |
| M0.4 | MIT, EditorConfig, README/rules/progress/decisions | Source review and repository inventory | Pass: files implemented and inspected |
| M0.5 | Measurement math/identity/failure behavior | Fixtures, natural-exit fixture, 300-s scaffold baseline | Pass: 24 tooling checks and 300-second baseline |
| M1.1 | Island/Notch/Bar × Collapsed/Hover/Expanded and a real live stat | State tests + actual desktop screenshots/interaction | Implementation complete (Island, Notch, Bar; Collapsed, Hover, Expanded); 300-s trials for Island & Notch pass; Bar & Expanded trials pending unsuppressed desktop |
| M1.2 | No focus steal, toolwindow/Alt+Tab/taskbar absence | Live foreground HWND/style inspection + manual observation | Pass in unit & smoke tests: native styles (WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_LAYERED) and ForegroundEverIsland = false verified in all trials |
| M1.3 | Real cross-process click-through and hover/click recovery | Underlying app interaction + tray/hotkey recovery | ClickThrough flag verified in collapsed trials; Tray icon and Ctrl+Alt+I recovery implemented and tested |
| M1.4 | PMv2, two monitors with distinct scale, negative coordinates | Physical two-monitor evidence; scale/topology checks | Partial: PMv2 manifest, DPI scaling and signed coordinate math pass; physical dual-monitor mixed-DPI checks pending hardware |
| M1.5 | Display unplug/replug, work area/taskbar, Explorer tray recovery | Real or safely reproducible desktop checks | Display topology change & taskbar message handling implemented; physical unplug/replug not tested |
| M1.6 | Fullscreen suppression/restoration, including borderless | SHQuery state tests + real app cases | Partial: SHQueryUserNotificationState suppression implemented; Hidden scenario verified (0 work); Fullscreen fixture requires active desktop foreground |
| M1.7 | Finite 200–300-ms transitions, no resting frame loop | State/clock lifecycle tests + observable callbacks | Pass in unit tests (IslandTransitionTests, NativeRendererTests); no frame timer when collapsed or hidden |
| M1.8 | Shell memory/CPU budgets (Native Win32 layered) | Raw 300-s samples for every style/scenario | Partial: Collapsed Island PASS (36.63 MB, 0.029% CPU); Collapsed Notch PASS (39.66 MB, 0.060% CPU); Hidden PASS (27.81 MB, 0.000% CPU); Bar, Expanded, Fullscreen, PostExpansion pending interactive desktop |
| M2.1 | CPU/RAM correctness, first sample/reset/nonfinite/denial | Pure tests + standard-user live source check | Pass in unit tests (MetricMathTests, WindowsProviderTests); live GlobalMemoryStatusEx verified |
| M2.2 | Network physical/virtual/no-duplicate deltas | Topology fixtures + available real interfaces | Pass in unit tests; live GetIPStatistics verified |
| M2.3 | Battery absent, partial fields, signed rate/health validation | Fixtures + real battery where available | Pass in unit tests; live GetSystemPowerStatus source verified |
| M2.4 | Disk PDH existence/status/warmup/reset | Counter fixtures + real standard-user counters | Pass in unit tests; delta throughput calculation verified |
| M2.5 | GPU absent/single/hybrid, correct adapter grouping | Identity/engine fixtures + real hybrid hardware | Pass in unit tests; DXGI adapter enumeration verified |
| M2.6 | Ping/jitter, WLAN permission/disconnect/unavailable | Mocks + explicit optional live observation, no surprise prompts | Pass in unit tests; async ping + JitterCalculator verified |
| M2.7 | Thermal fallback truthful or Unsupported | Official contract verification + available hardware | Unsupported (no-driver ACPI thermal-zone not present) |
| M2.8 | Top process exit/denial/PID reuse; confirmed kill path | Mock executor; no real process termination | Pass in unit tests (ProcessTerminationPolicy, mock WindowsProcessExecutor); critical processes protected |
| M2.9 | One scheduler; visible demand; hidden/power/saver/resume | Fake-clock/provider lifecycle tests + actual power events where available | Pass in unit tests (M2HostTests); collapsed mode polls only CPU; expanded polls visible metrics |
| M2.10 | Real-provider budgets and zero hidden metric work | Raw 300-s samples + counters | Pending full unsuppressed desktop trial runs |
| M3.1 | Every requested customization; all four edges | Schema/view-model tests + real UI interaction | Pass in unit tests (M3SettingsAndBehaviorTests, IslandGeometry); Top/Bottom/Left/Right placement verified |
| M3.2 | Restart persistence, corrupt JSON, backup/future schema | Temp-directory tests + actual app restart | Pass in unit tests (FileSettingsStore); atomic swap, backup, corrupt copy, future schema protected |
| M3.3 | Full settings/theme/layout export/import, profiles/alerts/hotkey, bounds and atomicity | Fixtures, malicious/oversize input, failed save/rollback | Pass in unit tests (SettingsEnvelope); size limit, roundtrip, declarative envelope verified |
| M3.4 | Configurable hotkey/conflict rollback | Mock native tests + actual successful registration | Pass in unit tests; hotkey chord validation & fallback verified |
| M3.5 | Heat/battery/ping alerts, unavailable/stale/DND/suppression | Deterministic fresh/stale threshold fixtures | Pass in unit tests (AlertEngine); battery low threshold, hysteresis, cooldown, DND suppression verified |
| M3.6 | Gaming/work/meeting profile rules and manual priority | Pure rules tests; no system-setting execution | Pass in unit tests (ProfileManager); precedence, debounce, pure Tidlet policy verified |
| M3.7 | Post-use retention plus representative max geometry/opacity/widgets/edge/DPI budgets | Raw 300-s Collapsed and Expanded/hidden/fullscreen runs | Not measured |
| M3.8 | Stop after M3 | Final report, complete gaps/evidence list, no M4 work | Future gate |
| H1 | Windows 10 and Windows 11 coverage | Real systems/VMs identified as such | Not tested |
| H2 | Comfortable on an actual 8 GB reference machine | Same acceptance scenarios and RAM context | Not tested; hardware unconfirmed |
| H3 | No driver/admin/default external traffic/telemetry | Source/publish dependency audit + standard-user live observation | Not tested |

Minimum scenario outcomes: record Pass, Fail or Not tested. Do not convert Not tested to Pass because an automated fixture or a different machine passed.

Architecture evidence must distinguish x64, x86 and deferred ARM64. M0 compiled x64/x86; M1 unit tests ran x64; x86 execution remains untested.

M0 actual source/evidence: see [PROGRESS](../PROGRESS.md); raw measurements live in docs/evidence/m0.
