# Warm start — validation in progress

31 October 2026. Application-session whole-process lifetime <=100 MB; fresh bar/popup and post-close current RAM <=90 MB. Existing CPU/hidden-work rules remain unchanged.

## What was built

Cold application/popups show a static shaped loading layout, without fabricated measurements or history. Overview displays preparing rows while device inventory runs. Bar items are initialized before their first reading and retain the last snapshot during rebuilds. Explicit CPU/network baseline states display loading marks or a recent real reading with an explanatory tooltip.

A shared session-only cache keeps at most one recent real sample per supported module. It expires after two minutes, does not extend age on a warming baseline, removes actual failed/invalid samples, discards processes/core details and network addresses, and caps copied adapter/disk/volume lists at 32. Cached values are labelled previous/refreshing until a real sample arrives; no cache is written to disk and no synthetic chart points or averages are inserted. No cache polling is added.

Completed background reads now present immediately through the existing WPF dispatcher instead of waiting for the next 1–5-second polling tick. Completion only presents; it never schedules another metric read. Configured cadence, one pending read, pause, closed-view guards and serialized source disposal remain enforced.

## Executed build/tests/previews

Release builds passed without warnings/errors, **125 tests each on actual x64 and x86 runtimes** (73 Core, 27 Shell, 25 Windows). Evidence TestResults/warm-start-{x64,x86}. New tests cover expiry/clock reversal/real-zero versus invalid, cache privacy and bounds, cold/previous/warming/failure rendering, no invented history, immediate first presentation and no extra reads. An initial syntax error in the new timing assertion failed the first build; it was corrected before the successful builds/tests.

Commands used the established offline SDK/package environment and `dotnet build TidletMonitor.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -p:OutputPath=bin/warm-start/`, then `dotnet test TidletMonitor.sln -c Release --no-build --no-restore -p:OutputPath=bin/warm-start/ --logger trx --results-directory TestResults/warm-start-x64`. Actual x86 repeated with `-p:PlatformTarget=x86 -p:OutputPath=bin/warm-start-x86/ -p:IntermediateOutputPath=obj/warm-start-x86/Release/`, results in TestResults/warm-start-x86 and `-- RunConfiguration.TargetPlatform=x86`.

Live CPU previews of two and eight seconds exited naturally, workers drained, recording two/two and eight/eight reads/frames. Their 100% exports were visually inspected; the first export at 1.18 seconds already contained real readings, so it is not claimed as a captured cold skeleton. Files remain ignored under artifacts/warm-start-preview-{2,8}. These are render exports, not desktop/DPI acceptance or resource trials.

## Resource gate

