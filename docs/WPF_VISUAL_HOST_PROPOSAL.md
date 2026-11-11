# Proposal: lighter WPF visual host, unchanged memory gate

30 October 2026. Status: approved by the user; implementation and full measurement completed; RAM gate failed. WPF remains required. The approved software-only Window prototype peaked at 84.627456 MB against 50 MB; CPU passed at 0.0356887884%. See WPF_MEMORY_NEXT_EXPERIMENT.md and evidence/r0-wpf-bar/software-only.

## Approved narrow experiment

Replace only the R0 feasibility fixture's WPF Application/Window/control tree with a WPF HwndSource whose RootVisual hosts DrawingVisual content. Continue rendering with WPF PresentationCore; do not use the retired native/GDI renderer. Draw the same 24-DIP CPU/RAM readings using WPF drawing/text APIs, with the same providers and three-second cadence. Preserve the existing Window fixture for comparison. No R1 AppBar/tray or R2 popup features yet.

This tests whether avoiding higher-level control/template initialization reduces resident memory. It is a hypothesis, not a projected saving or guaranteed pass. No dependency, native package, elevation, Windows configuration change, forced GC, working-set trimming or exclusion of shared pages. Keep 50/90 MB limits and include all auxiliary costs. No disk-size optimization is substituted for RAM measurements.

## Verification and limitations

Verify dispatcher lifetime, message handling, DPI and disposal with official documentation before coding. Do not drop keyboard handling or accessible names to manufacture a pass: direct drawing needs explicit hit testing, keyboard and accessibility support. Record any unsupported interaction as a gap; it cannot certify the final app. Popup controls may still load higher-level WPF assemblies, so even a bar-only pass would not prove the 90 MB popup gate or the 50 MB post-popup gate.

Build/test both architectures; validate missing/invalid readings and bounded natural shutdown. Measure a fresh 300-second bar trial after 30-second warmup, record raw working set/CPU, binary hashes and lifecycle evidence, and replay raw CSV. Verify preference separately from actual pipeline. Stop again if memory or CPU fails; do not continue R1/R2 or relax budgets. Approval would cover this feasibility experiment only.

## Official sources checked 30 October 2026

- HwndSource and IDisposable unmanaged-resource ownership: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0
- RootVisual accepts a Visual, assembly PresentationCore.dll: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource.rootvisual?view=windowsdesktop-10.0
- DrawingVisual is lightweight because it omits layout/event handling; Microsoft's sample uses a FrameworkElement host, so a direct-root host requires explicit lifecycle/input validation: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/using-drawingvisual-objects
- Drawing versus Shape performance considerations: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging

These sources establish available WPF APIs, not this application's achievable working set. TODO(verify): complete input/accessibility strategy and direct-root dispatcher/DPI behavior before implementation.

## Implementation and verification

The fixture uses a UIElement with DrawingVisual content as HwndSource.RootVisual, retaining WPF input and a custom UIElementAutomationPeer. It does not construct Application or Window. Win32 monitor/work-area reads and positioning of its own HWND live in Islet.Windows/Bar, with no desktop work-area changes. Source code for the original Window fixture is preserved. --visual-host is diagnostic-only, requires --r0-probe --solid, a report and bounded natural exit; ordinary launch is unchanged. The measured trial also uses the already approved application-local SoftwareOnly preference.

Normal Release solution and isolated x64/x86 builds passed with zero warnings/errors after the user closed an older app locking Release files. All 77 active tests passed on each actual test architecture (57 Core / 5 Shell / 15 Windows). Initial x86 test launch aborted before executing tests because the runtime resolver selected a 64-bit hostfxr; process-local DOTNET_ROOT_X86 and DOTNET_ROOT(x86) selected the existing 32-bit runtime for the successful corrected run. No installed SDK/runtime or persistent environment setting changed. Failed TRX evidence remains distinct from corrected results.

Six-second desktop smoke rendered a first frame at actual 125% DPI, attempted two CPU/RAM samples, verified the root peer's reading name and exited naturally with timers stopped. Its PNG is a render-target capture, not a desktop screenshot. No PresentationFramework or managed media projection assembly appears in that smoke's loaded-assembly report; all actual process working set still counts. Mouse/menu/keyboard behavior, external screen reader, DPI transition and actual render pipeline remain unverified. The root name is a single combined reading, not yet separate module accessibility peers.

Full trial command:

powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -VisualHost -ExecutablePath src/IsletMonitor/bin/visual-host/IsletMonitor.exe -OutputDirectory docs/evidence/r0-wpf-bar/visual-host

The experiment uses isolated outputs inside the sole project folder. No other Tidlet instance was recorded at launch. Normal desktop apps and separate build/test processes were active during observation; these are not helpers of this host. Completed result follows; no R1/R2 acceptance is inferred.

## Final measurement - STOP

30-second warmup plus 300.002634 seconds / 301 samples. Peak/lifetime peak 76.337152 MB; mean 69.769985 MB; p95 73.580544 MB. CPU 0.640625 seconds = 0.0266924740% machine normalized / 0.2135397918% core equivalent. CPU passes; RAM fails 50 MB by 26.337152 MB. Raw CSV recomputation agrees. Seven measured hashes match isolated output; normal Release binaries differ and are not claimed to have these measurements.

Natural exit 0, first frame, 115 samples, owned timers stopped, peer name verified, no idle menu/chart/process work or reservations. Loaded assembly report includes PresentationCore/UIAutomation but no PresentationFramework or media projection. SoftwareOnly remains an unverified pipeline preference. Numerical observer result is Fail (2); wrapper command returned 1. Render-target capture remains 125%, not a desktop screenshot.

No additional feature/experiment starts. See WPF_X86_COMPARISON_PROPOSAL.md for the next budget review, retaining WPF and unchanged limits.
