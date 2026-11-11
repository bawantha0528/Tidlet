# Application module toggles — 31 October 2026

Every module header switch must show/hide its top-bar item and persist the choice. Remove the last-item guard: users may switch all items off, leaving the tray and application available to switch them on again. Hide the empty bar and release any Reserve area through the existing surface SetHidden path; do not treat an empty layout as fullscreen suppression or close the application. Stop bar metric polling while empty. Reuse one portable visibility update for header/settings/context-menu paths.

Run focused tests across all module header switches, empty-layout persistence, bar hide/resume and kept-open application access. Build and a short live empty-bar/application preview; no broad full-suite rerun. No dependency, native API, admin, machine-setting or Git action. Prior resource numbers remain tied to prior binaries.

## Executed outcome

## Mouse-click correction

User reports switches still unresponsive. The native WM_NCHITTEST hook classifies the toggle/settings bounds as HTCAPTION, intercepting mouse input before WPF. Correct the header hit classification to return HTCLIENT for actual interactive WPF elements and retain dragging over empty header space. Add regression checks for this native-hook decision at multiple scales/negative screen coordinates and route mouse-up through the actual hit element, then run focused tests/build. No broad resource repeat, package, Git or machine change.

Release solution build with `-p:OutputPath=bin/module-toggles/` passed with zero warnings/errors. Four focused tests passed in each x64/x86 test-host configuration: every module header off/on (all seven modules), empty-layout settings export/import, hidden bar/no polling/resume, and the existing GPU toggle. Results: `TestResults/module-toggles-x64` and `TestResults/module-toggles-x86`. The Windows test assembly had no matching tests and is not counted.

Eight-second native preview using temporary empty-layout settings exited naturally: bar Samples=0, DashboardReads=8, GPU bar requests=0, dashboard workers drained, native surface Hidden=true and TrayPresent=true. Empty layout did not trigger environmental suppression. The CPU application page remained open with its switch off; its 100% render export was visually inspected at `artifacts/module-toggles-empty-preview/bar.dashboard.100.png`. This is a render export, not a desktop screenshot. Actual monitor scale was 125%; no new mixed-DPI hardware acceptance claim.

No full suite or five-minute resource rerun for this scoped UI/state correction. Prior resource results certify their original binaries only. Reserve cleanup uses the existing native hide/reposition path and skips reservation for empty settings; the new empty-layout Reserve behavior was source-reviewed, not physically tested. Current user's saved settings were preserved. No Git, dependency or machine-setting action.

Mouse correction outcome: the prior accessibility tests did not exercise native nonclient routing. HeaderHitTest now returns HTCLIENT for interactive header elements and HTCAPTION only for empty draggable header space. The seven-module regression checks toggle/settings/close/empty-title classification at 100/125/150% scales and negative screen origins, then uses WPF mouse-up rather than automation Invoke to toggle off/on. Four focused tests passed in each x64/x86 configuration under TestResults/toggle-mouse-fix-*. Isolated Release build passed with zero warnings/errors. Initial normal Release replacement failed because PID 28288 held its DLLs; after the user exited it, the normal solution Release build passed with zero warnings/errors. No process was terminated automatically. Physical mouse/monitor scenarios and new 300-second resource measurements remain unclaimed.
