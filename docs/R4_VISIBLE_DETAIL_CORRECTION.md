# Visibility-driven detail sampling — 31 October 2026

User approved the bounded correction in R4_PROVIDER_COMPARISON_RESULTS.md. Keep the 90 MB cap, aggregate metrics, all features, opaque 860x580 WPF application, x86/software preference and existing popup defaults.

## What was built

Portable PopupDetails flags and optional IPopupDetailSource let the application presenter pass viewport interest on its serialized worker before Probe/Read. Core bars/process lists intersecting the viewport activate only their counter families. No intersecting detail means no PDH query; basic CPU/RAM remain available. Changing interest closes/rebuilds the owned query, resets rate baselines and frees the process buffer; only process counters allocate that buffer or enumerate processes. Reads and disposal stay serialized. Capability dictionaries retained by older UI samples are not mutated during reconfiguration.

Wide pages reserve process-section height before values exist, show warming/unavailable explanations and retain scroll access to all features. Scroll changes clear older detail displays; an in-flight result with outdated viewport interest is discarded for those details. Queries stop on navigation/close through existing lifecycle cleanup. Compact popup defaults remain fully enabled. Request totals are reported separately as DashboardCoreDetailRequests/DashboardProcessDetailRequests; these represent requested work, not proof of successful native samples.

## How verified

Corrected Release builds: zero warnings/errors. 112 tests pass each on actual x64/x86 (70 Core, 17 Shell, 25 Windows); TestResults/r4-visible-corrected-{x64,x86}. Mock tests cover initial no-process interest, scroll into/out of processes, ordering before Probe, close/no later reads, no overlapping configuration/disposal, and core-interest viewport transitions. Existing mock navigation/predecessor disposal tests remain green. Pure counter-selection tests cover CPU/RAM/all/none/diagnostic process omission. An initial viewport test incorrectly expected core bars offscreen when partly intersecting; corrected fixed geometry/core count instead of changing the visibility rule. Original failed TRX remains TestResults/r4-visible-x64.

Full trial command (result pending when this entry was created):

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/visible-details-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-visible-corrected/IsletMonitor.exe
```

This does not enable --memory-profile. Full app/startup/capture/tail costs stay counted. No normal Release replacement, new dependency/helper, Windows setting, forced collection/trim or Git operation. Resource acceptance and subsequent static/after-close/hidden work remain pending until the completed trial is assessed. Physical mixed-DPI/screenshots/Win10/games/accessibility/CI acceptance remains incomplete.

## Completed resource result — STOP

The full unprofiled navigation trial completed: **300.0063513 s / 301 samples**, observation max **86.347776 MB**, mean 81.386826 MB, p95 85.946368 MB; whole lifetime/tail **91.734016 MB**, exceeding unchanged 90 MB by **1.734016 MB**. Average CPU **0.1269504373% machine** / 1.0156034987% core equivalent on eight logical processors. The observation-only summary says Pass, but scenario-check correctly rejects the whole-lifetime gate: OverallAccepted=false. Peak was already recorded before capture. This separate run is not a controlled causal comparison with earlier builds.

One application window visited 87 pages, 200 reads/156 frames; **91 core-detail requests and zero process-detail requests**, consistent with partially visible CPU core bars and offscreen process lists in this unscrolled navigation. No claim that full scrolled process views were resource-certified. Lifecycle/activity/hash/capture checks passed; workers drained, opaque target, natural exit zero. Retained evidence: docs/evidence/r4-dashboard/visible-details-navigation-300s. The wrapper exits failure despite the application's natural exit zero.

No further static/after-close/hidden/150% acceptance trials or feature work after failure. Normal Release stays R3; isolated bin/r4-visible-corrected is the measured development build. docs/R4_VISIBLE_RENDERING_PROPOSAL.md proposes viewport-aware drawing (current clipping still constructs offscreen content), plus finite/range guard coverage for synthetic invalid core fills, before another full navigation trial at 90 MB. Native core source already filters those values; renderer hardening remains a known gap. This next correction is not approved or implemented. No cap increase, Git, dependency/helper, system setting, GC or trim action.
