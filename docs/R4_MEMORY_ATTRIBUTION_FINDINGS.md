# R4 memory attribution findings — 31 October 2026

Status: approved diagnostic investigation completed; application acceptance remains blocked at 90 MB. The next experiment below is proposed, not authorized or implemented. Normal Release remains R3.

## What was built

Opt-in `--memory-profile` records bounded own-process/GC snapshots at startup, window, inventory, source probe/first sample, navigation, export and cleanup phases. Maximum 512 records and 128 module basenames per requested checkpoint; actual full run retained 361 records. Module names omit paths. No package, helper, forced GC, working-set trim, GC policy or Windows setting change. The recorder is inactive on normal launches. The wrapper marks profiling as attribution-only and cannot report application acceptance.

## How verified

Isolated Release builds completed with zero warnings/errors. Tests passed on actual x64 and x86: 108 each (70 Core, 15 Shell, 23 Windows). TRX: `TestResults/r4-memory-profile-{x64,x86}`. Added tests cover bounded recorder/privacy/snapshot semantics and decorator result/call/disposal preservation. A 12-second smoke exited naturally with 40 records, seven module checkpoints and drained workers.

Executed full command from the project root:

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/memory-attribution-300s -Page CPU -Scenario Navigation -CapturePercent 100 -MemoryProfile -Executable src/IsletMonitor/bin/r4-memory-profile/IsletMonitor.exe
```

Evidence: `docs/evidence/r4-dashboard/memory-attribution-300s` contains raw observer/tail CSV, summary, phase JSON, probe, binary hashes, capture and rejected scenario checks. Lifecycle, activity, immutable binary hashes, one 100% export and natural exit checks passed; 204 metric requests and 160 frames. Physical interaction was not verified. No second attribution or acceptance run followed the failure.

## Measured RAM and CPU

All MB below are decimal, consistent with the 90,000,000-byte cap. Recorder costs are included.

| Measurement | Result |
| --- | ---: |
| Observation duration / samples | 301.0010888 s / 302 |
| Observation working-set maximum | 91.099136 MB |
| Observation mean / p95 | 86.755437 / 88.985600 MB |
| Complete lifetime peak, including export/cleanup tail | **97.660928 MB — fails 90 MB** |
| Average CPU, machine normalized across eight logical processors | **0.2524129159%** |
| CPU, single-core equivalent | 2.0193033269% |

This instrumented peak is not a controlled regression comparison with the earlier uninstrumented 91.525120 MB run. Profiling allocates, loads APIs and changes timing. Historical failures remain unchanged.

## What the phases establish

| Phase | Elapsed seconds | Working set MB | Reported lifetime peak MB |
| --- | ---: | ---: | ---: |
| After arguments | 0.002 | 29.667328 | 29.667328 |
| Before bar | 0.045 | 31.723520 | 31.780864 |
| Bar visual prepared | 0.278 | 58.859520 | 58.859520 |
| Application HWND created | 0.316 | 59.781120 | 59.781120 |
| CPU probe begins | 0.362 | 61.546496 | 61.546496 |
| Application visual prepared | 0.441 | 71.634944 | 73.175040 |
| Inventory starts / finishes | 0.449 / 0.488 | 71.761920 / 73.506816 | 73.175040 / 73.535488 |
| CPU probe finishes | 1.206 | 82.759680 | 83.849216 |
| First transition to RAM | 4.471 | 87.310336 | 90.836992 |
| RAM probe begins / finishes | 4.482 / 4.499 | 90.951680 / 83.415040 | 90.951680 / 90.951680 |
| Before 100% export | 343.470 | 88.739840 | 95.952896 |
| After export | 343.563 | 92.745728 | 95.952896 |
| Final page selection | 345.162 | 92.856320 | 97.660928 |
| Workers drained | 345.386 | 84.934656 | 97.660928 |

The first cap crossing happened during initial navigation, well before the export. Export coincided with another increase in current residency, but cannot explain the earlier failure. Visual-prepared checkpoints mean drawing construction, not proof that pixels reached the physical display.

CPU probing, WPF drawing and inventory overlap on different threads. The CPU probe bracket's 21.21 MB increase cannot all be assigned to PDH. Likewise, inventory's overlapping 1.74 MB increase does not establish its exact cost or prove DXGI is the dominant cause. New module basenames across inventory include dxgi.dll, MLANG.DLL, psapi.dll and System.IO.FileSystem.DriveInfo.dll; loading identifies a phase, not resident bytes.

Before export, the last GC snapshot reports heap 9.565936 MB and committed 9.666560 MB; these are collection snapshots, not a live heap census. Cumulative approximate managed allocations reach 52.999748 MB by shutdown, with collection counts 12/11/2. Working set, private committed bytes, last-GC committed bytes and cumulative allocation measure different things. Subtracting them would not establish native resident memory. No leak or single root cause is proven.

Source review confirms CPU and RAM probes initialize wildcard Process Working Set/ID Process counters even when the top-process section is outside the viewport. CPU additionally opens per-core counters. Later probe brackets often coincide with decreasing residency, so the trace does not establish a monotonically growing counter leak.

## Concrete next experiment — requires review

Use two fresh, sequential, bounded **30-second provider-only diagnostic runs** of the same isolated x86 executable, without WPF bar/window initialization or inventory. Variant A exercises the existing CPU source with wildcard process counters; variant B omits only those process counters in an explicitly diagnostic source option while retaining aggregate CPU and per-core readings. Keep the same cadence, record before/after query creation, counter registration, first collection/read and disposal, retain hashes and raw external memory samples. Verify mock tests preserve the normal default and prevent the diagnostic variant from appearing as a normal application mode.

This separates process-counter initialization from WPF/inventory overlap. It is diagnostic attribution, not a five-minute product budget pass, feature removal or authorization to change the production backend. Report both results, even if either is above 90 MB; neither permits further UI work. Add no packages/helpers/settings changes or collection/trim tricks. If a material difference is reproducible, propose demand-started process sampling when its section is visible, retaining all product features; otherwise reject that hypothesis and report the remaining uncertainty before another correction.

## Known gaps / next step

Full 150% navigation, after-close and application-hidden acceptance remain unrun after the failure. Remaining wide module pages, settings behavior and polish are incomplete. Render exports are not physical 100%/150% desktop screenshots. Win10, mixed-DPI, games, screen-reader and hosted CI acceptance remain gaps. Stop for review of the focused provider comparison. No Git operations performed.
