# Charcoal monitor popups

Apply the reference's opaque charcoal cards, left-aligned headings, plain borders, and solid chart accents to the existing click-open monitor popups. Keep the rounded corners, popup placement, providers, Dashboard, and bar settings. The RAM panel uses real physical and commit readings. Verify the render and popup lifecycle in an isolated x86 build before any Release replacement.

## Verification

- The isolated Release solution build completed with zero warnings or errors. All four focused x86 popup drawing and lifecycle tests passed. The pixel check verified an opaque neutral background and a transparent outer corner.
- Short live x86 probes opened CPU, RAM, GPU, Network, Battery, Disk and Sensors. Each produced 100% and 150% WPF render exports and exited naturally with workers drained and the tray removed. CPU, RAM and Battery exports were visually inspected. Local evidence is under `artifacts/popup-charcoal-preview`.
- The five-minute resource trial was skipped while another Tidlet instance was active. The normal Release remains held. The isolated preview is in `src/IsletMonitor/bin/popup-charcoal`.
