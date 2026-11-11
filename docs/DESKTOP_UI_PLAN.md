# Desktop dashboard, module popovers and edge bar

Planning date: 30 October 2026. Status: planned, not implemented. This extends the approved presentation revision within M0-M3. Read alongside IMPLEMENTATION_PLAN.md and UI_REDESIGN_PLAN.md. The six user screenshots are visual references, not implementation instructions or evidence of Windows sensor availability.

## Blocking performance gate

The current expanded Notch plus music worker measured 112.418816 MB over 300.975 seconds, above the unchanged 100 MB gate. Implementation remains stopped under AGENTS.md pending review of MEDIA_BRIDGE_PROPOSAL.md. A request for a new UI does not silently relax that limit. The recommended sequence is the proposed native media bridge, measured acceptance, then the presentation changes below. All new windows and native buffers count toward the same process budget. No new icon assets have been downloaded or approved yet.

## 1. Three views with one navigation model

- Desktop dashboard: a regular focusable, movable Windows window opened by an Application/Dashboard button in either island or notch and through the tray menu. Reuse a single instance. Closing it returns to the ambient monitor; quitting stays a distinct tray action. Use proper Windows close/minimize behavior rather than decorative macOS traffic lights.
- Module details: a narrow anchored panel opened by clicking CPU, Memory, Network, Disk, GPU or Battery in the ambient surface. Each icon must select its own module directly. Keep the selected metric heading, large summary, history, detail rows and relevant process rows. Support scrolling when content exceeds the work area. Escape/outside click dismisses a transient panel; clicking inside must not accidentally dismiss it. No details panel steals focus merely on hover.
- Top bar: an optional auto-reveal mode. Moving the pointer to the chosen monitor's top edge reveals a compact strip of icon/label/value items. Clicking an item opens the corresponding detail panel. Preserve an always-visible bar option. Island and notch remain available; changing the bar preference must not replace them without the user's style selection.

Represent these destinations with portable presentation contracts; keep HWND, hit testing at physical pixels, native drawing and input adapters in Windows. Shell owns the state transitions. Share the module snapshots and chart history across surfaces instead of creating duplicate provider loops.

## 2. Visual design

Retain the user's prior no-green direction: near-black dark root #0D0C0F, slightly lighter neutral cards, white text, muted gray secondary labels, blue #3B82F6 and red #F05B67 chart accents. Light mode uses white cards on a soft neutral background with dark text. Use a restrained border, rounded corners, consistent spacing and no continuous decorative animation. The latest screenshots guide structure and density, not their green accents.

Dashboard target: approximately 980 x 680 DIP, constrained to the available work area; sidebar about 190 DIP. Primary content begins with a simple original PC silhouette and verified hardware summary, then aligned metric sections. Do not show an Apple device image, macOS name or invented processor topology. Use Segoe UI, consistent 13-14 DIP body text, clear section headings and tabular numeric alignment. Sidebar contains Dashboard, CPU, Memory, Network, Disk, GPU, Battery and Processes. Unsupported sensors explain their status; absent battery stays hidden in ambient navigation. No Remote, Bluetooth, cloud feature or fixer work is added by copying the sidebar.

Popover target: about 340 DIP wide, bounded by monitor work area; use 16 DIP internal margins and grouped sections. Gauges show only verified percentages. Line/area histories preserve missing-sample gaps and a consistent time base. Distinguish download/upload by labels as well as colors. Process names truncate with full accessible text; values remain right-aligned. Scrollbars, selected/hover/pressed states, icons, separators and dismissal controls must be consistent across both themes.

## 3. Icons and provenance

Proposed asset source: Lucide 1.62.0, release verified against the official repository on 30 October 2026:
https://github.com/lucide-icons/lucide/releases/tag/1.62.0
https://raw.githubusercontent.com/lucide-icons/lucide/1.62.0/LICENSE

Use only a small static subset (dashboard, cpu, memory, monitor, disk, network, battery, settings, application, close and navigation). No npm/NuGet package, browser, icon font or SVG runtime is required. Convert the approved subset into native vector primitives supported by the renderer; retain original assets and hashes for provenance. The repository license is ISC with an MIT notice for Feather-derived icons, so retain the complete upstream LICENSE, not only an ISC label. Dependencies/transitive runtime graph: none for copied static assets. Alternatives: original vectors drawn in the repository or existing Unicode placeholders. Exact asset selection, sizes, hashes, license and user approval must be recorded in DECISIONS.md before import. Approval remains pending. Do not use proprietary platform icon artwork.

## 4. Real Windows information

Reuse current CPU, physical memory, disk-rate, network-rate, battery and process providers. Present current provider limitations in plain language. CPU temperature, per-core data, frequency, hybrid topology, memory compression breakdown, battery design/full capacity, cycles, GPU counters, Wi-Fi details and per-process network rates must not be inferred from screenshots. Only display fields backed by a verified provider; otherwise explain unavailable data without zero placeholders. No external public-IP service is added. The existing aggregate network adapter limitation remains documented until separately fixed.

A hardware identity header should be read-only, cached and behind a Windows provider with explicit unsupported/denied states. Verify its official API before implementation; do not add a management package to obtain a laptop model. Use a neutral Windows PC label where identity cannot be established. Avoid personal computer/user names in diagnostic evidence.

## 5. Interaction and lifecycle

