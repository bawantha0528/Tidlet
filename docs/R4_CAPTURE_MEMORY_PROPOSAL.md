# Capture-period memory correction at unchanged 90 MB

31 October 2026. User requested continuation after this pending review; proceed with this bounded correction at unchanged 90 MB. Static smaller-window passes are retained; the earlier navigation lifetime failure is not waived. New runs must pass independently.

## Executed results

860x580 DIP opaque WPF static trials pass: CPU lifetime 87.199744 MB / 0.3938861235% average machine CPU; RAM 88.547328 MB / 0.3406715262%; Dashboard 76.832768 MB / 0.0532107094%. Each full observation has 302 samples over at least 300 seconds. Earlier larger-window failures remain unchanged. Evidence: docs/evidence/r4-dashboard/small-{cpu,ram,dashboard}-300s.

New lifecycle build passes 105 active tests each on actual x64/x86 (70 Core / 14 Shell / 21 Windows), including disposal serialization across closing/reopening windows. Bounded navigation and after-close smokes exit naturally and drain workers. An older test failed during concurrent validation; targeted rerun passed. Replaced its fixed 3.2-second expectation with a ten-second bounded wait for two completed mock reads plus explicit pending-worker drain. The two-read and no-read-after-drain assertions remain; failed TRX evidence is preserved.

Full navigation trial: docs/evidence/r4-dashboard/navigation-300s. 300.006890 seconds / 301 samples, observation max 83.898368 MB, mean 78.473196 MB, p95 83.189760 MB; machine CPU 0.2897068881%, core equivalent 2.3176551045% across eight logical processors. Navigation visits 87 pages across CPU/RAM/Dashboard/Sensors, 200 requests / 156 frames, one window and close at 345.0044852 seconds. Actual target opaque, worker/timer/tray cleanup and seven binary hashes pass.

The observation-only summary is Pass, but final tail accounting rejects the run: lifetime rises from 88.551424 MB at tail start to **93.863936 MB**, over 90 MB by **3.863936 MB**. Tail sampled working-set maximum is 91.226112 MB. This period includes automatic 100/150 render exports and the last navigation; the exact allocation cause is not established. Do not claim that live navigation or export alone is proven responsible. No forced GC/trim, helper, hidden-window exclusion or cap change.

## Proposed bounded change

1. Add an explicit diagnostic capture-scale option so a run creates **one** export scale (100% or 150%), instead of constructing two RenderTargetBitmap/encoder graphs plus several DPI text-cache refreshes back to back. Keep normal application drawing, viewport, metric intervals and navigation cadence unchanged. Record requested/actual export dimensions and timestamps. Default product interactions are unaffected.
2. Perform two separate, sequential full navigation trials, one at each scale. Each uses the same 300-second observation and complete cold/lifetime/tail memory accounting. Both must pass 90 MB; never run the applications concurrently, shift capture to a helper, exclude capture costs or force collection. Compare image content/geometry with earlier exports and retain both complete results.
3. If either fails, stop again. If both pass, run full after-close CPU/RAM and actual owned-window hidden-work checks before converting more application pages. Existing short smokes are not full resource acceptance.

This removes an unnecessary double-export burst in a diagnostic path; it does not claim the new path will fit, and it does not change the RAM cap or excuse the current failure. A smaller initial viewport has already been approved; no further size reduction or dependency is proposed. No system setting, admin action or Git operation.

Known unfinished work: GPU/Disk/Network/Battery wide application pages, module settings layout, applied themes/units, accessibility/translation/icon polish, physical screenshots/mixed-DPI/Win10/real-game/hosted CI acceptance. The new hidden application fixture parameter is prepared but not executed after the budget failure. Normal Release remains the earlier R3 application; current diagnostic source/output is isolated.

AGENTS.md requires: "If a budget fails, STOP, report actual numbers and a proposal." This next change requires user review; overall UI authorization does not silently waive the measured cap.
