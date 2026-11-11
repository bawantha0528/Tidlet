# Warm start — 31 October 2026

User requested recent or placeholder content immediately when opening the bar/application, followed by quiet background refresh.

1. Show a static, shaped loading skeleton on a cold open rather than empty cards, unavailable errors or fabricated readings. Explicitly label it as preparing live readings; no invented percentage, history, device or process.
2. Retain a small bounded, session-only cache of recent real module samples, shared by application pages and top-bar popups. Expire after two minutes, discard process lists and private network metadata, and mark warm content as previous readings until replaced. Do not seed live histories or averages from old samples or fabricated points. No disk persistence, outbound traffic, packages or extra polling.
3. Present completed background samples immediately on the UI dispatcher, independently of the next polling deadline. Preserve single pending read, configured cadence, pause/visibility gating and serialized disposal. Existing errors and absent hardware replace loading with accurate explanations.
4. Preserve bar items during rebuilds with the last real snapshot; on first launch show compact loading marks. Add bounded-cache/expiry/privacy, cold/warm rendering, immediate first-frame and polling/disposal tests. Build/test actual x64/x86 and run relevant five-minute resource trials under existing limits. Never infer a pass if tooling blocks execution. No Git or machine setting action.