Navigation trial started with:

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/warm-start-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/warm-start/IsletMonitor.exe
```

Initial warm-start build full navigation accepted: 300.9933783 seconds/302 samples; observation max 84.090880 MB, lifetime/tail 88.707072 MB, machine CPU 0.2452815587%, 213 reads/213 frames. Activity/lifecycle/hash/single-capture checks passed.

Initial warm-start CPU popup accepted: 300.9913278 seconds/302 samples; observation max 86.720512 MB, full lifetime/tail 89.305088 MB, machine CPU 0.3147152551%, 345 reads/345 frames. The popup wrapper now observes its capture/shutdown tail, retains raw post-observation.csv and applies the common fresh-session 90 MB policy. Syntax check passed; both 100/150 exports and capture costs counted. Evidence docs/evidence/r4-dashboard/warm-start-popup-cpu-300s. Historical popup verdicts are not modified.

Final consistency correction marks successfully collected first GPU/Disk counter baselines as WarmingUp, matching CPU/network. It prevents recent values being replaced by empty baseline data. Final Release builds again passed zero warnings/errors and 125 tests each architecture, in TestResults/warm-start-final-{x64,x86}. Product executable bin/warm-start-final. Initial navigation/popup results above remain tied to their original bin/warm-start binaries; no hash is silently replaced.

Final GPU, Paused CPU and AfterClose CPU trials are running sequentially through the dashboard wrapper with the final executable, evidence docs/evidence/r4-dashboard/warm-final-{gpu,paused,after-close}-300s and status artifacts/warm-final-sequence.json. No pending pass is inferred. Normal Release stays frozen R3. No dependency, helper, Git, system setting, forced collection or trimming action.

Final GPU completed and was accepted: 300.9802375 seconds/302 samples, observation max 87.134208 MB, lifetime/tail 90.726400 MB, machine CPU 0.1868893468%, 344 reads/344 frames, full lifecycle/hash/capture/cleanup checks pass.

The first Paused attempt did not start: the script on disk still lacked that parameter despite the earlier settings report. The sequence stopped, preserving its rejected setup verdict. No budget failure or paused pass is claimed. The script was corrected, its actual ValidateSet inspected using Get-Command and its body/syntax verified before the fresh retry at warm-final-paused-retry-300s. Earlier syntax parsing alone had not verified parameter support; this corrects that reporting error. Retry status is artifacts/warm-final-retry-sequence.json; AfterClose remains queued behind acceptance.

Paused retry accepted: 300.9959679 seconds/302 samples, observation max 74.301440 MB, lifetime/tail 77.828096 MB, machine CPU 0.0350399212%. Application remained open; bar samples/reads, module reads and chart frames were all zero. Inspected the paused 100% export. Cleanup/hash/capture/activity gates passed.

AfterClose accepted: 300.9962402 seconds/302 samples, current observation max 84.250624 MB (90 MB limit), lifetime/tail 86.646784 MB (100 MB application-session limit), machine CPU 0.0434754185%. Module reads/frames stayed at their close-time baseline of 16/16. Natural exit, disposal, hash and policy checks passed. No pending work or cache timer was introduced.

Final hidden trial is running with the existing owned fullscreen fixture: `powershell -NoProfile -File scripts/measure-r2-hidden.ps1 -OutputDirectory docs/evidence/r4-dashboard/warm-final-hidden-300s -Executable src/IsletMonitor/bin/warm-start-final/IsletMonitor.exe -DurationSeconds 300 -ApplicationPage CPU -InteractiveFixture`. No hidden pass is inferred before completion. No input/system setting/process termination is performed.

Final hidden trial completed and was accepted: **300.9901097 seconds / 302 samples**, current observation/tail max **86.548480 MB** (90 MB limit), full application lifetime **90.091520 MB** (100 MB limit), machine CPU **0.0045423004%**, core equivalent **0.0363384033%** (below 0.1%). Actual hidden-window proof, 826 foreground checks/zero failures, unchanged binary hashes and full 420-second natural-exit tail were verified. HiddenMetricReads/ChartWork/ProcessWork were zero; application reads/frames stayed at eight/eight from baseline. Workers drained, timers stopped and tray removed. The owned fixture acquired foreground independently; no synthetic input was issued. This does not certify a physical game.

## Outcome and limits

Warm-start feature is implemented and its scoped checks pass. First launches use loading placeholders; only recent real session readings can fill a reopened view. Cache does not survive app exit and does not fabricate chart history. Final build/tests: 125 per architecture. Initial navigation/popup and final GPU/paused/post-close/hidden evidence retain their individual hashes and verdicts. Public numeric evidence remains alongside raw observations; popup probe/images containing running-application names are locally retained and git-ignored. User performs all Git operations.

Run the tested development executable from the project folder:

```powershell
.\src\IsletMonitor\bin\warm-start-final\IsletMonitor.exe
```

Normal Release remains the older R3 build; it was not overwritten. Broader light/system/high-contrast/icon/accessibility/translation polish remains unfinished. Physical mixed-DPI, Windows 10, real games, external screen readers and hosted CI remain unverified. No dependency, helper, telemetry, system setting, forced GC, working-set trimming or Git action.
