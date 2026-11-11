# Review after visibility correction — 31 October 2026

Status: proposal only; do not implement until approved. Preserve 90 MB, all features, x86/software preference, opaque 860x580 WPF application, bar/popups and normal Release R3.

The approved data-visibility correction passes 112 tests each architecture. Full unprofiled navigation: 300.0063513 seconds/301 samples, observation max 86.347776 MB, lifetime 91.734016 MB, machine CPU 0.1269504373%. Lifecycle, activity, hashes, one 100% export and natural cleanup pass; whole-lifetime RAM fails 90 MB by 1.734016 MB. Core detail requests 91, process requests zero in unscrolled navigation. The lifetime peak predates capture. Full static/after-close/hidden/150% trials did not follow this failure. See R4_VISIBLE_DETAIL_CORRECTION.md and raw evidence.

## Proposed next bounded correction

Make the wide application page's drawing work follow its viewport as well as its sampling. Current DrawingContext clipping controls visible pixels but the code still constructs text and geometry for offscreen sections. Maintain the full layout height and scroll access, but omit offscreen labels, rings, charts and row drawing from the retained visual. Reuse existing bounded resources; do not preload hidden page visuals. Preserve warming, capability errors and scroll activation of real metrics. This is a concrete candidate for reducing retained drawing/allocation costs, not a proven 1.74 MB saving or established root cause.

Also restore an explicit finite/range guard around core-bar fill geometry. Current native source rejects impossible values, but the renderer's new warming path must independently reject manually supplied infinity or values over 100 rather than constructing invalid geometry. Add a meaningful invalid-value rendering test alongside viewport/scroll tests. Do not claim current tests cover that renderer scenario.

Build/test x64 and x86. Check visible output and scroll transitions using deterministic mocks, retain all old evidence, then run one full unprofiled 300-second navigation trial with complete startup/capture/tail accounting and unchanged 90 MB. Stop on another failure. If it passes, resume the previously required static/after-close/hidden validation before extending other pages. No new dependencies, GC/working-set tricks, machine settings or Git operations. This approval would authorize this bounded rendering correction, not a cap increase or a claim that R4/R5 are complete.

Alternative: retain the current implementation and 90 MB requirement, leaving further production changes paused. No budget increase is inferred from any earlier approval.
