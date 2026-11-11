# Monitor-only scope — 10 November 2026

The user cancelled the optional-tools extension. Remove the five experimental tools, their app entry points, sidebar pages, bar icons, native services, and dedicated test/diagnostic sources. Retain monitor settings, order, metric providers, popups and standard Settings editing. Legacy `Tools` settings are ignored on load/import and omitted when saving. Leave the user's files alone.

Build Release, run the active tests, and verify the monitor bar and Dashboard from a fixture containing older tool settings. Run the existing five-minute monitor bar measurement at the unchanged cap before replacing the normal Release output. Preserve historical evidence and keep Git manual.

## Verification

- Release build, then active tests in x64 and x86: 135 passed in each (80 Core, 30 Shell, 25 Windows). New regression covers import of enabled legacy tools, monitor order and export without the retired field.
- Eight-second x86 render fixture loaded a settings file with all five retired tools enabled. The bar showed CPU, GPU, RAM, traffic and battery in the configured order. CPU Dashboard opened with monitor-only navigation and exited naturally, with workers drained and the tray icon removed. Render exports are in `artifacts/monitor-only-preview`.
- `scripts/measure-r1-bar.ps1 -Gpu` passed the five-minute x86 bar gate: 76.80 MB whole-process lifetime peak and 0.041529% average machine CPU over 300.996924 s. No Dashboard opens, closed-popup work or fullscreen interruptions. Raw evidence: `docs/evidence/monitor-only/gpu-bar-300s`.
- Normal Release rebuilt without warnings or errors. All seven runtime files match the measured build. No new full application/popup resource matrix was run. Existing user settings and files were left untouched.
