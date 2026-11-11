# Taskbar-style bottom monitor strip

The user requested the existing monitor bar at the bottom with the attached taskbar visual style. Use the existing Bottom setting to place a compact glass monitor strip directly above the Windows taskbar in Overlay mode. Keep Windows Start, pinned apps and tray controls native. Preserve saved metric ordering, alignment, and the existing Top option. Use current real bar samples for a tiny CPU history and GPU activity meter. Keep red upload, blue download and the green battery. Bottom popups open upward.

Use the monitor work area for Overlay placement; no Explorer injection, reparenting, taskbar replacement, or Windows settings changes. The existing Reserve mode remains an AppBar and follows its documented negotiation. Test Overlay only on the user's desktop. Build and verify geometry, focused popup/bar tests, a short live bottom preview, and the five-minute resource limit before updating normal Release.

Official references: [Windows AppBars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars) and [monitor work-area bounds](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfo).

## Verification before resource check

- Release `bin/taskbar-glass` builds completed without warnings or errors. Seventeen focused x86 tests passed (eight Core, nine Shell) including negative-origin and 150% geometry, order, popup rendering and lifecycle. The final rebuild only tightened the bottom traffic labels beside their arrows.
- Twelve-second live Overlay preview, 125% DPI: bar x1320..1910, y972..1015, Windows work-area bottom1020. Actual CPU popup y111..966 opened upward; 12 frames, live GPU data, all workers drained, tray removed and natural exit. Render exports: `artifacts/taskbar-glass-preview`. Placement was measured on this display; auto-hide, secondary hardware and Reserve with this new style are not claimed as physically verified.
- The translucent popup-only trial was interrupted after 86 samples with 86.34 MB peak observed. Retain incomplete evidence at `docs/evidence/popup-glass/translucent-cpu-300s`. A single combined bottom/CPU popup trial covers the current build.

The bar receives `WM_SIZE` to update its drawing after native placement changes, and uses `MonitorFromWindow` with nearest-monitor fallback to query current work-area bounds: [WM_SIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-size), [MonitorFromWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromwindow).

## Resource result

The 300.97-second combined bottom strip/CPU popup trial passed live placement and shutdown. It failed the 90 MB lifetime cap at 92.35 MB, with 0.321% average CPU. Normal Release was left in place. See [memory review](TASKBAR_GLASS_MEMORY_REVIEW.md).
