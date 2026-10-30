# Stats-inspired Tidlet interface

User request: adapt the supplied Stats screenshot's information layout, using original colors and Windows-native code. Extend the user's M3 branch; no M4-M7, new dependencies or Git writes.

1. Keep the approved native Win32/.NET 8 renderer. Use a charcoal, jade and amber palette, compact module tabs, round gauges, bounded history charts, separators and aligned detail rows.
2. Add a responsive overview plus individual module pages and a combined summary rail. Hide an absent battery from the overview. Preserve island/notch/bar and no-activation behavior. Read existing theme preferences without overwriting settings.
3. Pass structured, validated real readings to the renderer. Probe providers, stop expanded-only polling when collapsed or suppressed, reset delta histories on resume, and cap histories at 60 samples. Never fabricate sensors or macOS-only fields.
4. Replace the discovered disk placeholder with standard-user PDH disk rates. Reject the GPU placeholder as unsupported. Use existing real process working-set enumeration at a bounded cadence; add no process actions.
5. Test geometry/hit targets, missing and invalid values, failures, lifecycle demand and native resource cleanup. Build/run tests, inspect actual app frames, then measure expanded, collapsed and hidden states for 300 seconds each. Report results and hardware gaps honestly.

Reference: https://github.com/exelban/stats and its MIT license reviewed 30 October 2026. Screenshot used as layout inspiration. No Stats source, artwork, branding or colors copied. Unavailable Windows fields remain explicit.

## Latest user palette direction - 30 October 2026

The user now requests the reference's white and neutral-gray dark colors, with only slight changes and no green. This supersedes the earlier jade/amber direction. Change surfaces, text, default accents and paired chart colors to neutral white/gray plus blue/red. Keep saved settings files untouched; old green accents must render using the new blue fallback. Preserve layout, sensor behavior, animation and dependencies. Validate with Release build, existing tests, real light/dark page captures and pixel inspection. This is a cosmetic follow-up, not a new milestone; previous five-minute measurements remain historical until repeated on the new binary.
## Black notch control center - 30 October 2026

The user supplied a compact notch reference and requested its near-black background across the whole dark app. They explicitly approved adding working music controls and a focus timer. This is an authorized extension of the monitor scope; M4-M7, drivers and unrelated screenshot toolbar tools remain deferred.

Plan: (1) apply sampled near-black surfaces (#0D0C0F / #080809) to every dark view; preserve white mode and the existing no-green preference; (2) give the expanded Notch overview a 620x320 compact layout with media card, focus timer, CPU/RAM bars, network rates and buttons that open real monitor pages; keep the detailed full dashboard available; (3) add a portable 25-minute deadline-based focus timer with pause/resume/reset that needs no background ticking when hidden; (4) add a capability-based system media provider using the official Windows media-session API only after approval for its targeting/runtime package; no player/denied/unsupported states must be honest and disabled controls must not pretend to work; (5) build, run timer/layout/provider tests, render both compact and full views, inspect actual frames and measure the expanded Notch for 300 seconds. Never issue real media commands during automated tests; use fake session executors. No commits or machine-setting changes.