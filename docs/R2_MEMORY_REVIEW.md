# R2 memory review: preserve the limits, optimize before acceptance

30 October 2026. Status: user approved the bounded correction and subsequent monitor UI completion; validation pending. R1 bar limit remains 75,000,000 bytes; popup remains 90,000,000 bytes. No startup exemption, dependency, architecture change or new renderer is approved. See UI_COMPLETION_PLAN.md for the extended scope; budget failures still stop work.

## Concrete result available for review

The x86 WPF bar and CPU/RAM popup kit are implemented. R1 Reserve measurement passed at 63.426560 MB peak / 0.0473672831% average machine CPU. An owned maximized window stayed below the reserved strip, and normal exit restored the original work area on this 125% display. Physical mixed-DPI and crash/kill recovery remain unverified.

CPU popup held-open trial: 30-second warmup plus 300.0163839 seconds / 301 samples. Numerical window peak 88.498176 MB, mean 76.912225 MB, p95 80.658432 MB; CPU delta 8.625 seconds = 0.3593553745% machine normalized over eight logical processors / 2.8748429962% core equivalent. Numerical window passes 90 MB, but lifetime startup peak was 91.062272 MB: 1.062272 MB above the popup cap. No startup exception is assumed, so overall acceptance is blocked. Evidence: evidence/r2-popups/cpu-held-300s, raw-check.json distinguishes these results.

One CPU open at 0.0018 seconds and one close at 344.9942; 343 presented frames / 344 read requests, zero outstanding workers, drained cleanup, no hidden-state interruption, timers stopped and native tray deletion returned success. Raw CSV replay agrees; seven recorded Release hashes match. No helper processes, collection/working-set manipulation or runtime installation. The earlier mixed CPU/RAM trial is explicitly rejected, not substituted as a pass.

The diagnostic UI thread reported 70,434,840 allocated bytes across 343 draws (about 205 KB/draw), excluding worker allocations. This is cumulative allocation, not retained RAM. Fixed ring Push/Average operations passed zero-allocation tests; the full WPF presentation path is not allocation-free. Both final test runtimes passed 93 active cases. Hosted CI, full RAM/after-close/hidden trials and physical 100%/150% screenshots are not certified. Current 100%/150% images are render exports; local reference comparisons are in ignored docs/reference/r2-comparison.html.

## Proposed next bounded change, requiring review

1. Keep data definitions, capability handling, layout, WPF/x86 stack and 75/90 MB caps unchanged. No R3-R5 feature work.
2. Remove unnecessary reflection-based theme deserialization using .NET 8 JsonDocument or its existing source generator. Add no package. Cache immutable section labels/typefaces/pens and reusable chart point storage; avoid rebuilding unchanged drawing resources. Do not prewarm resources merely to shift uncounted costs into the bar, use GC/trim tricks or discount any window/process.
3. Test the unchanged metric math and failure cases plus popup open/close/worker lifetime on both architectures. Measure cold-open lifetime peak as well as a full 300-second CPU and RAM popup trial. Then run full after-close <=75 MB and hidden CPU checks with all costs included. Keep raw data/hashes/state history and reject interrupted scenarios.
4. Savings are a hypothesis, not a guarantee. If a cap still fails, stop again. No budget relaxation or profiler/tool download is approved by this proposal.

Approval would cover only this R2 memory/allocations correction and validation, followed by the requested R2 review stop. Alternative: retain this preview and stop here for UI review.
