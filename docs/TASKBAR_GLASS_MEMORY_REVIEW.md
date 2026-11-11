# Bottom strip and glass popup budget review

The combined x86 bottom strip (CPU, GPU, RAM, Network, Battery) and translucent CPU popup crossed the 90,000,000-byte lifetime working-set cap. The completed 300.9672224-second trial measured 92,348,416 bytes lifetime peak, 89,661,440 bytes observation max and 0.320581% average machine CPU. Native placement and clean shutdown passed; the aggregate resource gate failed (exit 2). Evidence: `docs/evidence/taskbar-glass/bottom-cpu-300s`. Normal Release was left in place. This aggregate run cannot identify the expensive path: it includes the new bottom drawings, the translucent popup and the existing optional GPU provider.

## Proposed correction at the same cap

1. Use two bounded fresh-process diagnostics with the same bottom window, CPU popup, module order, and renderer. Keep the GPU slot visible in both; mock GPU readings in one and use the existing live provider in the other. Record startup, first GPU reading, popup activity and shutdown through the existing memory phase recorder. Include all auxiliary costs. Treat these as attribution, never as five-minute acceptance.
2. If the provider adds the extra peak, inspect its startup and retained allocations. If the common drawing path does, profile the actual WPF resources and remove redundant live allocations or reuse geometry. Preserve real readings, translucent styling, bar position and monitor controls. Only change what the comparison supports.
3. Rebuild, run focused tests and repeat the complete five-minute bottom bar with live GPU and CPU popup. Count startup, render exports, and shutdown. Release only if the lifetime cap and CPU limit pass. Stop again on failure.

No cap increase, new dependency, forced collection, working-set trim, or Windows settings changes. Await user direction per AGENTS.md before any further implementation or diagnostic experiment.