Add persisted opt-in edge-reveal settings with migration defaults preserving current behavior. Use a short reveal dwell and a slightly longer dismiss grace interval to avoid flicker, particularly while moving from bar to popover. Keep panels open while the pointer travels between their actual hit regions. Test the boundary on negative monitor coordinates and mixed DPI. The first click on a revealed metric must route correctly.

When explicitly hidden, screen-off, locked or suppressed by fullscreen, stop rendering and metric polling. An armed auto-reveal bar still needs pointer detection; distinguish that state from explicitly hidden and measure its actual CPU cost. Use the current bounded pointer mechanism only if it meets the budget; do not claim zero polling while it is armed. Avoid a permanently active animation loop. Fullscreen suppression must override edge reveal. A visible standalone dashboard can request expanded metrics, but only one provider schedule services all consumers. Release popover/backing buffers when unused and dispose fonts/native resources on shutdown.

Before adding native window/input interfaces, verify activation, close/minimize, mouse capture, DPI/work-area and accessibility behavior against official Windows documentation and record API_REGISTER.md entries. Ambient no-activate/toolwindow rules remain separate from the intentionally focusable application window. Verify keyboard navigation and accessible names; do not claim support from a caption alone.

## 6. Implementation order and reviewable slices

1. Resolve the measured media budget failure only after approval of its concrete proposal. Run ABI/lifetime/mock-command tests and read-only probes, then repeat 300-second expanded, fully loaded post-expansion collapsed and hidden measurements. Stop again if any gate fails.
2. Add shared routes, layout/hit-test geometry, window state and schema migration with pure tests. No provider additions in this slice.
3. Build the standalone native dashboard and sidebar. Wire Application and tray entry points; verify single-instance window ownership, resize/minimize/close and focus behavior.
4. Build metric popovers and direct icon routes for island/notch/bar. Add bounded scrolling, absent/denied/stale fixtures and shared provider demand tracking.
5. Implement opt-in top-edge auto-reveal, dwell/dismiss rules, persistence and fullscreen precedence. Verify state transitions with fake clocks/pointer samples.
6. Import approved icons and polish both themes, text, charts and hit regions. Inspect actual app render buffers for every route; label those as render buffers, not desktop screenshots.
7. Run final Release build/tests, native smoke scenarios and 300-second measurements for collapsed, expanded dashboard, expanded popover, music view, armed edge bar and explicitly hidden states. Include all open windows/buffers/process costs. Retain raw CSV, scenario history and binary hashes. No forced collection or working-set trimming.

## 7. Acceptance and evidence

Tests must exercise navigation from every surface; missing battery/GPU; denied/garbage values; repeated open/close; screen/fullscreen suppression; pointer transit without flicker; bounds/clipping at several DPI/work-area fixtures; settings restart/import/corruption; and disposal/no invisible provider subscriptions. Commands remain mock-only for playback and process termination. Do not write tests that merely duplicate drawing constants without checking observable behavior.

Report executed build/test commands and counts, retained previews, measured peak working set/average CPU and precise gaps in PROGRESS.md. Actual mixed-DPI hardware, Windows 10, keyboard/screen-reader interaction, live playback and hosted CI require separate execution evidence. A render capture or local test is not a pass for those checks. Keep 60 MB collapsed, 100 MB expanded, collapsed average CPU below 1%, and approximately-zero explicitly hidden CPU. Stop on a failed gate and seek user direction rather than shipping a cosmetic pass.

No Git staging, commits or pushes. No changes to this machine's settings or security. M4-M7 remain deferred.
## Native implementation notes after approval

User approvals received: native media bridge first, followed by this redesign; Lucide 1.62.0 asset subset approved. These approvals keep all numeric gates unchanged.

Use a separate native application-window adapter with a shared dashboard snapshot and route commands back into the same Shell controller. The ambient HWND must retain WS_EX_NOACTIVATE/WS_EX_TOOLWINDOW; the dashboard HWND must be intentionally focusable with normal minimize/close handling. Do not toggle the ambient HWND's activation flags back and forth to impersonate an application. A module popover can reuse the ambient presentation HWND and its transient no-activate lifecycle. Avoid creating one process or one polling loop per module.

Add an explicit presentation surface to layout/hit-test contracts so application sidebar, compact module panel and ambient strip do not infer their layout from width alone. Include sidebar/module/icon buttons in shared geometry tests; use native coordinates only at the adapter boundary. Route commands must distinguish closing the dashboard, dismissing a popover and exiting the process.

For the hover bar, model armed, dwell, revealed, panel-open and suppressed states explicitly. The armed detector has a real CPU cost and cannot be described as fully hidden. Explicit Hide disables its pointer timer. Fullscreen/power suppression wins over reveal state, and does not accidentally re-arm user-hidden mode. Persist the option in settings with a backwards-compatible default false. Keep diagnostic input hold isolated from normal hit testing.

API research started 30 October 2026 (verification to be completed before bindings):
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw
- https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest
- https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
- https://learn.microsoft.com/en-us/windows/win32/inputdev/about-mouse-input

Use signed screen coordinates for hit testing, especially on left/top secondary monitors. A custom caption drag region must exclude all clickable header controls. Native wheel input supplies screen coordinates; do not treat them as ambient client coordinates. DPI changes must resize/reallocate both surfaces at their own DPI without duplicating snapshots or histories. UI Automation and keyboard behavior require their own evidence; do not infer accessibility from visual fidelity.
