# Light mode

Apply the existing Light preference to the Dashboard, module pages, monitor popups, and bar. Use pale neutral surfaces, gray-blue borders, dark labels, and blue selection with the established metric colors. Refresh the live UI through the existing settings path. Preserve dark mode, saved layouts and the current behavior of System mode. Verify theme switching and representative x86 render exports in an isolated build.

## Verification

- The isolated Release solution build completed with zero warnings and errors. All 32 active WPF shell tests passed in the x86 configuration. The regression invokes the Settings theme row and checks the open window changes from charcoal to a pale background.
- Native x86 previews opened the Dashboard, CPU page, RAM popup, Network popup, and live-GPU bottom strip. The probes loaded isolated Light settings, produced WPF render exports, and exited with workers drained and tray removed. Renders are under `artifacts/light-mode-preview`.
- **Settings → Theme** persists the selection. **System** retains its existing dark fallback.

## Resource trial

The five-minute observer recorded 301 samples over 300.006 seconds, with a 92.139520 MB observation maximum, 94.711808 MB lifetime peak, and 0.208329% average machine CPU. The 90 MB cap was exceeded.

The probe opened Network at 70.76 seconds and GPU at 75.46 seconds, then RAM near shutdown. The wrapper rejected the bar-only scenario because it recorded 275 popup frames and reads. It verified Light settings, live GPU readings, natural exit, and drained workers. These results describe a mixed bar-and-popup session and cannot establish the cost of Light mode by itself. See [observer summary](evidence/light-mode/bottom-gpu-300s/summary.json).

Keep the preview isolated. The proposed next step is a controlled Light versus Dark bar-only comparison using the same binary, followed by a held-open GPU popup trial if the bar-only runs pass. Keep the 90 MB limits and leave the test strip alone during each run.
