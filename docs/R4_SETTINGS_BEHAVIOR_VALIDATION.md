# Settings behavior — build/tests pass; resource validation blocked

31 October 2026. Application lifetime 100 MB; fresh bar/popup and post-close current RAM 90 MB. CPU <1% over 300 seconds. No budget change.

## What was built

- Monitoring pause keeps the application available with an explicit paused explanation, without starting a module provider. Pending reads drain and dispose before resume creates another provider. Environment suppression still closes application/popups. Paused bar clicks cannot open a polling popup.
- Settings changes from application, tray/context menu and import propagate to the open page. Module headers provide bar visibility and scoped settings; supported styles, update cadence, popup enable and order are editable. At least one bar item is retained.
- Shared portable transfer formatting applies bits/bytes to Network in the bar, compact popup and wide application page. Raw samples remain bytes/second. Disk transfer readings remain bytes. Invalid values are explained; Celsius/Fahrenheit still has no active temperature provider to format.

## Executed verification

Release build and **120 tests passed each on actual x64/x86 runtimes** (71 Core, 24 Shell, 25 Windows). Both builds had zero warnings/errors. Evidence: TestResults/r4-settings-behavior-{x64,x86}. New coverage includes paused startup, pending-read disposal/resume ordering, settings notification versus actual environment hiding, paused popup rejection, unit conversion/invalid values and redraw round trips. Native actions remain mocked.

Build/test commands use the existing offline SDK/package environment, `dotnet build TidletMonitor.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -p:OutputPath=bin/r4-settings-behavior/`, then `dotnet test TidletMonitor.sln -c Release --no-build --no-restore -p:OutputPath=bin/r4-settings-behavior/ --logger trx --results-directory TestResults/r4-settings-behavior-x64`. Actual x86 repeats with `-p:PlatformTarget=x86 -p:OutputPath=bin/r4-settings-behavior-x86/ -p:IntermediateOutputPath=obj/r4-settings-behavior-x86/Release/`, test results at TestResults/r4-settings-behavior-x86 and `-- RunConfiguration.TargetPlatform=x86`.

The observer adds a Paused scenario requiring the application to remain open, baseline open=true and zero bar/module reads and chart frames. It seeds only a dedicated local test settings file. PowerShell parser check passed. No full paused measurement has run.

Correction during subsequent warm-start validation: the script on disk did not actually contain the Paused parameter when the first full trial was attempted; that invocation failed before launching. The earlier parser check verified syntax, not feature presence. Paused support was then applied and its actual parameter metadata/body inspected before retry. See WARM_START_VALIDATION.md for the preserved setup rejection and subsequent resource results.

## Blocked resource trial

The automatic approval service did not execute the navigation command: initial review timed out; the permitted retry failed because its stream disconnected before completion. This is a review infrastructure failure, not an unsafe-action determination or product resource failure. No result is claimed, and the approval check was not bypassed. A sandbox test invocation also returned exit1 with no output and is not counted as a passing suite; the subsequent normal-user x86 test invocation above passed. No execution policy or security setting changed.

Next command (not an execution claim):

```powershell
powershell -NoProfile -File scripts/measure-r4-dashboard.ps1 -OutputDirectory docs/evidence/r4-dashboard/settings-navigation-300s -Page CPU -Scenario Navigation -CapturePercent 100 -Executable src/IsletMonitor/bin/r4-settings-behavior/IsletMonitor.exe
```

After accepted navigation, run a fresh Paused CPU scenario with the same executable, then relevant units/settings/after-close/hidden checks. Stop on any resource failure. New settings behavior has **no measured RAM/CPU acceptance yet**; preceding four-page resource passes certify bin/r4-module-checked only. Normal Release remains frozen R3. Theme/high contrast, icons, translation/accessibility polish and physical mixed-DPI/Win10/games/readers/hosted CI remain incomplete. No dependencies, helper, Git, system setting, forced GC or trimming action.
