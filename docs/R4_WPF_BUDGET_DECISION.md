# WPF application RAM decision — 31 October 2026

Approved 31 October 2026: user selected "Approve application 100 MB". The policy below now applies to new measurements. Historical 90 MB verdicts remain unchanged; approval does not certify any existing trial. Implement and test the explicit gates before new validation.

The approved viewport drawing correction passes 113 tests per architecture and live CPU/RAM render inspection. Full unprofiled navigation completed at 300.9850745 seconds/302 samples: observation maximum 86.708224 MB, whole lifetime/tail **93.814784 MB**, machine CPU **0.0940920826%**. Lifetime fails 90 MB by 3.814784 MB; peak predates export. Lifecycle/activity/hash/capture checks passed, natural exit and workers drained. Completed evidence is recorded in R4_VIEWPORT_RENDER_CORRECTION.md and PROGRESS.md. Earlier 90 MB failures remain unchanged. These repeated trials do not establish one leak or a predictable cross-run allocation saving; no further speculative optimization or UI extension proceeds without review.

## Recommended decision: 100 MB for application-use sessions

Authorize a **100,000,000-byte whole-process lifetime cap for sessions that open Dashboard/Settings/module application pages**. Retain 90,000,000 bytes for fresh bar-only and popup-only sessions. For application sessions after the window closes, require current working set <=90 MB during the post-close observation; retain and report the earlier application lifetime peak against 100 MB, rather than resetting or concealing it. Aggregate every product helper/window/process, preserve <1% machine CPU over 300 seconds and hidden/no-offscreen-work rules. No GC/working-set trim or startup/capture exception. This policy distinction must be explicit in the measurement wrapper and reports before any run uses it.

Approval would authorize only that measured-budget policy and the pending UI/validation work already in scope. It would not declare any existing application run a pass, replace older failed verdicts, waive physical/functional checks, add dependencies or guarantee scrolled/full-feature views fit 100 MB. First validate full navigation under the explicit policy, then static CPU/RAM/overview, after-close and hidden scenarios sequentially. Stop on any future failure. Keep the frozen R3 normal Release until the replacement is verified. No Git or machine-setting operation.

## Alternative: retain 90 MB and leave implementation paused

Keep the existing hard limit and preserved source/evidence. No next optimization is assumed to be authorized; a further investigation would require a separately scoped, evidence-based proposal. No claim that WPF cannot ever reach 90 MB on any machine: the narrower conclusion is that this application's tested paths have repeatedly exceeded it on this machine.
