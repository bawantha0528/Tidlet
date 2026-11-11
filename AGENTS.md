# Active replacement brief - 30 October 2026

10 November latest scope: system monitoring only. Remove the optional Screenshot, Clipboard, Git, Eye Care and AI Limits extension. Preserve monitor readings, alignment and remembered compact order, monitor popups, Dashboard, and Settings. docs/MONITOR_ONLY_CLEANUP.md supersedes the historical tools plan. Budgets, manual Git, no dependencies, and no machine-setting changes remain in force.

31 October latest user approval: application-use sessions may reach 100,000,000 bytes whole-process lifetime RAM (docs/R4_WPF_BUDGET_DECISION.md). Fresh bar/popup sessions retain 90,000,000 bytes; after application close current observation RAM retains 90,000,000 bytes while prior lifetime remains reported against 100 MB. CPU/hidden-work/accounting rules unchanged. Explicitly implement/test/report policy before sequential navigation/static/after-close/hidden validation. Preserve old failed verdicts. Stop on a future failure; no package/helper/Git/system-setting/GC/trim permission.

31 October latest approval: execute docs/R4_VISIBLE_RENDERING_PROPOSAL.md's bounded viewport drawing correction and invalid core-value coverage, preserve scrolling/features and 90 MB, then build/test and full unprofiled navigation. Stop on another failure. No package/helper/Git/system setting or GC/trim action.

31 October latest approval: implement docs/R4_PROVIDER_COMPARISON_RESULTS.md's visibility-driven core/process detail correction, preserving aggregate metrics, all features and 90 MB. Build/test, full uninstrumented navigation with complete lifetime accounting; stop on failure. No new dependency, helper, Git, GC/trim or machine setting.

31 October latest approval: execute the two bounded 30-second provider-only diagnostic variants in docs/R4_MEMORY_ATTRIBUTION_FINDINGS.md, with/without CPU process counters and no WPF/inventory. Keep 90 MB unchanged; both runs are attribution-only, report findings before production changes. No package/helper/Git/system-setting/forced-GC/trim permission.

31 October latest approval: execute docs/R4_MEMORY_ATTRIBUTION_PROPOSAL.md's diagnostic-only BCL phase profiling at unchanged 90 MB. One bounded full navigation attribution run, including recorder costs; no forced collection/trim, dependency, helper, Git or system setting. Report findings/proposal before further production changes; profiling is not application acceptance.

31 October continuation: user said "countinue" after the pending docs/R4_CAPTURE_MEMORY_PROPOSAL.md review. Proceed with that bounded single-scale diagnostic correction and sequential full 100/150 navigation trials at unchanged 90 MB. Count all capture/startup/tail costs; stop on another failure. No broader budget/dependency/Git/system-setting permission.

31 October latest approval: test docs/R4_APPLICATION_NEXT_DECISION.md Option A, the 860x580 DIP initial application viewport at unchanged 90 MB. Preserve opaque WPF, wide cards, scrolling, bar/popups and x86/software preference. Build/test/measure, then stop on any failure. No budget increase, dependency, Git or system-setting action.

31 October subsequent approval: execute docs/R4_OPAQUE_WINDOW_PROPOSAL.md's bounded opaque WPF application-only comparison. Keep bar/popups, architecture/render preference and 90 MB limits unchanged; stop on a future failure. No package/Git/machine-setting authorization.

31 October: user approved docs/R4_APPLICATION_MEMORY_REVIEW.md's bounded wide application-page/memory correction at unchanged 90 MB. Compact popups belong only to top-bar clicks; application pages use wide cards/charts. Revalidate full CPU/RAM/overview/navigation/after-close/hidden costs before extending remaining pages. Stop on a future failure. No new dependency, system setting or Git operation authorized.

Latest user approval supersedes the R2 stop below: implement the R2 memory correction and remaining monitor UI (R3 module popups, R4 Dashboard/Settings, R5 polish). User explicitly approved a 90 MB bar limit on 30 October; popup remains 90 MB, CPU <1% and hidden-work rules unchanged. Finish pending resource validation; stop on future failures. No helper, fixer, creature, media, new dependency or Git operation is authorized. Read docs/UI_COMPLETION_PLAN.md.

Read docs/BAR_R0_R2_PLAN.md first. The latest user instruction replaces island/notch/hover-expand/style switching and creature with a 24 DIP WPF bar, click-open anchored popups and a later sidebar dashboard. Implement R0-R2 only, then STOP. R3-R5 and all old later milestones are deferred. The native renderer exception and earlier Lucide/UI approvals below are historical, not the active UI direction. Use Windows Segoe Fluent Icons/MDL2 Assets, no imported icons or new dependencies without asking. Preserve existing providers and newer user changes.

Active budgets (user approved 30 October 2026): bar <=90 MB working set and average CPU <1% over 300 seconds; popup <=90 MB; no chart/process work with popup closed. Count every auxiliary process/window. Never force GC or trim working sets. If a budget fails, STOP, report actual numbers and a proposal. The user approved the genuine x86 WPF visual host as the R1-R2 baseline after the 60.882944 MB comparison; see docs/WPF_RAM_BUDGET_DECISION.md. Preserve original 50 MB failures as historical evidence. No system setting/security changes; no telemetry or unenabled outbound traffic. Save reference screenshots under ignored docs/reference. User-specific/personal reference images do not enter Git.

Legacy UI sources/tests are preserved but excluded from the active build; document retired tests separately from executed active tests. Portable compatibility schemas may remain for migration. No automatic Git operations during feasibility work. The agreement below is historical wherever it conflicts with this replacement brief; its dependency, truthfulness, safety and source-preservation rules still apply.
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

