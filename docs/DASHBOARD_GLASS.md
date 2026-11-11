# Dashboard glass styling

Apply the supplied visual reference to the full Dashboard and its module pages. Use an opaque WPF host with static navy, violet and teal gradients, a glass-like sidebar selection, and tinted cards. Draw a generic device illustration and keep the reported device data. Reuse the drawing pipeline and verify isolated x86 render exports before considering release. The earlier top-strip release gate remains separate.

Reference for frozen WPF brushes: [Paint an area with a radial gradient](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-paint-an-area-with-a-radial-gradient), checked 10 November 2026.

## Validation

- Isolated Release solution build succeeded with zero warnings or errors. All 16 active x86 ApplicationPageTests passed.
- Native x86 probes opened Dashboard, CPU and Settings and exported each page at 860×580 and 1290×870. The probes used the opaque window host and exited naturally with dashboard workers drained and the tray icon removed.
- The 300-second navigation trial refused to start because the older app-icon build was running. There are no fresh RAM or CPU figures for this revision. Normal Release remains held at the earlier top-edge strip gate.
