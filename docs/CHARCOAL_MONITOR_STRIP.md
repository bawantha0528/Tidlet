# Charcoal monitor strip

Apply the reference to the existing monitor strip in standard top, hover-reveal and bottom placements. Use a charcoal background, a CPU utilization ring, a bounded GPU history line, a warm RAM meter, red upload and blue download dots, and the existing green battery indicator. Keep live readings, placement, saved ordering, module hit regions and popup behavior. Validate the isolated x86 preview against the existing resource limits.

The GPU line keeps up to 20 valid samples from the existing bar reads. The layout falls back to narrower text readings when there is insufficient room for meters. Drawing uses existing WPF geometry and frozen brushes. [WPF geometry commands](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.streamgeometrycontext) · [WPF gradient coordinates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/painting-with-solid-colors-and-gradients-overview)

## Verification

- Isolated Release solution build completed with zero warnings or errors. Seventeen focused tests passed in the x86 test configuration.
- Ten-second x86 previews rendered top, bottom and hover-reveal placements on a 125% display. Each showed a valid GPU sample and shut down cleanly. The hover fixture opened its CPU popup below the strip. Preview captures are WPF exports under `artifacts/charcoal-strip-preview`.
- The live-GPU bottom strip passed the five-minute bar-only gate. Over 301.001 seconds and 302 observations it reached 78.106624 MB observed and 78.147584 MB lifetime working set, with 0.054506% average machine CPU. The probe recorded 115 bar samples and confirmed no closed-popup chart or process work. [Scenario check](evidence/charcoal-monitor-strip/bottom-gpu-300s/scenario-check.json)
- The new visuals are available in `src/IsletMonitor/bin/charcoal-strip`. This run covers the bottom bar with no popup. The earlier hover-popup and open Dashboard acceptance gaps remain.
