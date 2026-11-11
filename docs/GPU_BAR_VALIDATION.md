# Live GPU bar — 31 October 2026

## What was built

GPU bar now displays a real percentage rather than `Details`. The existing GPU Engine/DXGI source is created lazily on a serialized worker, driven by the visible bar's existing cadence. No additional timer, package, driver, helper, admin permission or network request. Hybrid GPU summary is the maximum valid reported adapter usage, never their sum; the help text names the reported adapters and the existing popup/application still show them separately. Missing/denied/invalid readings remain unavailable, and initial counter baselines are warming states.

The composition root now keeps its sampling settings in sync with saved changes. The GPU application-header toggle updates saved bar membership; enable starts GPU sampling, disable removes the item and disposes its source after any pending read. Pause/fullscreen/hide/exit stop the bar sampler. Re-enable waits for prior disposal. GPU was added after CPU in the user's existing saved bar items; other preferences were retained. Other users' default module list was not changed.

## Verification executed

Release solution build at bin/bar-gpu passed with zero warnings/errors. Initial build failed on two target-typed test fixture constructor expressions; corrected to explicit GpuActivity before successful build. No production dependency change.

Focused tests: three BarGpuSamplerTests and GpuHeaderToggleSavesVisibilityAndKeepsTheApplicationOpen passed with x64 and x86 test-host configurations. Results TestResults/bar-gpu-focused-{x64,x86}. The toggle test was strengthened to check actual WPF input hit area against the displayed switch; its rebuilt Shell-only test then passed again in both architectures, TestResults/bar-gpu-hit-{x64,x86}. The test invokes the same accessible action against a mock save boundary, confirms show/hide and preservation of all other items, and keeps the application open. No synthetic OS input, real termination or settings-changing Windows action.

Tests cover max adapter selection, NaN/infinity/impossible values, valid zero, counter warming, no factory while off, pending-read disposal before re-enable, and denied-probe behavior. Full suites were not repeated. Fourteen-second live bar preview read actual Intel Iris Xe/NVIDIA GTX1650 metrics, showed GPU 5%, exited naturally and drained the worker. Inspected artifacts/bar-gpu-preview/bar.png; it is a render export, not a physical screenshot.

Commands used established offline environment, `dotnet build TidletMonitor.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -p:OutputPath=bin/bar-gpu/` and `dotnet test TidletMonitor.sln -c Release --no-build --no-restore -p:OutputPath=bin/bar-gpu/ --filter 'FullyQualifiedName~BarGpuSamplerTests|FullyQualifiedName~GpuHeaderToggleSavesVisibilityAndKeepsTheApplicationOpen' --logger trx --results-directory TestResults/bar-gpu-focused-x64`. x86 test repeats with existing x86 runtime roots and `-- RunConfiguration.TargetPlatform=x86`. Shell-only hit-area rebuild/test uses the same output, BuildProjectReferences=false, and corresponding test-name filter.

## Resource result

```powershell
powershell -NoProfile -File scripts/measure-r1-bar.ps1 -OutputDirectory docs/evidence/gpu-bar/live-300s -Gpu -Executable src/IsletMonitor/bin/bar-gpu/IsletMonitor.exe
```

Accepted: **301.0005157 seconds / 302 samples**, observation max **76.435456 MB**, full lifetime/tail **76.439552 MB**, average machine CPU **0.0434748009%** (0.3477984074% core equivalent/eight logical processors). Fresh bar limits remain 90 MB / <1% machine CPU. 115 bar samples/GPU requests, final actual GPU 5.10%, workers drained, zero closed-popup chart/process work, unchanged hashes and natural cleanup passed. No other Tidlet instance or helper was excluded. Raw observations/tail/hash/policy/probe/scenario check retained in docs/evidence/gpu-bar/live-300s.

The existing bar wrapper now supports explicit executable/GPU fixture settings and includes full capture/shutdown tail, shared session memory policy and binary-hash checks. Syntax verified; historical evidence untouched. No forced collection or residency manipulation.

## Gaps / next step

Run bin/bar-gpu/IsletMonitor.exe after exiting an old instance. Use the toggle at the top-right of the GPU application page to show/hide GPU in the bar. Full-suite/hosted CI, physical multi-monitor/DPI/Win10/real-game and new GPU-enabled hidden/application co-open resource trials were not executed; off/pause/hidden disposal uses the same demand lifecycle, covered by mocks. Earlier resource results certify their original binaries only. Normal Release remains frozen; no Git operation.
