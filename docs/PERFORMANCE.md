# Performance protocol

Planning specification only; no measurements exist.

The authoritative protocol is [implementation plan, section 9](IMPLEMENTATION_PLAN.md#9-measurement-contract-and-stop-policy). This file is a quick checklist to carry into implementation.

1. Standalone Release app, no debugger; record executable hash, commit, runtime, Windows build, RAM, processor denominator, DPI/render tier, power state and widgets.
2. Fixed warmup (30 s ordinary, 60 s populated graphs), then >=300 actual seconds with monotonic elapsed time and 1-s sampling.
3. Identity by PID + start time; refuse recycle/early exit/incomplete or unreadable essential samples.
4. Collect full working set, private bytes, CPU time, handles and threads; output raw CSV and summary JSON.
5. Report decimal MB and MiB, working-set mean/median/p95/sampled max, startup/lifetime peak, actual CPU seconds, normalized and core-equivalent CPU. Sampled max is not proof of no shorter transient peak.
6. Collapsed: <=60,000,000 sampled bytes; normalized CPU <1%. Expanded: <=100,000,000 sampled bytes. Report both CPU formulations and denominator.
7. Hidden/fullscreen approximately-zero engineering target: <=0.1% core-equivalent, exact CPU seconds, zero metrics/graph/animation callbacks. Document any minimal fullscreen reconciliation.
8. Scenarios: scaffold baseline (M0 only), each collapsed style, populated expanded, tray-hidden, actual fullscreen, collapse after repeated expansion and collapse after settings/import/profiles.
9. No forced GC, working-set trim, affinity/power-plan/system tuning or omitted helper costs. Keep observer cost outside app and report it separately.
10. Failing/near-limit runs are retained and repeated with confounders documented. Unresolved WPF transparency failure means STOP and propose a renderer alternative before feature work continues.
