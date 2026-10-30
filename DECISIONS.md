# Tidlet Monitor — decisions

Date: 29 October 2026. The disclosed test-only dependency stack has been restored under the subsequent M0 implementation go-ahead; see the execution authorization below. No production package. “Chosen” below records a plan decision, not implemented behavior.

| ID | Decision/status | Reason and consequences |
|---|---|---|
| D001 | Chosen: plan first, M0–M3 only | AGENTS and a detailed plan precede feature code. Stop after M3; later phases stay deferred. |
| D002 | Required: .NET 8, WPF, Win32 | Honor the requested stack. Pin SDK rather than use installed default .NET 10. Support ends 10 Nov 2026; no autonomous retargeting. |
| D003 | Chosen: no production NuGet dependencies | WPF/built-in .NET JSON/drawing/async plus verified native interop. No chart/MVVM/DI/theme/tray/hardware package. Any new package still requires approval. |
| D004 | Chosen: one-way portable architecture | Core has no WPF/Windows APIs. IWidget/View belongs to Shell. App composes Shell and Windows via contracts. |
| D005 | Chosen: per-device/per-field capabilities | Startup support differs from runtime validity. Missing/denied/invalid/stale data has no numeric value. |
| D006 | Chosen: central demand scheduler | 1 s Expanded, 4 s Collapsed, visible-only Hover. Stop metrics/render work when hidden/fullscreen/off; bounded OS event reconciliation remains measurable. |
| D007 | Chosen: bounded HWND and finite animation | No screen-sized transparent window, perpetual render loop or forced memory trimming. M1 establishes feasibility before M2/M3. |
| D008 | Chosen: click-through activation policy | Visible-only 100 ms pointer observer transitions Collapsed to interactive Hover; tray/hotkey recovery. Benchmark it. Ordinary hover events cannot be assumed through a click-through HWND. |
| D009 | Chosen: expanded/settings accessibility | Island stays no-activate. Explicitly opened settings/keyboard detail/confirmation windows can activate normally. |
| D010 | Chosen: plain local JSON | Versioned bounded settings/theme/layout, atomic replacement, preserved corrupt/future-version file, last-known-good backup; no executable theme resources. |
| D011 | Chosen: profiles affect only Tidlet | No OS power-plan/registry/network/process changes. Meeting rules say when an app is selected, not that a meeting is detected. Automatic switching is off until configured. |
| D012 | Chosen: optional explicit ping target | Default local gateway where available; no hidden external endpoint. Blocked ICMP is not an outage diagnosis. |
| D013 | Chosen: heat availability is truthful | Rule/UI can be mock-tested before helper. Unsupported temperature disables heat alert; ACPI zone is not CPU temperature. |
| D014 | Chosen: network ambiguity is visible | Metadata/identity filtering and selected physical-uplink fallback; no virtual-name-only filters or physical+logical double counting. |
| D015 | Chosen: honest performance units | Conservative decimal MB sampled maxima, actual 300 s CPU delta, both normalized/core-equivalent CPU. Approx-zero target <=0.1% core-equivalent is an explicit engineering interpretation. |
| D016 | Required: mocks for actions | No system-setting commands, real process termination, elevation or fixer execution during testing. M2 confirmation path is injectable. |
| D017 | Deferred: helper and driver | Research confirms PawnIO transition; no LHM/PawnIO/WinRing0 dependency or executable helper before M4. |
| D018 | Temporary output location | Requested OneDrive workspace rejects all tested file-creation methods. Save planning artifacts in task output and report it. Cause unknown; no security-setting changes. |

## Disclosed test dependency stack — implemented with M0 go-ahead

Real .NET 8 `dotnet test` requires a test host/framework/discovery adapter. An empty solution returning success is not acceptable. Proposed **test-only** direct references:

