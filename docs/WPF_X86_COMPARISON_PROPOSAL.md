# Next budget review: 32-bit WPF comparison

30 October 2026. Approved by the user; build/tests, smoke and full measurement completed; RAM gate failed. Keep WPF and the unchanged 50 MB bar / 90 MB popup limits. R1/R2 remain blocked if the active full trial fails.

The lighter visual host avoided Application/Window and PresentationFramework during its measured launch, but peaked at 76.337152 MB over 300.002634 seconds, with 0.0266924740% machine CPU. Raw evidence is in docs/evidence/r0-wpf-bar/visual-host. Avoid declaring an absolute minimum for WPF from these experiments.

Proposed next bounded comparison: run the identical visual-host fixture with a genuine x86 .NET 8 host. All 77 tests have passed in the corrected actual x86 test runtime; that does not establish x86 application memory or apphost behavior. The installed SDK contains Microsoft.NETCore.App.Host.win-x86. Verify the exact existing pack version/license and supported framework-dependent apphost build path before use; no new package, tool or framework may be downloaded without approval. Verify actual process bitness and preserve its manifest/DPI behavior. Do not merely set PlatformTarget=x86 and assume the generated executable host is 32-bit.

Smaller pointers/runtime structures may reduce memory; that is an inference to test, not a promised saving. Preserve accessible reading names and the same metric cadence. Measure all costs over 300 seconds after 30 seconds warmup; retain raw CSV, apphost/DLL hashes, bitness, capabilities, rendering preference and natural-shutdown evidence. Test launch on this machine only; do not claim Windows 10/ARM64 or two-monitor acceptance from it. No default architecture change or product feature work is authorized by this proposal.

If it still fails, stop for user review. Never replace working set with private bytes, trim the working set, force GC, silently raise limits or switch away from WPF. User may instead choose to stop feasibility work here. No further experiment starts until approval after the completed failure report.


## Approved implementation

User selected "Approve 32-bit WPF comparison". Existing SDK 8.0.414 / installed Microsoft.NETCore.App.Host.win-x86 8.0.20 (MIT) produced an isolated, framework-dependent win-x86 executable. No new package or installed runtime. Offline restore initially requested extra runtime packs and failed against an empty local feed; disabling runtime-pack downloads alongside apphost/targeting downloads allowed the framework-dependent restore and build to succeed using installed/local content. Existing project lock files are preserved; architecture-specific assets/locks are isolated under obj/win-x86-probe. Application dependency graph contains only the three existing project libraries plus framework references.

Build and diagnostics: scripts/build-r0-x86.ps1 reproduces the executed restore/build commands (syntax checked and full script executed successfully after the measured binary unlocked). The executable header reports I386 0x014c / PE32 0x010b; its embedded manifest includes PerMonitorV2 and asInvoker. The six-second smoke reports actual ProcessArchitecture=X86 / PointerSize=4, scale 1.25, first frame, two samples, accessible reading name and natural exit with timers stopped. The original visual host is unchanged apart from architecture/pointer diagnostic fields in the entry point. Normal launch and default architecture remain unchanged.

Release solution and isolated x86 application builds passed with zero warnings/errors. All 77 tests passed in each actual x64/x86 test runtime, with guards verifying all three suites. Evidence: TestResults/r0-genuine-x86-x64 and TestResults/r0-genuine-x86-tests. The 32-bit test build uses separate output/intermediates from the genuine apphost; test-host architecture is not used as proof of apphost architecture.

Full measurement command:

powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -VisualHost -ExpectedPointerSize 4 -ExecutablePath src/IsletMonitor/bin/win-x86-probe/IsletMonitor.exe -OutputDirectory docs/evidence/r0-wpf-bar/genuine-x86

The observer will reject mismatched runtime pointer size/architecture after natural exit. No other Tidlet instance at launch. Normal desktop and separate build/test activity occurred during observation. Module paths verify that the actual x86 CoreCLR/WPF runtime is installed 8.0.14, while the previous x64 host used a different patch. No isolated architecture-only causality is asserted. No forced collection/trim, uncounted helper, AppBar reservation, Git or Windows setting change. Actual render pipeline, external screen reader, interactive menu/keyboard and multi-monitor acceptance remain unverified. Completed result follows.

## Completed result - STOP

After 30-second warmup, 300.0027221 seconds / 301 samples. Peak/lifetime peak 60.882944 MB; mean 58.714908 MB; p95 60.821504 MB. CPU delta 0.96875 seconds = 0.0403642171% machine normalized / 0.3229137367% one core. CPU passes; RAM fails 50 MB by 10.882944 MB. Raw CSV replay agrees. Seven measured hashes match the isolated output; normal Release is not claimed equivalent. In-process X86 / PointerSize=4, first frame, 115 samples, root peer verified, timers stopped, natural exit 0; no idle menu/chart/process work or reservation. Lifecycle passes; numerical gate fails (2), enclosing command returned 1. Full reproducible offline build script subsequently passed without warnings/errors or downloads.

No further feature/experiment starts. Review WPF_RAM_BUDGET_DECISION.md for an explicit requirement and baseline decision. This comparison did not establish a 50 MB pass or change product defaults.
