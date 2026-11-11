# Charcoal Dashboard

Apply the latest solid charcoal reference to the full application window, including Settings and the monitor pages. Use a blue active navigation row, gray card borders, and accent marks on the hardware inventory. Keep the current opaque WPF host, window size, navigation and live data. The reference's combined-module column is outside this visual restyle. Verify the isolated x86 build, render exports, and applicable resource gate before considering Release promotion.

## Verification

- The isolated x86 Release solution build completed with zero warnings or errors. All 16 focused ApplicationPageTests passed.
- Native previews of Dashboard, CPU and Settings exported 860×580 and 1290×870 renders, used the opaque window host, and exited naturally with workers drained and the tray removed. The 100% renders were inspected. The Dashboard and CPU README images are from this revision.
- The previous popup preview was already running, so no five-minute application trial was started. The updated executable is in `src/IsletMonitor/bin/dashboard-charcoal`. Normal Release remains at the previously validated build.

## Follow-up navigation run

All 139 active tests passed with the x86 test configuration. After the earlier instance closed, the navigation trial recorded 302 samples over 301.003 seconds. Working set peaked at 76.80 MB during observation and 79.18 MB over the process lifetime. Average machine CPU was 0.0435%.

Tidlet detected an environmental suppression condition and closed the Dashboard at 10.00 seconds. It recorded four module reads and four frames, and no end-of-run export. The process exited naturally and drained its workers. The numerical limits passed, but the open-window scenario failed with exit 2. This evidence does not certify open Dashboard performance. See [scenario check](evidence/dashboard-charcoal/navigation-300s/scenario-check.json). A repeat requires the desktop to remain visible, awake, and outside fullscreen throughout the trial.
