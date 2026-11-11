# Top-edge reveal strip

The user replaced the requested native-taskbar placement with a glass monitor strip that drops from the top when the cursor reaches the edge. Add an opt-in **Top · reveal on hover** placement alongside existing choices. Use the same strip and readings, with a short clipped reveal animation, a brief hide delay, and an open-popup hold. No metrics are sampled while every strip is hidden. Suspend edge checks while Windows suppresses the bar. Preserve the native taskbar and existing saved placement.

Test edge geometry and lifecycle with a controlled cursor source, inspect the actual x86 render and measure the 90 MB limit before replacing normal Release. This is a separate trial for the requested top layout; the bottom variant's 92.35 MB failure remains recorded. No dependencies, system settings, native shell changes, mouse hooks or synthetic desktop input.

References: [GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos), [WPF property animations](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-animate-a-property-by-using-an-animationtimeline).

## Results, 10 November 2026

- Isolated x86 Release build completed with zero warnings or errors. The 20 focused tests passed, including saved placement, mixed-DPI geometry with a simulated display, cursor-driven reveal and retract, popup hold, and reopening with the previous CPU sample.
- A native x86 probe at 125% DPI placed the strip at y=5–48 and the CPU popup at y=54–909 on the same display. The WPF bar and 100%/150% popup render exports are in [cpu-300s](evidence/top-edge-reveal/cpu-300s). Real mixed-DPI displays, physical pointer animation, and a sustained hidden CPU measurement remain unverified.
- The combined live-GPU strip and CPU popup trial completed 300.997 seconds with 302 samples. Observation maximum was 89.141248 MB and average machine CPU was 0.377002%. Whole-process lifetime RAM reached 91.725824 MB against the 90 MB cap. Lifecycle, placement, worker cleanup, and binary hash checks passed. The overall gate failed with exit 2.

Normal Release was held at the prior build. Proposed follow-up is a controlled comparison of the same top strip and CPU popup with live GPU sampling versus a fixture-supplied GPU reading to isolate the provider's contribution. If that does not explain the peak, profile WPF popup startup and capture phases separately. Keep the 90 MB cap and require a full live-data run before release. This follow-up was not run.
