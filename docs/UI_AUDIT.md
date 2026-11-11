# UI audit

Review the active strip placements, seven monitor popups, and Dashboard pages in Dark and Light using x86 diagnostic renders. Exercise settings, popup lifetime, and missing readings in tests. Prioritize reproducible defects and reduce drawing and sampling work without changing metrics or resource limits. Use only owned probes and mocks for actions. Refresh the README from current renders and keep unverified hardware and performance scenarios explicit.

## Corrections

- The strip refreshes meter geometry and click targets immediately after a size or DPI change. A redraw never inserts a duplicate GPU history sample. CPU, GPU, and RAM honor the TwoLine setting.
- Popups skip offscreen chart and text work, keep the scroll position in bounds as data changes, and support Page Up and Page Down. Long label/value pairs stay in separate columns. CPU core bars fit within their slots. Disk history waits for complete rates.
- Rapid popup switches wait for the previous provider to dispose. The Sensors popup stops after one completed capability read.
- Dashboard settings create click targets only for rows intersecting the viewport. Keyboard tabbing can move backward, focus is retained when the same target remains visible, and a strip-only toggle leaves the open Dashboard module and history intact. Battery charge and state text are centered.
- Diagnostic bar-only runs ignore UI requests that would open an unplanned popup or Dashboard.

## Verification

Release built cleanly in `bin/ui-audit`. All 143 active tests passed separately in x86 and x64 configurations. The repeatable [UI smoke script](../scripts/smoke-ui.ps1) passed all 40 Dark/Light combinations on a Windows 11 desktop, with top, bottom and hover strips, every popup, every monitor page, Dashboard, Settings and About. Popup and Dashboard exports were rendered at 100% and 150%. Real power-setting writes were excluded.

Three-frame CPU popup probes recorded 1,337,840 → 1,175,076 bytes of managed presentation allocations in Dark and 1,383,952 → 1,157,112 in Light. Both Sensors probes went from three frames to one. These are short live-data comparisons. Other modules varied, and these figures do not establish whole-process RAM or five-minute CPU.

The existing Tidlet instance prevented a fresh five-minute measurement. The audited preview is isolated. The earlier Light mixed-session overage and full Dashboard scenario gaps remain open. Further performance validation requires a clear desktop with the old instance closed. Physical mixed-DPI, Windows 10, real fullscreen behavior, and assistive technology were outside the executed matrix.