| Package | Exact proposed version | Purpose | License/source |
|---|---:|---|---|
| Microsoft.NET.Test.Sdk | 18.11.0 | VSTest build/host integration for actual test execution | [MIT / official package](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/) |
| MSTest.TestFramework | 4.5.2 | Assertions, fixtures and data-driven tests | [MIT / official package](https://www.nuget.org/packages/MSTest.TestFramework/) |
| MSTest.TestAdapter | 4.5.2 | Discover/run tests via VSTest | [MIT / official package](https://www.nuget.org/packages/MSTest.TestAdapter/) |

Version/target/license listings were checked on 29 October 2026. The exact direct versions were subsequently restored and all 14 tests passed; the locked transitive graph and owner-supplied MIT license expressions were inspected.

Why this stack: one Microsoft-maintained test family, works with the documented .NET 8 VSTest path, no separate mocking library, no production runtime cost. Alternative xUnit/NUnit also adds framework/runner dependencies; a homegrown executable would not independently satisfy standard discovered `dotnet test` acceptance. Do not write fake passing test targets.

Transitives: test SDK pulls test host/coverage tooling; MSTest may bring analyzers, platform/object-model/report components and a telemetry extension. Review the actual exact graph and licenses, pin it in lock files and keep references private to test projects. Do not silently accept an unrelated/transitively expanding runtime dependency. No optional coverage collector, benchmark or GUI-automation package is proposed.

No application telemetry. For tooling, set `DOTNET_CLI_TELEMETRY_OPTOUT=1` and `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` only in build/test child-process environment and CI. [Official CLI opt-out](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry), [test platform opt-out](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-telemetry). Do not change persistent user/system settings.

**Authorization record:** the user subsequently instructed “implemet M0” after the concrete dependency proposal. The three disclosed direct versions were restored exactly and their resolved graph/license expressions inspected. See [DEPENDENCIES](docs/DEPENDENCIES.md). No new package family was added.

## Decisions deliberately left unverified

`TODO(verify)`: multi-group CPU fallback; exact GPU counter instance mapping; disk activity definition; Wi-Fi signal-only privacy-safe path; native battery IOCTL access/units; ACPI user-mode thermal contract; window/tray/hotkey/event binding signatures; GitHub action pins; latest SDK servicing assessment. The M0 graph and exact locally installed SDK pin are now verified; this is not a claim of newest servicing support. See API_REGISTER for sources and tests required before support claims.

## M0 execution authorization and location

29 October 2026: user instructed “implemet M0” immediately after the concrete test-dependency proposal. Proceeding with the disclosed Microsoft.NET.Test.Sdk 18.11.0 / MSTest.TestFramework 4.5.2 / MSTest.TestAdapter 4.5.2 test-only stack as part of that M0 go-ahead; no production package. This records the instruction and interpretation rather than claiming an explicit package-by-package yes. Reverify actual restore graph and license metadata before accepting it.

The original OneDrive workspace write recheck still failed with ENOENT. M0 is prepared in the permitted TidletMonitor-m0 output folder; the original requested folder remains untouched. Do not change system security settings. CI action dependencies are limited to checkout, setup-dotnet and upload-artifact required by the disclosed M0 CI plan; pin verified official tag commits and record sources.

## M0 implementation decisions

- Pin the locally verified SDK 8.0.414 exactly with rollForward=disable; CI installs that same SDK in its runner. No machine SDK install/upgrade. This is a reproducible developer scaffold, not a promise of current servicing/security support.
- M0 includes a plain WPF bootstrap, minimal portable capability/settings contracts and 14 real discovered tests. No sensor, island or settings-persistence feature was pulled forward.
- Measurement/testing scripts require PowerShell 7, already available on this host and Windows GitHub runners; it is developer tooling, not an app runtime dependency.
- Use ordinary as-invoked default WPF bootstrap window behavior; no platform DLL binding is introduced before its API is verified in M1. x86/x64 builds are compiled separately, while native-layout acceptance stays untested until interop exists.
- Microsoft test packages and CI actions remain test/build-only. Three direct test references and 10 transitive package families resolve under the lock file; all resolved nuspec license expressions inspected were MIT.
- Restricted sandbox canceled the first compiler task. Actual successful build/test/app measurements use the authorized execution channel; no security setting or OS configuration was changed.

## Latest user instruction: canonical folder and manual Git

The user now requires all active work in `C:\Users\bawan\OneDrive\Documents\Tidlet` and will perform commits manually every time. This overrides all earlier milestone auto-commit/init instructions. Do not stage, commit, amend, initialize Git or push without a specific new request. Earlier commits predate this instruction; no new Git mutation was made during transfer. The output repository is a transfer source only.

The requested transfer was attempted through ordinary copy and native Windows copy without directory metadata. Windows still rejected destination creation with access/file-not-found errors. No source files were verified in the destination. No system security settings changed. A fresh transfer ZIP contains the current working files and updated rules, without `.git`, caches or build output. Pause feature work until the canonical folder is usable.

## Project placement resolved

All 56 extracted project files were moved up from TidletMonitor-project-transfer into the canonical Tidlet folder. Resolved absolute source/destination paths were checked before moving; existing destinations were protected from overwrite; SHA-256 hashes verified every moved file. Only the empty extraction wrapper was removed. No Git history was copied, initialized, staged or committed. Earlier blocked-transfer entries above are historical. The agent changed no Windows security settings.


## M1 feasibility first

29 October 2026: follow docs/M1_FEASIBILITY.md before completing the shell. No dependencies. Require a natural-exit duration for the opt-in click-through probe until tray recovery exists. One bounded drawing surface, no blur/shadow/perpetual rendering. Real GetSystemTimes sampling, processor-group capability probe, no missing-to-zero conversion. Portable interfaces separate shell from Win32. Explicit diagnostics are local and bounded. Preserve M0 default until feasibility resolves. No alternate renderer or relaxed budget authorized.

## M1 WPF stop gate reached

Two complete 300-second collapsed feasibility measurements failed the unchanged 60 MB working-set limit: maxima 107.741184 MB and 109.666304 MB. CPU passed at 0.046874% and 0.039061% normalized across eight logical processors. Stop full M1 feature work. Preserve the opt-in tested probe and default M0 entry path. docs/M1_RENDERER_PROPOSAL.md is a concrete proposal requiring user direction; no native replacement or budget relaxation has been implemented. All tests pass, but M1 acceptance is incomplete. No new dependencies or Git mutations.

## Native renderer approved by user

User replied `approve` to docs/M1_RENDERER_PROPOSAL.md. Proceed under docs/M1_NATIVE_PLAN.md: native STA startup and layered-window rendering; keep C#/.NET 8, all M1 behavior and budgets. No new dependencies. Keep WPF legacy probe explicit and avoid initializing/loading WPF in native startup. Core owns portable presentation data/geometry; Shell coordinates state and demand; Windows owns bindings and native buffer/resource lifetime. AllowUnsafeBlocks is confined to the Windows DIB renderer with checked dimensions and scoped spans; this adds no package. Reuse native buffers rather than allocating managed frame arrays. Validate actual module loading and memory before completing full M1. No Git mutations.

Native gate passed: 30.740480 MB maximum and 0.00846315% normalized CPU over 300.0138101 seconds, no WPF assemblies. Proceed with the original M1 behavior on the approved native path. Read-only Windows reduced-motion/high-contrast preferences are honored; no system setting is written. Recovery uses native tray menus and Ctrl+Alt+I; conflicts degrade to tray with a reason. Fullscreen uses SHQuery plus a foreground/monitor heuristic, foreground/location WinEvents and a bounded one-second reconciliation; user-hidden windows stop those watches. All heavyweight/real-provider work and animation stop while suppressed. Native error and physical-hardware gaps must remain explicit.

## 30 October 2026 - Stats-inspired native dashboard

The user requested the supplied Stats screenshot's compact information organization with original colors. Use original Win32/GDI drawing (no copied Stats source/assets): charcoal/jade/amber and a warm light theme, eight module tabs, overview cards, round utilization gauges, dual history charts, aligned detail rows and combined summaries. Preserve island/notch/bar and no-activation behavior. Stats repository and MIT license reviewed: https://github.com/exelban/stats ; https://github.com/exelban/stats/blob/master/LICENSE . No new dependency is added.

The existing M2 NativeSystemDiskSource fabricated byte increments and the GPU source returned an unqueried generic adapter. Never use those values. Production disk now uses built-in PDH PhysicalDisk(_Total) read/write byte-rate counters with one background collection at a time, explicit warmup, status/range checks and generation reset across suppression. Keep the old cumulative-counter interface for mock tests only; its old native source now reports unsupported. GPU remains explicitly unavailable until a genuine adapter/counter provider is implemented. This redesign does not authorize the optional helper, sensors, Bluetooth, clock/calendar, fixes or other M4-M7 work.

Process rows show real accessible-process working sets, refreshed at most every four seconds while expanded. No termination action is added. The native renderer clears only the text-mask region being read, retains bounded fonts/histories, and scales the complete expanded canvas to fit small/high-DPI displays and the existing bitmap allocation ceiling. Theme switching in the dashboard is session-only; existing saved theme/accent preferences are read on normal launches. Diagnostics skip personal settings.

Measurement validation now rejects unexpected style/state history in pure collapsed and expanded scenarios. Existing mixed-interaction measurements must not be treated as idle proof. No budget is changed, no forced GC/trimming is used, and all owned test apps exit naturally.
## 30 October 2026 - User-requested reference colors, no green

The user replaced their earlier distinct-colors instruction: now match the supplied Stats reference's neutral dark-gray/white surfaces and blue/red accents, with slight adjustments and no green. Use dark background #414143, panels #4D4D50, light background #F4F4F5, panels #FFFFFF, blue #3B82F6 and secondary red #E64B58/#F05B67. Battery rings use blue. Update schema/host defaults and the native fallback; green-dominant saved custom accents render as blue without editing the user's settings files. Keep all layout, metrics and lifecycle code unchanged. No dependency or Windows API is added. Prior five-minute evidence remains historical to its original binary; do not present it as a new palette measurement.
## 30 October 2026 - Black notch, focus and media scope (approved; dependency pending)

The user explicitly approved working music controls and a focus timer in the screenshot-inspired notch. Dark surfaces will match sampled screenshot pixels: outer #0D0C0F and card #080809, with subtle neutral borders and existing blue/red accents. White mode stays white; no green returns. Use an independent compact Notch overview with access to the full monitor; do not add nonfunctional screenshot toolbar icons.

Focus: portable 25-minute deadline-based model, start/pause/resume/reset, with no dedicated polling while hidden. Media: official Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, introduced Windows 10 build 17763; only players participating in Windows system media sessions can be controlled. Gracefully report no player/unsupported/denied; no driver or elevation. Automated media commands use mocks.

Proposed dependency, NOT added: Microsoft.Windows.SDK.NET.Ref 10.0.17763.56 via the Windows-version-specific .NET 8 targeting framework. Exact version is declared by the installed pinned SDK 8.0.414 and verified on the official NuGet page as net8.0 compatible, no package dependencies. This targeting pack also supplies Windows SDK projection/runtime assemblies; payload and redistribution license must be inspected before final inclusion. The newest package targets net9.0, so it is not an acceptable silent upgrade. Alternatives: hand-written WinRT COM ABI (large maintenance/verification burden), generic simulated media keys (cannot reliably supply current title/timeline or acknowledge the intended player), or a separate PowerShell helper (avoidable process/memory overhead). Approval is required by AGENTS.md's no-new-dependency rule before any reference/download/restore is added. Sources: https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.17763.56 ; https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-enhance ; https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager .
Dependency approval received directly from the user: "Approve the Microsoft SDK package" (30 October 2026). Approves Microsoft.Windows.SDK.NET.Ref 10.0.17763.56 and its supplied projection/runtime files for the media API, not an upgrade to .NET 9 or the Windows App SDK. Pin the package through WindowsSdkPackageVersion and keep the app output directory stable for existing launch commands. Core and Shell remain free of WinRT APIs. License URL published by the package: https://aka.ms/WinSDKLicenseURL (binary document; inspect downloaded package license before release). Declared NuGet dependency graph: no dependencies; bundled runtime-file/license inventory is recorded after restore.
Implementation details for the approved media dependency (30 October 2026): restore and `--locked-mode` restore succeeded. The installed 8.0.414 SDK selects the pinned 10.0.17763.56 targeting pack for net8.0-windows10.0.17763.0. The package nuspec contains an empty .NETCoreApp8.0 dependency group (no additional NuGet packages). RuntimeList.xml/deps.json identify two copied runtime files: Microsoft.Windows.SDK.NET.dll (23,637,048 bytes, assembly 10.0.17763.38, file 10.0.17763.55) and WinRT.Runtime.dll (528,944 bytes, assembly 2.2.0.0, file 2.2.0.48161). WinRT.SourceGenerator.dll is build-only. No native driver, helper EXE or Windows App SDK is introduced. The package contains no embedded license file; its nuspec points to the Microsoft Windows SDK EULA. That official RTF was downloaded and its installation/use and distribution sections inspected locally in artifacts/sdk-license. Do not relabel the SDK package MIT. Tidlet's own code remains MIT. TODO(verify before any M7 redistribution): confirm the SDK runtime files' applicable redistribution notices/rights and include the required third-party terms in the release packaging; this work does not produce a release installer/zip.

Notch behavior: a 620x320 DIP home view is the default expanded Notch overview; Island/Bar retain their full monitor layout. The Monitor button opens the existing full overview. Module buttons open the matching detailed page; the full Notch header returns home. Switching to Notch from another style selects its home view. The native renderer applies #0D0C0F background / #080809 cards across dark views; white remains unchanged. CPU uses blue, memory uses red, and old saved green accents retain the earlier blue fallback. Only actual working navigation/control affordances are present. Hidden/fullscreen/collapsed/full-dashboard states start no media requests; one already-in-flight OS request may finish without UI callbacks. Session generations discard readings from before a hide/navigation transition. Disk/GPU/process providers are not sampled by the compact home because those readings are not visible there. Focus is a 25-minute, session-only monotonic deadline with pause/resume/reset, and no hidden ticking loop. Playback tests use injected mock backends exclusively.

Performance gate decision, 30 October 2026: the full SDK projection was measured, not assumed cheap. Expanded Notch passes 100 MB, but post-expansion collapsed working set exceeds 60 MB. Implementation stops; no renderer swap, helper or budget increase is made autonomously. docs/MEDIA_MEMORY_PROPOSAL.md describes a proposed demand-started music-only worker using the approved package, with aggregate process accounting and natural exit before collapsed measurement, plus a narrower native COM bridge alternative. Both require user review of this blown budget before implementation. This does not start M4 hardware-helper work.

30 October 2026: the user approved the recommended media isolation proposal by requesting "do next step". Implement one demand-started, non-elevated music-only worker using the existing executable and previously approved SDK package. Use inherited redirected anonymous standard streams instead of a globally named IPC endpoint, with bounded versioned JSON packets, deadlines, natural EOF/idle exit and visibility generations. Count all worker costs in measurement. No new dependency, administrator action, hardware-helper feature, budget relaxation or Git write is approved or required.

Protocol JSON now uses .NET 8's built-in System.Text.Json source generator for its four fixed packet/lifetime types. No new package/tool is added. A preliminary 20-second diagnostic had mixed expanded/collapsed states and a 108 MB numerical high point; it is not acceptance evidence. Source-generated metadata avoids building the new protocol's reflection graphs before the complete aggregate gates. Preserve this diagnostic in artifacts/media-worker/aggregate-diagnostic, repeat with a stable state and the standard warmup, and report any final breach rather than treating short/cold results as acceptance. Official source-generation guidance: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation?pivots=dotnet-8-0 (verified 30 October 2026).
