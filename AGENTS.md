# Tidlet Monitor — engineering agreement

## Scope and plan-first rule

Build an MIT-licensed Windows 10/11 system monitor with C#/.NET 8, WPF and verified Win32 interop. Implement M0–M3 only; stop after M3 for user review. The user requested a full detailed plan first: read `docs/IMPLEMENTATION_PLAN.md` before writing feature code. M4–M7 remain deferred.

## Non-negotiable rules

- No new dependency without explicit user approval. Record purpose, alternatives, exact verified version, license, transitive graph and approval in `DECISIONS.md` before adding it. This includes test frameworks, SDKs and development tools.
- No default admin rights or driver. Never bundle WinRing0. Optional hardware helper work begins in M4.
- Never run commands that change this machine's system settings. All fixer and process-termination tests use mock executors; do not terminate real processes while testing.
- Verify APIs, counters, platform limits and versions against official current sources. Record date and source. Mark unresolved details `TODO(verify)`.
- Never claim a build, test, hardware scenario, CI run or performance measurement succeeded unless it was executed. Skipped/unavailable checks remain explicit gaps.
- No accounts, messaging, cloud sync, telemetry, phone-home updates, agent/broker/dev-tool features, registry cleaners or macOS work.
- Missing, denied, stale, failed and invalid readings need explicit states and plain-English reasons. Never substitute zero.
- The user owns all Git operations. Do not stage, commit, amend, initialize Git, or push unless explicitly requested for that specific operation. Preserve user changes; read-only Git inspection is allowed.

## Architecture

- `src/Islet.Core`: portable models/contracts, metric math and validation, versioned settings/theme schemas, profile/alert rules, inert fixer/mood schemas; no Windows APIs, WPF or executors.
- `src/Islet.Shell`: WPF island, styles/states, themes, animation, lazy widget view host, settings UI. `IWidget` has id/name/view/update/enabled here so WPF stays out of Core.
- `src/Islet.Windows`: native bindings, metric probes/providers, display/power/tray/fullscreen/hotkey adapters and mockable Windows action boundaries.
- `src/IsletMonitor`: app entry and composition root.
- `tests`: pure/Windows tests, fixtures, fake providers and mock executors.
- `scripts`: repeatable validation and performance measurement, read-only toward system configuration.
- `docs`: verified API register, detailed plan, acceptance matrix, evidence and limitations.

Every provider probes `Supported | Unsupported(reason) | NeedsHelper(reason)` at startup. Runtime samples separately represent validity, stale values and transient failures. Widgets lazy-load and unsubscribe when invisible. Settings/themes use validated versioned JSON in `%APPDATA%\IsletMonitor`, with import/export and corruption recovery.

## Performance gates

- Collapsed idle: working set <= 60 MB; average process CPU < 1% over 300 seconds.
- Expanded graphs: working set <= 100 MB.
- Hidden/fullscreen: approximately zero CPU; report precise numbers and timer/provider activity.
- Do not game measurements with forced garbage collection, working-set trimming or excluded child costs.
- If WPF transparency cannot meet budgets, STOP, provide actual numbers and a renderer proposal, and wait for user direction.
- Run `scripts/measure.ps1` after each milestone. Missing SDK, hardware, interactive desktop or remote CI is a gap, not a pass.

## Milestones and reporting

1. M0: scaffold solution/projects, rules/plan/progress/decisions, EditorConfig, MIT, README, build/test GitHub Actions, measurement script.
2. M1: three runtime styles and states, no-activate/toolwindow/click-through, tray, multi-monitor/DPI/fullscreen support, bounded animations and measurement gate.
3. M2: Tier 1 providers/widgets, validated math, adaptive polling and failure fixtures; confirmed process-termination flow tested only with mocks.
4. M3: customization, persistence/recovery, export/import, profiles, hotkeys and alerts. STOP for review.

After every milestone: Release build, actual tests, 300-second measurement runs, then update `PROGRESS.md` and report the changes. DO NOT stage or commit: the user will commit manually every time. Do not mark incomplete acceptance gates complete.

Report fields: **What was built / How it was verified (commands run, results) / Measured RAM and CPU / Known gaps / Next step**.

Target commands (not execution claims; M0 must create their inputs first):

```powershell
dotnet build TidletMonitor.sln -c Release
dotnet test TidletMonitor.sln -c Release --no-build
pwsh -NoProfile -File scripts/measure.ps1 -ProcessId <pid> -Scenario Collapsed -DurationSeconds 300
```

Use available PowerShell if pwsh is missing. See the detailed plan for scenario prerequisites and CPU normalization.

Ask only for new dependencies, admin actions, blown budgets or scope changes; decide routine implementation details and record them. Do not alter machine security settings to fix a workspace access problem.

## Active project location — user instruction

Use `C:\Users\bawan\OneDrive\Documents\Tidlet` as the sole development folder. All active source files and TidletMonitor.sln are directly in this folder. The earlier output repository and ZIP are reference backups only. Do not stage, commit, initialize Git or push; the user performs Git operations manually. If Windows blocks file access, report it without changing security settings or developing elsewhere.

## Approved M1 renderer exception

The user approved docs/M1_RENDERER_PROPOSAL.md after two transparent WPF working-set failures. Native C#/.NET 8 startup and Win32 layered island rendering are now authorized under docs/M1_NATIVE_PLAN.md. Shell presentation uses portable platform contracts; Windows owns Win32/GDI. Preserve WPF legacy experiments and avoid WPF initialization in the ambient host. Keep budgets unchanged and count every auxiliary window/process. This overrides the original WPF-only ambient-shell description, not the dependency, test, safety or manual-Git rules.

## Approved dashboard presentation revision

The user requested a Stats-inspired information layout with original colors on their M3 branch. Follow docs/UI_REDESIGN_PLAN.md: native overview/module pages, capability-driven real data and preserved island/notch/bar modes. Do not reintroduce representative disk increments or an unqueried GPU adapter. GPU/advanced sensors remain explicitly unavailable until genuine providers exist. This revision does not start M4-M7 or certify all earlier acceptance gates.

Additional validation: scripts/smoke-dashboard.ps1 renders eight pages in two themes and exits naturally; scripts/measure-native-shell.ps1 measures the full app. Pure collapsed/expanded trials reject unexpected state/style history. Record module navigation within expanded trials, retained raw CSV, binary hashes and physical-hardware gaps. Render-buffer previews are not desktop screenshots. A successful local run is not a hosted CI pass. Continue the manual-Git policy.
## Approved Notch media/focus extension

The user approved music controls and a focus timer for the compact black Notch UI, and explicitly approved Microsoft.Windows.SDK.NET.Ref 10.0.17763.56. Follow the appended control-center plan in docs/UI_REDESIGN_PLAN.md and the dependency/runtime/license inventory in DECISIONS.md. This authorizes the named extension, not M4-M7 or additional dependencies. Keep Windows media APIs in Islet.Windows, load the projection lazily, use mock executors for every automated playback command, preserve the full dashboard, and start no media polling when its card is invisible. The deadline-based focus timer requires no hidden ticking. Validate scripts/smoke-control-center.ps1 and repeat expanded Notch performance measurement. Keep all Git operations manual.

The user approved docs/MEDIA_MEMORY_PROPOSAL.md's demand-started music-worker implementation by saying "do next step". Reuse the approved package/executable, initialize WinRT only in the worker, exit naturally when invisible, test playback only via mocks, and measure aggregate parent/worker costs. No new dependency or M4 hardware-helper work is authorized. Budgets and manual-Git rules remain unchanged.
