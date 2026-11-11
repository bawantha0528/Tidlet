# Application layout correction and memory review

31 October 2026. User approved the bounded correction at unchanged 90 MB. Historical failed evidence below is retained; new implementation requires fresh measurements. No budget revision requested.

## Requested presentation

The latest five references clarify two separate presentations. Only a top-bar click opens the narrow anchored popup. Application sidebar pages must use the full content width, with summary cards, wide history charts and detail sections; do not embed a 320-DIP popup in the application. Reuse portable data/math/drawing primitives, not popup window layout.

CPU: summary ring with System/User/Idle segments, actual processor identity, wide usage history and per-core bars. RAM: summary ring/Windows memory breakdown, wide history, pressure and page-file sections. GPU: separate real adapters, utilization and histories; do not add macOS ANE/tiler/FPS without verified Windows sources. Disk: actual volume capacities and physical-instance read/write history, without inventing a volume-to-disk mapping or SMART values. Sensors: explanatory NeedsHelper/off page with no helper launch or fan controls. Network/Battery follow the same wide-card approach. Module settings occupy a separate settings view; use only supported Windows settings. Bluetooth/Clock/Remote shown in the reference are not added to scope.

All five new reference PNGs are copied into ignored docs/reference. They are visual references, never product assets. Current production icons remain installed Windows glyphs/original geometry. No Apple traffic-light assets or other imported icons.

## Executed evidence

Current development build: bin/r4-development. Zero-warning Release build. Corrected eight-second Dashboard, Settings and embedded CPU smokes exited naturally, timers/tray cleaned up and workers drained. First older smoke failed with ObjectDisposedException during shutdown; fixed by early Stopping notification and IsDisposed guard, not hidden from the record. Current tests: 101 each on actual x64 and x86 runtimes (70 Core / 10 Shell / 21 Windows), including settings preference roundtrip and invalid/future/oversized import cases. This does not certify all settings actions or UI interaction.

Full trial: docs/evidence/r4-dashboard/overview-300s. 300.9826999 seconds, 302 samples, eight logical processors. Observation max 89.681920 MB; lifetime peak **94.892032 MB**, exceeding 90 MB by **4.892032 MB**. Average machine CPU 0.0467219545%; core equivalent 0.3737756357%. No forced collection/working-set trimming. Seven binary hashes unchanged. Whole bar/application process counted; no auxiliary host.

The numerical observer reports Pass for its observation window; the wrapper rejects the run because lifetime RAM exceeds 90 MB. The requested fixed Dashboard-only scenario also failed: two Dashboard opens and 18 module reads/11 frames occurred during the interactive run instead of one static overview with no module work. Therefore do not label this an isolated Dashboard overview pass or infer its exact memory cause. The observed whole-application RAM failure still applies. Natural exit, worker draining, timers/tray cleanup passed; no fullscreen interruption. No additional UI implementation follows this failure.

## Proposed bounded correction at unchanged limits

1. Replace application ModulePresenter's embedded PopupVisual with a dedicated wide-page renderer and full-width cards. Top-bar PopupCoordinator alone owns compact popups. Start with CPU/RAM and overview to prove the approach before applying it to other pages.
2. Reuse frozen page geometry/brushes and bounded last-value text entries; avoid reconstructing sidebar hit targets on each metric update. Serialize old/new module worker disposal across navigation so repeated switching cannot accumulate native query work.
3. Keep one lazy application window and one active visible module. Closing, pause or fullscreen suppression disposes its provider; opening a top-bar popup closes the application view's metric work. No data fabricated and no background graph/process work introduced.
4. Add diagnostic page/navigation history and a held-input mode to distinguish a static-page trial from a navigation stress trial. These are bounded diagnostic fixtures only; interactive behavior stays normal. Do not suppress lifetime costs or exempt capture/startup.
5. Build/test both architectures; render CPU/RAM application pages and unchanged bar popups at 100/150 export scales. Measure full 300-second overview, CPU/RAM, repeated navigation, after-close and hidden scenarios with unchanged 90 MB and <1% CPU limits. Stop again on failure. No dependency, system setting, Git operation or memory trimming.

This proposal does not claim that caching or the new layout will meet the budget. Remaining module-page conversion, settings/theme/accessibility polish follows only after this correction passes. User approval is required by AGENTS.md: "If a budget fails, STOP, report actual numbers and a proposal."
