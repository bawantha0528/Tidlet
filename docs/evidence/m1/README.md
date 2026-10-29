# M1 feasibility evidence

This folder records an experimental collapsed transparent WPF pill, not a completed M1 shell. The default entry path remains the M0 scaffold.

`transparent-probe-run1` contains the externally observed first trial. `context.json` records launch conditions; `binaries.json` identifies the measured assemblies; `samples.csv` and `summary.json` come from scripts/measure.ps1; `probe.json` is written once during the app's natural exit. Both runs are complete: 301 samples over at least 300 seconds, with natural-exit diagnostics. Both failed working set; CPU passed.

The app creates a 220 × 36 DIP window with an 8 DIP top gap. It reads real GetSystemTimes CPU data every four seconds. The provider probe establishes a baseline; a first interval without counter progress has no numeric value. Windows performs placement and extended-style application through verified interop. On this host the smoke observation reported 120 DPI, 275 × 45 physical pixels, with top Y=10.

A visible style bit does not prove no occlusion. Sampled foreground comparisons do not prove an exhaustive focus audit. Style flags do not replace physical cross-process click-through or Alt+Tab checks. No mixed-DPI, fullscreen, tray, animation or expanded-panel acceptance is claimed.

Test-host execution required leaving the restricted process sandbox for normal desktop test-host execution. The context records actual administrator status; no UAC elevation or system-setting change is used. App hosts exit themselves. No process is killed, working set trimmed, garbage collection forced or machine power/affinity settings changed.
## Repeat and decision

Trial 1: mean 94.375 MB, sampled max 107.741 MB, normalized CPU 0.046874%.
Trial 2: mean 93.100 MB, sampled max 109.666 MB, normalized CPU 0.039061%.

Both used eight logical processors for normalization and 30 seconds warmup. Run 2 includes missing-native-API error handling and additional tests; visual rendering/cadence/dimensions are unchanged. Their assembly identities differ and are recorded separately. Raw summaries are preserved without substituting run 2 for the failure in run 1. The generic observer leaves architecture as TODO; each probe independently records actual X64 runtime architecture. The generic child-process note refers to M0; these probes also launch no child processes.

M1 stopped under the user's explicit rule. No native replacement has been implemented. See ../../M1_RENDERER_PROPOSAL.md. verification.json records the final 28 unit tests, 24 measurement checks and five startup cases. Fullscreen/hidden/expanded measurements and physical interaction acceptance remain absent, not passed.