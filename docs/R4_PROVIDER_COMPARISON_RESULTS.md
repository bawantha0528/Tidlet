# Provider-only comparison — 31 October 2026

## What was built

Executed the user's approved two-variant diagnostic from R4_MEMORY_ATTRIBUTION_FINDINGS.md. Explicit `--provider-comparison with|without <absolute-report>` dispatch bypasses WPF and device inventory. The production constructor still enables process counters. Only the diagnostic option omits wildcard process Working Set/ID Process counters, retaining CPU and logical-core readings. Bounded phases surround query creation, process counter registration, all-counter completion, probe/read/disposal. Sources dispose normally; no helper, new package, machine setting, forced GC, working-set trim or Git action.

## How verified

Isolated Release builds completed with zero warnings/errors; **109 tests passed each on actual x64/x86**, 70 Core/15 Shell/24 Windows. Results: `TestResults/r4-provider-{x64,x86}`. Added test verifies explicit diagnostic argument validation and unchanged normal process-counter default. Existing mock-source/decorator tests remain green. Comparison script parsed without errors, then executed:

```powershell
powershell -NoProfile -File scripts/compare-provider-memory.ps1 -Executable src/IsletMonitor/bin/r4-provider-comparison/IsletMonitor.exe -OutputDirectory docs/evidence/r4-dashboard/provider-comparison
```

The script ran each variant sequentially in a fresh x86 process, externally observed with `scripts/measure.ps1 -DurationSeconds 30 -WarmupSeconds 0 -AllowShortRun`, then recorded the tail through natural exit. Both made 30 valid aggregate CPU reads and 29 valid core reads; process rows totalled 174 with counters and zero without. Neither loaded PresentationCore, PresentationFramework or WindowsBase. Binary hashes across exe/dll/json were unchanged. Evidence includes raw observer and tail CSV, provider phase reports, summaries and comparison.json. Natural exit and source disposal completed in both. Apphost hash alone is insufficient because apphost can stay identical across managed builds; the retained DLL hashes identify the measured binary set.

## Measured RAM and CPU

| Variant | Observation duration | Observation max MB | Complete lifetime MB | Average machine CPU |
| --- | ---: | ---: | ---: | ---: |
| With process counters | 30.998135 s / 32 samples | 33.873920 | **37.736448** | **0.434754%** |
| Without process counters | 30.9955977 s / 32 samples | 30.380032 | **34.156544** | **0.289860%** |

Both include diagnostic/report-serialization costs and tail peaks. Eight logical processors normalize CPU. The observer's 30-second window includes startup and excludes part of the tail; lifetime peaks include that tail. These are short **attribution-only** runs: OverallAccepted=false. Neither is a five-minute application pass. The unchanged product cap is 90,000,000 bytes.

Observed lifetime difference: **3.579904 MB**. With-counter process registration coincided with working set rising 25.591808 to 29.941760 MB. In the without-counter variant, subsequent per-core registration coincided with 25.321472 to 29.106176 MB and a transient lifetime peak of 33.325056 MB. Thus removing process counters does not avoid all performance-counter startup costs. Phase brackets include recorder allocations and unrelated runtime activity; they are not exact native allocation sizes.

At disposal, working sets were 34.246656 MB with and 30.384128 MB without. The higher whole-lifetime peaks occur later and include report generation. Neither source visibly returns all residency immediately on query disposal, but a short snapshot cannot establish a leak.

## Interpretation and limits

This pair supports a modest cost associated with process counter sampling, not a claim that the original overlapping 21 MB CPU-probe increase was all PDH or all process counters. Registration of either counter family can initialize shared native counter infrastructure. Only one pair ran, with the with-counter variant first; OS caches, order and background activity are uncontrolled. A guaranteed 3.58 MB saving, exact allocation attribution or dependable full-application pass is not established. No process names, IDs or sampled values were persisted by this diagnostic; reports retain only aggregate read/row counts and memory phases.

## Proposed bounded production correction — requires approval

Make expensive detail sampling follow actual section visibility in the application window. Keep aggregate CPU via GetSystemTimes and RAM via GlobalMemoryStatusEx active at the existing cadence. Initialize per-core PDH counters only while core bars are in the viewport; initialize process counters only while the process list is in the viewport. Scrolling into a section requests genuine data with an explicit warming state; scrolling away stops that work. Dispose/recreate counter queries through the existing serialized worker boundary, with no native handle disposal racing a read. Keep all rows/features available and preserve normal popup behavior when those sections are visible. Do not suppress readings, change units, fake values or remove features to claim a pass.

Implement visibility-interest tests with mock sources for initial view, scrolling, navigation, close and disposal ordering, then Release build/test both architectures. Run one full **uninstrumented 300-second navigation trial** with complete startup/capture/tail accounting at unchanged 90 MB, recording section activity. Stop immediately on another resource failure and report a proposal; on a pass, continue the previously required static/after-close/hidden checks before extending pages. No new dependency or machine setting is part of this proposal. It is not implemented or authorized by the diagnostic approval.

## Known gaps / next step

The WPF application still has the earlier failed lifetime measurements. R4/R5 pages/polish and physical DPI/Win10/game/accessibility/CI checks remain incomplete. Normal Release remains R3. Report this comparison and await review of visibility-driven detail sampling before further production changes.
