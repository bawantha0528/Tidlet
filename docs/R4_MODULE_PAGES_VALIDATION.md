# Remaining wide module pages — validation in progress

31 October 2026. Application-use lifetime limit 100 MB; fresh bar/popup and post-close current RAM remain 90 MB. CPU <1% over 300 seconds. No dependency, helper, system-setting, Git, forced collection or working-set trimming change.

## What was built

Dedicated wide GPU, Disk, Network and Battery application pages replace the remaining explanatory placeholders. Compact popups remain separate and open from the top bar. GPU histories retain actual adapter identities; physical disk counters and volume capacity remain separate. Network uses recorded traffic and actual adapter metadata without public-IP or connectivity requests. Battery shows only valid reported state/capacity/health/power/cycles. Missing readings are explained. Histories are lazy and bounded; offscreen drawing is culled.

## Executed checks

Release builds completed without warnings/errors. Final corrected suite passed **115 tests each on actual x64 and x86 runtimes** (70 Core, 20 Shell, 25 Windows), in TestResults/r4-module-checked-x64 and TestResults/r4-module-checked-x86. Isolated executable: src/IsletMonitor/bin/r4-module-checked/IsletMonitor.exe. Rendering coverage includes missing hardware, two GPU adapters, invalid values and scrolling; no real system-changing action.

An intermediate test in TestResults/r4-module-final-x64 failed because an impossible finite transfer rate could enter disk history even though its current value was hidden. Input is now rejected before history insertion; the failed evidence is retained and the corrected suites above passed.

Four 12-second live preview runs exited naturally with workers drained. GPU produced a 150% export; Disk, Network and Battery produced 100% exports. All four exports were visually inspected. These are WPF render exports, not physical mixed-DPI screenshots. Network artifacts remain ignored because adapter metadata can be private. These short runs are not resource acceptance.

## Resource gate

Full GPU trial started with:

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/modules-gpu-300s -Page GPU -Scenario Static -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-module-checked/IsletMonitor.exe
```

All four full trials completed and were accepted sequentially. MB are decimal; CPU is normalized across eight logical processors. Every observation contains 302 samples and exceeds 300 seconds. Complete lifecycle, activity, unchanged binary hashes, one 100% export, natural exit and drained-worker checks passed.

| Page | Observation seconds | Observation max MB | Lifetime/tail MB | Average machine CPU % | Reads / frames |
| --- | ---: | ---: | ---: | ---: | ---: |
| GPU | 300.9758841 | 85.651456 | 91.451392 | 0.1842963271 | 344 / 343 |
| Disk | 300.9933496 | 80.146432 | 88.555520 | 0.1992101738 | 344 / 343 |
| Network | 300.9941843 | 78.426112 | 82.018304 | 0.2770765744 | 345 / 344 |
| Battery | 300.9977114 | 75.685888 | 78.786560 | 0.2147804952 | 345 / 344 |

GPU, Disk and Battery raw evidence is in docs/evidence/r4-dashboard/modules-{gpu,disk,battery}-300s. Network evidence is private/ignored at artifacts/r4-modules-network-300s. The sequence verdict is artifacts/r4-modules-sequence.json. Remaining commands use the GPU command above with matching Page and OutputDirectory. No earlier result was relabeled.

Settings behavior/theme/accessibility work remains pending; the normal Release remains the frozen R3 build. Proceed with docs/R4_SETTINGS_BEHAVIOR_PLAN.md under the same resource policy. These trials do not certify every scrolled device/process view, physical mixed-DPI, Windows 10, games, screen readers or hosted CI.
