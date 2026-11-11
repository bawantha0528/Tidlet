# Network bar spacing — 31 October 2026

Status: reverted at the user's subsequent request. Source dot placement/idle colors restored to the earlier bar-right-green revision. Historical preview/build results below describe the undone revision only. Green battery and right alignment retained.

User requests the upload/download dots close to their corresponding speeds: red upload on the top row, blue download on the bottom row. Keep the fixed-width, right-aligned speed column and align both dots just before it, with a small gap. Use dimmed red/blue when idle so the meaning stays recognizable. No metrics/cadence/settings changes. Verify an app-only Release build and short bar render preview; skip full test suites/performance trials per explicit user instruction. No dependency, system setting or Git action.

Completed: the two dot positions share a column calculated from the wider speed label, leaving a 4.5-DIP gap to that text. Values keep the same fixed right edge; other items retain their slots. Frozen red/blue brushes are reused, with dimmed matching colors when idle. App-only Release build in bin/bar-traffic-dots succeeded with zero warnings/errors. Six-second live bar preview exited naturally, timers stopped/tray removed; inspected artifacts/bar-traffic-dots-preview/bar.png. No full test/performance rerun or new resource acceptance claimed.
