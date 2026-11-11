# Post-popup RAM gate: review required

30 October 2026. This proposal is NOT approved. User approved the R2 correction and remaining R3-R5 UI, with unchanged 75 MB bar / 90 MB popup resource gates. Corrected open CPU and RAM trials pass. The completed after-CPU bar trial failed at 77.950976 MB; full results follow below. No startup exemption, forced collection, trimming or further feature expansion is authorized.

## Changes already validated

The frozen x86 WPF correction uses direct theme parsing, shared frozen resources, bounded last-value text reuse and direct chart geometry streaming. CPU: 78.819328 MB observation peak, 81.391616 MB lifetime, 0.3594905013% average machine CPU over 300.9902198 seconds. RAM: 79.896576 MB observation peak, 82.391040 MB lifetime, 0.4029933887% over 300.0010444 seconds. Both retain raw data, unchanged binary hashes and successful lifecycle proof. These results cover bin/r2-correction, not the newer preserved Network/Battery workspace edits.

## Review choices

1. Keep bar <=75 MB and authorize one bounded close-path resource-release experiment. Explicitly clear the popup drawing commands and its application-owned text/sample references on close, verify source/timer/root teardown, and repeat the complete open/after-close trials. No forced GC, working-set trimming, prewarming, dependencies or system changes. This may reduce resource retention; it is not guaranteed to change working-set residency or pass. Stop again if it fails.
2. Explicitly revise the bar-after-popup limit to 90 MB (same whole-app working-set cap as open popups), retaining CPU <1%, no hidden chart/process work, all accounting and future stop rules. This is a budget change, requires explicit approval, and does not certify the remaining UI or pending hidden/current-source trials.

Do not infer a universal WPF memory floor or a confirmed leak from these separate measurements. Do not start another feature/experiment until the user chooses a direction after the final report. Existing source is preserved, all development remains in this folder, and no Git/system/security action has been taken.

## Completed after-CPU trial

300.9917463 seconds / 302 samples. Bar observation peak 77.950976 MB (2.950976 MB above 75 MB), mean 57.469864 MB, p95 77.619200 MB; lifetime peak including the legitimate initial popup was 78.491648 MB. CPU 0.0298492404% machine / 0.2387939234% core equivalent over eight logical processors. Popup closed at 15 seconds; 14 frames / 15 reads before close, with no further chart/process work. Workers drained, timers stopped, tray deletion succeeded, binary hashes unchanged, raw CSV replay agrees. Evidence: docs/evidence/r2-popups/correction-after-cpu-300s. This is a memory FAIL, not an idle-CPU failure. No further RAM-after-close/hidden trial or feature expansion follows without direction.

The user clarified the newer Network/Battery additions were experimental and requested removal. They have been removed from the active popup source/schema/view/routes/tests, with a local ignored backup under artifacts/retired-network-battery-experiment. Genuine bar network/battery providers remain. Their replacements should match the CPU/RAM popup kit after resource review. This cleanup is not R3 completion or a new memory pass.

## User decision — 30 October 2026

Approved: "Approve a 90 MB bar limit; keep popup limit at 90 MB". Both active RAM caps are now 90,000,000 bytes. CPU, hidden-work, no-trim and complete-accounting rules remain unchanged. Historical 75 MB failure is preserved. This approves the budget revision, not the optional close-path experiment or an automatic acceptance of unexecuted scenarios. Continue the pending validation and approved monitor UI completion.
