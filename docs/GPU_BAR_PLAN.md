# Live GPU bar and application toggle — 31 October 2026

Replace the GPU bar's `Details` placeholder with real usage from the existing verified DXGI/Windows GPU Engine source. For hybrid systems use the busiest reported adapter, never sum adapter percentages; retain per-adapter details in the popup/application. Explain missing readings, distinguish counter warm-up and show no fake zero.

Use one lazy asynchronous sampler driven by existing visible bar polling, not an additional timer. Probe/read/dispose are serialized; stop and drain when disabled, paused, hidden or exiting. No GPU query starts if the GPU item is off. Reuse existing Windows bindings with no new API/package/admin/helper/outbound request.

Keep the application GPU header toggle connected to saved item visibility. Add GPU to the user's saved bar items as requested, preserve other preferences, and leave future on/off changes under the existing toggle. Cover sampler validation/hybrid selection and pending-read stop/re-enable with mocks; verify application toggle via its accessible action against mock save/desktop boundaries. Build, focused tests, short real preview, then five-minute resource validation because this adds metric sampling rather than only drawing. Preserve active RAM/CPU limits and stop on failure. No Git or system-setting action.
