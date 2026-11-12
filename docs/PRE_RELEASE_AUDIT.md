# Tidlet pre-release audit

Audited on 12 November 2026. This audit covers the current source and its Windows x86 preview. It does not authorize a release, a system change, or an elevated hardware test.

## Release recommendation

Treat the current build as a developer preview. Before a general release, qualify the package on a clean standard-user account, remeasure the pinned-widget budget, and make Gaming FPS behavior match what the controls promise.

## Findings by area

| Priority | Area | Finding and evidence | Release action |
| --- | --- | --- | --- |
| Blocker | Package and install | Windows reports the preview executable as `NotSigned`. The repository has a build/test workflow but no installer or release workflow. A fresh build requires the x86 Desktop Runtime and does not place PresentMon beside the app. | Produce a versioned installer or portable ZIP with all companion files, publish checksums, add appropriate notices, and test install, upgrade, launch and uninstall on a clean PC. Validate signing and publisher display if signing is part of the release. |
| Blocker | Memory and helpers | The last measured seven-pin desktop session failed the 90 MB cap at 90.31 MB lifetime peak. It averaged 0.164% CPU. The current widget build has no newer five-minute acceptance result. Successful PresentMon capture and automatic elevated CPU refresh also lack aggregate parent/worker numbers. | Repeat the existing five-minute bar and widget cases on the candidate. Add parent/worker accounting before claiming FPS or elevated CPU temperature meets budget. |
| High | Gaming and FPS | Auto Gaming Mode activates on fullscreen detection, but the bar suppresses itself for that state and stops PresentMon. The capture target is the foreground process. After switching away from the game, the strip can measure a different application. | Decide whether release FPS is windowed-only or whether capture can continue for an explicitly selected game. Make the Gaming page and tray behavior communicate that decision. |
| High | Data usage | The data ledger records physical adapter deltas when Tidlet samples them. With hover reveal enabled and no other Network view sampling, hiding the strip stops the normal sample loop and resets the network baseline. Closed and paused intervals are also excluded. The README describes totals as recorded while Tidlet is running, which is broader than the implementation. | Label totals as monitored traffic and disclose hidden gaps, or provide an explicitly measured collection mode independent of strip visibility. |
| High | Popup and strip overflow | A work area 280 px wide produced a 280 px popup host. The WPF surface still measures 320 DIP at 100%, so the right-side close button and scrollbar fall outside the host. With all seven strip modules, a 320 DIP layout put the final item's right edge at 528 DIP. These results were reproduced by invoking the current layout code. | Implement width-aware popup rendering and strip overflow or compact packing. Add tests for small work areas and physical mixed-DPI displays. |
| Medium | Sensors and CPU temperature | Standard launches need a manual CPU check. Desktop CPU widgets reuse the last session sample and carry no visible age or last-check label. Elevated automatic refresh was implemented but has no hardware end-to-end acceptance run. | Mark cached values and their age, and validate successful refresh, cancellation, helper exit and unsupported hardware. |
| Medium | Network popup and Wi-Fi | The popup always draws a gray connectivity history grid and says checks are off. The only actual diagnostics are on-demand from the Network Dashboard. Its Internet check is a TCP connection to a single IPv4 endpoint, while DNS uses the system resolver. | Give the gray panel an explicit unavailable state or replace it with a clear link to the existing check. Explain the endpoints and that a single endpoint timeout does not establish a full Internet outage. |
| Medium | Settings and Dashboard | The strip preview shows sample readings, and custom order is saved. Most appearance and preference changes rebuild every strip window, reset sampling and dismiss any open popup. | Keep the saved order and preview, then distinguish visual changes from changes that need a window or provider restart. |
| Medium | Accessibility | The popups expose the header actions and a summary through UI Automation. Most metric rows, charts, and scroll state are drawn directly and have no individual automation elements. | Provide readable labels, values and scroll state for screen readers. Test keyboard-only navigation, Narrator and high contrast. |
| Medium | Device and support limits | Battery and Intel GPU temperature availability varies by firmware and adapter. On the existing test PC, the GTX 1650 supplied a temperature while the battery and Iris Xe did not in the earlier readouts. Windows 10 and physical multi-monitor mixed-DPI behavior remain open. | Test representative laptops, desktops without a battery, mixed graphics, and display hot-plug or scaling before broad compatibility claims. |
| Medium | Battery packs | The charge percentage comes from Windows system power status, while detailed capacity and temperature are queried from the first enumerated battery interface. On multi-pack systems those fields may describe different scopes. | Clarify the scope in the interface and validate against a dual-battery machine. |
| Medium | Release notices | The dependency inventory identifies MPL libraries, but there is no assembled distributable to check for licenses and corresponding source notices. PresentMon and any optional bundled PawnIO installer need their own notices. | Include versioned third-party license and source notices in the installer or ZIP and audit the exact binary contents. |

## What already works in the verified preview

- The strip supports top, bottom, and hover reveal. Modules can be shown, hidden, and reordered. Battery stays last by default.
- CPU, GPU, RAM, Network, Disk, Battery and Sensors have popups and Dashboard pages. Sensor rows have wrapping and scrolling, and supported temperature readings respect the chosen unit.
- Widgets and desktop pins use shared widget rendering. Missing hardware readings degrade without inventing a value.
- Temp cleanup requires a scan and explicit confirmation. This audit does not exercise deletion or power-mode changes on the real PC.

## Source references

- [Strip layout](../src/Islet.Core/Bar/BarContracts.cs), [popup window sizing](../src/Islet.Shell/Bar/PopupCoordinator.cs), and [popup automation](../src/Islet.Shell/Bar/PopupVisual.cs)
- [Strip visibility and sampler](../src/Islet.Shell/Bar/MonitorBarHost.cs), [Windows fullscreen detection](../src/Islet.Windows/Bar/WindowsBarDesktop.cs), and [app composition](../src/IsletMonitor/BarApplication.cs)
- [CPU worker](../src/Islet.Windows/Metrics/CpuTemperatureHelper.cs), [network popup](../src/Islet.Shell/Bar/PopupVisual.Modules.cs), and [network diagnostics](../src/Islet.Windows/Bar/SystemDiagnostics.cs)
- [Historical measurements](../PROGRESS.md), [dependency inventory](DEPENDENCIES.md), [Mozilla MPL distribution guidance](https://www.mozilla.org/en-US/MPL/2.0/FAQ/), and [Microsoft SmartScreen guidance](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)

## Checks run on the current tree

- Locked NuGet restore succeeded. The Release solution build completed with zero warnings and errors.
- VSTest executed all 176 x86 tests and passed all three test suites. Results are under `TestResults/release-audit-x86`.
- All 48 light/dark UI probe runs passed, covering three strip placements, seven popups and fourteen Dashboard pages in each theme. Popup and Dashboard images were rendered at 100% and 150%. Evidence is under `artifacts/release-audit-ui-20261112-080343`.
- The PowerShell layout check reproduced the narrow-window geometry issue using the built Core assembly.
- The current five-minute RAM/CPU budget was not retested. Another Tidlet instance was already running. Elevated hardware, real fullscreen games, live outbound Wi-Fi checks, and cleanup or power-mode changes were outside this audit.
