# Media memory gate: proposal for user review

Status: proposal only; no worker or renderer change has been implemented. The user approved the SDK dependency and music/focus scope, but the collapsed resource gate is now under review. Existing binary/source are left available for inspection, with no commits.

## Observed issue

The complete expanded Notch passed a 301.001-second trial: 71.04512 MB maximum working set, 63.80491 MB mean, 0.261496879% machine-normalized CPU (2.091975% of one core; eight logical processors). Both Microsoft.Windows.SDK.NET and WinRT.Runtime were loaded. The only state observed was Notch/Expanded, zero disk requests, no WPF assemblies, zero timers after natural exit. Raw evidence: docs/evidence/notch-control-center/expanded-notch.

The complete post-expansion collapsed trial failed: 300.003635 seconds, 68.837376 MB sampled maximum, 65.447167 MB mean, 0.085936459% machine-normalized CPU (0.687492% of one core). Memory is 8.837376 MB above the 60 MB limit; CPU passes. The lifetime peak of 79.413248 MB includes the earlier expanded style exercise. Native lifecycle diagnostics passed: final state Notch/Collapsed, all nine exercise frames captured, no WPF, both media assemblies loaded, zero timers after natural exit. The raw CSV recomputation also failed on working set. Evidence: docs/evidence/notch-control-center/post-expansion-notch. No forced collection or trimming was used.

The large media projection/runtime remain resident in the main process after opening music. Their retained cost is a plausible contributor, not a proven sole cause; native/managed caches may contribute too. The renderer is already native, and no WPF assembly was loaded. Changing the renderer again would therefore be premature.

## Recommended next step, requiring budget review approval

Keep this UI and its portable timer/media contracts. Move only Windows media-session access into a small, non-elevated, demand-started media worker using the already approved Microsoft SDK package. The main process should never initialize its WinRT projection. Start the worker when the compact media card is visible; close its local IPC connection and let it exit naturally on collapse, hide, fullscreen suppression or full-dashboard navigation. Retain the on-demand focus timer in the main app. On worker failure, disable music controls with a reason and keep the monitor alive. No startup task/service, driver, admin rights, network connection, hardware helper or M4-M7 feature is proposed. No additional package is proposed.

Measure aggregate CPU/working set for both processes while expanded, and require the worker to exit before the collapsed acceptance interval. Never hide its cost, terminate it during tests, force collection or trim either process. Test worker failure, slow startup, stale responses and natural exit; every automated playback command must still use a fake executor. Repeat the complete 300-second expanded, post-expansion collapsed and hidden measurements. The targets remain 100 MB expanded, 60 MB collapsed and <1% collapsed CPU. This proposal is not a claim that those targets will pass.

Alternative: replace the full SDK projection with a narrowly scoped native WinRT/COM bridge, after verifying every activation interface, generic async IID and vtable layout against official SDK metadata and testing both bitnesses. This avoids a second process but carries more interop/ABI work. It is not implemented. Raising the memory budget is not proposed.

A cold collapsed diagnostic and final trial data will be recorded separately, without claiming a short run satisfies the five-minute gate. Review this proposal before further implementation.

Additional diagnostic evidence: a cold collapsed run (31.012272 measured seconds, not acceptance) peaked at 40.787968 MB / 0.012595820% machine CPU with neither media assembly loaded. A cold hidden run (30.017261 seconds, not acceptance) peaked at 37.486592 MB with zero observed CPU delta and zero metric/pointer/render calls. Both exited naturally; no media commands were issued. The complete post-expansion failure, not the short cold baseline, controls the budget decision.

## Approved implementation plan - 30 October 2026

The user's "do next step" approves the recommended demand-started music worker. This is approval to implement the proposal, not to relax budgets or add packages. Keep M4-M7 deferred and Git manual.

1. Reuse the existing executable with a strict --media-worker entry before native/WPF startup. Communicate only through inherited redirected standard streams with bounded length-prefixed JSON. The worker handles only read/allowed transport requests; EOF, inactivity, protocol errors or an operation deadline return normally. No named service, network endpoint, process termination or admin action. Only the worker initializes WinRT.
2. Add a visibility-aware provider contract and mockable worker-session transport. Start at most one worker on visible media demand; close input on collapse/hide/fullscreen/navigation/exit, discard stale generations, wait for natural exit before any restart and bound retry frequency. Keep UI calls nonblocking and preserve focus timing. Failure disables controls with a plain reason. Record worker identities, final CPU/peak memory and natural exits in an optional diagnostic journal.
3. Test protocol bounds/EOF, stale responses, crash/failure, slow startup, repeated visibility and mock playback dispatch. Run real workers only for read/no-session and natural EOF/timeout paths; never issue real media commands in automated checks. Validate both bitnesses, final themes and fullscreen lifecycle. No new dependencies.
4. Extend measurement to aggregate parent plus journaled workers, retaining exited-worker CPU and conservatively accounting for worker peaks between samples. Verify raw aggregate/component samples and require zero worker processes during collapsed/hidden acceptance. Run sequential 300-second expanded, post-expansion collapsed and hidden trials, update PROGRESS/README/evidence and stop again if a budget fails.
