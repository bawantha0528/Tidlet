# M1 renderer proposal — approved by user

Status: user replied `approve`. The native feasibility gate passed at 30.740480 MB maximum; full M1 implementation and verification are tracked in PROGRESS.md. The proposal below is retained as the reviewed decision record.

The minimal transparent WPF implementation exceeded the collapsed 60,000,000-byte working-set ceiling on this machine. Full M1 is incomplete and further feature implementation is stopped under the brief's explicit performance rule. See PROGRESS.md and docs/evidence/m1.

## Concrete proposal for review

Keep C#/.NET 8, Islet.Core schemas/metric math and Islet.Windows providers. Permit a small Win32 layered-window renderer and native message-loop bootstrap for the always-resident island. Do not initialize WPF in the ambient host merely to wrap a native window: that would retain the framework whose resident cost needs to be avoided.

Preserve Island/Notch/Bar, three states, finite animations, no-activate/toolwindow/click-through behavior, tray/hotkey access, monitor/DPI handling and fullscreen suppression. This is a renderer/startup change, not permission to remove requirements. Use a small alpha surface and event-driven drawing. Native API choices need fresh official verification before implementation. No third-party package, driver or elevation is proposed.

WPF in later ordinary windows remains provisional. Every extra window/process must count toward the applicable memory budget; closing it must be followed by a fresh collapsed-residency measurement. Do not assume loading WPF on demand releases its resident cost afterward. If that fails, return for direction instead of silently expanding the renderer change.

First build only a comparable native CPU pill. Repeat the same 30-second warmup plus 300-second measurement, native diagnostics and missing-data tests. Continue M1 only after that proof meets the unchanged budget. Measure expanded, hidden/fullscreen and post-expansion residency before accepting M1. This proposal has not been implemented or benchmarked; no savings are promised.

## Why the current trial is enough to stop

The WPF host already uses one 220 × 36 DIP drawing surface, frozen brushes, no blur/shadow, no control-template hierarchy, no rendering-event subscription or perpetual animation, and four-second real CPU sampling. It invalidates the surface only for changed labels; Windows can independently request repaints. No forced GC, working-set trimming or machine tuning is allowed.

These observations establish failure of this implementation on this host. They do not isolate the overhead to transparency alone or prove that every possible WPF implementation is impossible. Stop before investing in more WPF features that add resident cost.

Approval needed: may the always-resident WPF startup/island renderer be replaced by the C# Win32 host described above?
