# Keep WPF: approved memory experiment and measured result

The user clarified that RAM is the problem and asked to retain WPF. There is no approval to replace WPF with a native renderer or to raise the 50/90 MB limits. The solid-background comparison subsequently failed at 112.783360 MB; its evidence is retained in docs/evidence/r0-wpf-bar/solid.

The user approved the following bounded experiment, which stays entirely within WPF:

1. Add a diagnostic-only application-local software-rendering preference before constructing the Application/window, using System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly. Do not touch the graphics registry, Windows acceleration settings or other processes.
2. Keep the tiny 24-DIP bar and identical CPU/RAM sampling. Do not add charts, reserve work area, processes or R1/R2 features during this experiment. Defer construction of the context-menu visual until the user opens it; retain accessible names.
3. Build/test and perform a fresh 300-second comparison with the same 30-second warmup. Retain raw working set, private bytes, CPU, loaded-module names, scenario/lifecycle proof and binary hashes. Do not replace the working-set gate with private bytes or discount shared DLL pages.
4. CPU may increase with software rendering. Memory savings are an unverified hypothesis, not a guarantee. A preference is not proof of the actual pipeline. If it still exceeds the limits, stop and present the measurements; do not continue R1/R2 or silently relax targets.

Official API checked 30 October 2026: https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-7.0 . Its applicability table includes Windows Desktop 8. The property is a current-process preference and the value type is System.Windows.Interop.RenderMode. Documentation warns that other system configuration can override rendering preferences. Do not use any registry setting mentioned by the documentation.

Read-only solid-process module inventory contains Intel graphics modules and WPF assemblies. ImageSpanBytes is a PE mapped-image address span, NOT resident memory; it does not establish that a module consumes that many RAM bytes or causes the failure. The possibility that avoiding hardware pipeline initialization reduces resident memory is an inference to test. No leak, driver fault or guaranteed optimization is asserted.

No dependency, elevation, forced GC, working-set trimming or system settings change is proposed. Implementation and measurements are recorded below. R0_BUDGET_PROPOSAL.md remains the historical record of the original transparency failure.
## Approved experiment completed - 30 October 2026

User approval: "Approve WPF memory experiment". Implemented the bounded software-rendering preference and lazy context menu, without changing normal launch rendering, adding packages, changing Windows settings or increasing limits.

Release solution and isolated x86 application builds passed with zero warnings/errors. Active tests passed 75/75 on each x64 and x86 runtime (57 Core, 3 Shell, 15 Windows); both TRX guards passed. Retired legacy UI tests remain excluded rather than claimed passing.

Command: powershell -NoProfile -File scripts/measure-r0-bar.ps1 -Solid -SoftwareRendering -OutputDirectory docs/evidence/r0-wpf-bar/software-only

After 30 seconds warmup, 300.9961386 measured seconds / 302 samples: peak working set 84.627456 MB, mean 80.620252 MB, p95 84.578304 MB; CPU 0.859375 seconds, 0.0356887884% machine normalized over eight logical processors / 0.2855103072% core equivalent. RAM FAILS the unchanged 50 MB gate by 34.627456 MB; CPU passes. Peak is 28.155904 MB lower than the historical solid trial, but this combined rendering/menu change does not isolate causality.

Raw CSV replay agrees; all seven binary hashes match the measured Release outputs. Application exited naturally with code 0, first frame rendered, 115 metric sample attempts, owned timers stopped, no idle menu construction, no chart/process work or AppBar reservations, no media projection/legacy shell/native renderer. ProcessRenderPreference is SoftwareOnly; actual render pipeline remains unverified. Interactive context-menu opening was not exercised. PNG is an actual 125% WPF render-target capture, not desktop or R2 100%/150% evidence. Lifecycle passes; numerical measurement returns failure (2), enclosing command returned 1.

R1/R2 remain stopped. Next proposal: WPF_VISUAL_HOST_PROPOSAL.md. No additional rendering experiment is approved by the current approval.

