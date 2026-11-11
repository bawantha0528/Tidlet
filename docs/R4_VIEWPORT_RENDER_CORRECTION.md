# Approved viewport drawing correction — 31 October 2026

User approved R4_VISIBLE_RENDERING_PROPOSAL.md. Keep all features, scrolling, viewport-driven data queries, opaque 860x580 WPF application, x86/software preference, existing bar/popups and 90 MB unchanged.

## What was built

Wide CPU/RAM page drawing now skips offscreen labels, charts, gauges, cards and core/process rows before constructing their retained text/geometry. Layout height and detail-interest computation still cover the whole page. Text-cache entries outside the new viewport are removed on scroll, retaining the existing 192-entry bound. Conservative stroke/text bounds avoid clipping partially visible content. Existing clipping remains as a final pixel boundary; culling complements it. No allocation-free WPF claim: visible drawing and some caller-side strings still allocate.

Core fill geometry now requires a finite value in 0–100, independently of native source validation. Invalid values retain a missing/warming display instead of filling a bar. No sampling, units, polling interval, theme or feature removal was used to lower costs. No new dependency, helper, system setting, forced GC/trim or Git operation.

## How verified

Release builds completed with zero warnings/errors; **113 tests passed each on actual x64/x86**, 70 Core/18 Shell/25 Windows. TRX: TestResults/r4-viewport-{x64,x86}. New rendering test checks NaN/infinities/negative/over-100/maximum-double cores against missing-value pixels, confirms a valid 50% reading changes the display, and checks lower-content visibility plus identical pixels after a scroll round-trip. Existing CPU/RAM wide-page, lifecycle, viewport-interest and mock query-selection/disposal checks remain green.

Short live CPU and RAM runs each made eight reads/seven frames, exited naturally and drained workers. CPU requested eight core details and zero processes; RAM requested neither detail family in its initial viewport. Inspected actual 100% render exports at artifacts/r4-viewport-smoke/{CPU,RAM}/bar.dashboard.100.png: visible summary cards/charts preserved, lower sections scroll below the viewport. These are 860x580 render exports, not physical 100% DPI desktop screenshots. Short runs are not resource acceptance.

Full unprofiled navigation command:

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/viewport-render-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-viewport-render/IsletMonitor.exe
```

The binary set is isolated and frozen during observation. Resource result pending when this section was written; append the actual result before any subsequent trial or production work. Original failed evidence remains intact. Normal Release stays R3. No completed R4/R5, physical mixed-DPI/Win10/game/accessibility/hosted CI claim.

## Completed resource result — failed lifetime gate

**Measured RAM and CPU:** 300.9850745 seconds/302 samples; observation max **86.708224 MB**, mean 83.433865 MB, p95 86.249472 MB. Whole lifetime/tail **93.814784 MB**, exceeding unchanged 90 MB by **3.814784 MB**. Average CPU **0.0940920826% machine**, 0.7527366610% core equivalent on eight logical processors. Observation summary Pass is numerical-window-only; scenario-check correctly records OverallAccepted=false for lifetime failure. Peak was recorded before export. Separate-run differences do not prove causal improvements/regressions or a leak.

**How verified:** One window visited 87 pages, 199 requests/155 frames, 90 core-detail requests and zero offscreen process requests. Lifecycle/activity/unchanged seven-binary hashes/single 100% export all passed. Opaque target, workers drained, natural app exit zero. Measurement wrapper returned failure. Evidence docs/evidence/r4-dashboard/viewport-render-navigation-300s retains raw samples/tail/summary/context/probe/hashes/capture and rejected scenario check. All product startup, visible window and capture/tail costs included. No memory profiling enabled.

**Known gaps / next step:** Stop: no full static/after-close/hidden/150% trial or remaining page/polish work after failure. Native and mock-backed functionality checks do not certify full scrolled process-view memory. Physical DPI/screenshots, Win10, games, screen readers and hosted CI remain gaps. Normal Release stays R3; measured output is isolated bin/r4-viewport-render. docs/R4_WPF_BUDGET_DECISION.md proposes an explicit 100 MB application-use-session lifetime limit, retaining 90 MB fresh bar/popup and post-close current observation, or keeping 90 MB with work paused. No cap/gate changes implemented; await review. No new dependency/helper, system setting, forced collection/trim or Git operation.
