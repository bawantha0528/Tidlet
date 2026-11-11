# Application-session policy validation — 31 October 2026

User explicitly approved 100,000,000-byte application-use-session lifetime RAM. Fresh bar/popup sessions and post-close/hidden current RAM retain 90,000,000 bytes. CPU <1% machine over 300 seconds; suppressed CPU <=0.1% core equivalent and no hidden metric/chart/process work. Full startup/capture/tail peaks are retained. Earlier failed 90 MB verdicts remain unchanged.

## Implemented and tested policy

scripts/Measurement.psm1 adds an explicit Application scenario plus Test-SessionMemory. Dashboard and hidden wrappers persist current/lifetime limits and verdicts separately. Tail observations and binary hashes are retained. Tests passed: 18 policy/boundary checks, 24 existing measurement checks, eight worker-aggregation checks. Changed script syntax parsed successfully. No C# source/binary change from the prior Release/113-tests-per-architecture baseline at bin/r4-viewport-render; those results are not a newly executed C# suite for this scripts-only step.

## New full trials

All MB decimal; machine CPU normalized across eight logical processors. Each row has a fresh process and >=300-second observation, natural exit, unchanged binary hashes and accepted scenario checks.

| Scenario | Seconds / samples | Observation max MB | Lifetime/tail MB | Machine CPU % | Evidence suffix under docs/evidence/r4-dashboard |
| --- | --- | ---: | ---: | ---: | --- |
| Navigation, 100% export | 300.9938242 / 302 | 87.150592 | 91.389952 | 0.1200450295 | app100-navigation-100-300s |
| Held CPU, 100% export | 300.9797699 / 302 | 77.795328 | 87.617536 | 0.2887704464 | app100-static-cpu-retry-300s |
| Held RAM, 150% export | 300.9799693 / 302 | 81.383424 | 84.983808 | 0.1395182131 | app100-static-ram-150-300s |
| Overview, 100% export | 300.0083663 / 301 | 79.298560 | 79.306752 | 0.0292960580 | app100-static-dashboard-300s |
| After CPU close | 300.0022651 / 301 | 82.112512 | 86.802432 | 0.0299476906 | app100-after-cpu-300s |
| Hidden after CPU | 300.9833484 / 302 | 78.344192 | 85.884928 | 0.0032445732 | app100-hidden-cpu-interactive-300s |

Navigation made 200 reads/156 frames. CPU held 344/343, RAM 345/344. Overview made zero module reads/frames. Post-close held 16 reads/15 frames unchanged from its baseline. Hidden held eight reads/seven frames unchanged from baseline, zero HiddenMetricReads/ChartWork/ProcessWork, actual hidden-window proof from EnumWindows, 822 foreground checks and zero failures. Workers drained, timers stopped and tray removed.

## Preserved invalid attempts and fixture recovery

First CPU attempt app100-static-cpu-300s numerically passed at 88.653824 MB lifetime / 0.2005142005% CPU, but auto-hide closed the page at 237.14273 seconds and prevented capture. It remains rejected, not a held-view pass. Exact suppression cause was not recorded. User then confirmed the desktop could stay visible; fresh retry above passed.

Initial app100-hidden-cpu-300s failed the fixture startup activation assertion; no hidden acceptance is claimed. The retry adds an opt-in interactive fixture setup that waits up to 60 seconds for independently verified own-window foreground. No forced-focus workaround or settings change. Computer-use skill initialization/window inventory found no targetable fixture; no click or synthetic input was issued. Foreground was independently acquired and verified by the harness before observation. The product's natural-exit bound becomes 420 seconds, with an extended external tail observer accounting all remaining costs; fixture-context.json records this. All product costs remain included. The external fixture/observer/tooling are test infrastructure, not shipped Tidlet helper processes.

## Limits and next work

This validates the frozen CPU/RAM/overview application baseline and associated post-close/owned-fullscreen paths on this machine. It does not validate remaining wide GPU/Disk/Network/Battery pages, full scrolled process-view costs, themes/settings behavior or every option. 100%/150% PNGs are render exports; actual physical monitor was one 125% display. Physical mixed-DPI, Windows 10, real games, accessibility readers and hosted CI remain unverified. Normal Release remains R3 until the replacement is verified.

Proceed with already authorized remaining monitor UI under the same limits; each feature stage needs fresh build/tests/resource evidence. No new dependency, optional hardware helper, fixer, creature/media, machine setting, GC/trim or Git operation.
