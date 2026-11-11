# Tidlet Monitor — decisions

## Approved R2 correction and remaining monitor UI — 30 October 2026

User: "approve r2 correction and implemnt other all ui chnages also and application ui". Approves docs/R2_MEMORY_REVIEW.md correction and extends presentation scope to R3-R5 in docs/UI_COMPLETION_PLAN.md. Unchanged 75 MB bar/90 MB popup limits; first correct and validate R2, stop again if a budget fails. No dependency, helper, driver, system-setting or Git approval. Direct JsonDocument parsing and frozen/reused WPF resources use existing .NET APIs. No guaranteed savings claimed.

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

The direct cross-bitness worker fixture initially inherited the testhost's DOTNET_ROOT* bootstrap override and exited with official CoreHostLibLoadFailure 0x80008082. Fixture-only ProcessStartInfo.Environment overrides now remove those root keys so the selected apphost uses its registered installed runtime; normal production workers inherit their same-architecture parent unchanged. This does not edit this machine's user/system/process environment. The corrected read-only fixtures passed for both apphost bitnesses. Sources verified 30 October 2026: https://github.com/dotnet/runtime/blob/main/docs/design/features/host-error-codes.md ; https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables ; https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.environment .

Repeatable idle diagnostics: two 32-bit short native view checks and the initial aggregate trial were interrupted into collapsed state before completing the intended view. To keep idle trials reproducible on the user's active desktop, add --hold-diagnostic-input only when a probe report and bounded exit time are supplied. It suppresses pointer/hotkey/module state-changing input, records the number of held gestures, preserves actual pointer reads and all metric/render/fullscreen/power logic, and permits natural Close/Exit. Exercise drivers still run their scheduled states. Normal launches retain outside-click/keyboard/menu behavior. Measurement evidence must state that this is a held-input idle trial and still reject unexpected state/style/suppression history. This is a diagnostic repeatability change, not a memory/CPU-budget relaxation or evidence of interactive user navigation. Preserve rejected trials and report the distinction.

Architecture fixture builds now use separate IntermediateOutputPath=obj/<architecture>/Release/ as well as bin/<architecture>. Reusing default obj files across PlatformTarget overrides copied an x86 Core intermediate into an AnyCPU test output and caused assembly-load/discovery failures after the input-hold change. A normal Release Rebuild restores the default artifacts; isolated fixture builds and actual tests must pass afterward. CI is updated to keep those intermediates separate. Failed TRX files remain retained; they are not counted as passes. This changes build outputs only, with no new dependencies or system settings.

Final worker gate: expanded 112.418816 MB combined / 0.275147434% machine CPU failed; collapsed/style-exercise idle 56.659968 MB / 0.018169001% numerically passed with the documented early-cancellation qualification; hidden 37.908480 MB / 0.000651022% passed with no workers/metric/pointer/render work. Each measured at least 300 seconds. Raw aggregate/component replay and 27 binary comparisons passed. Implementation remains stopped for user review of MEDIA_BRIDGE_PROPOSAL.md; no native COM bridge, package change, budget increase or commit is made. The proposed alternative is not claimed to meet budgets before it is built/measured.

## 30 October 2026 - native media bridge and icon subset approved

The user explicitly approved MEDIA_BRIDGE_PROPOSAL.md ("Approve bridge, then redesign") and a subset of Lucide 1.62.0 assets ("Approve Lucide icon assets"). Implement the bridge first and retain the unchanged measured gates. No new runtime dependency is authorized or needed by the bridge. The existing SDK/worker prototype stays isolated for comparison, with normal launches using neither.

Lucide static vector subset: official release https://github.com/lucide-icons/lucide/releases/tag/1.62.0 verified 30 October 2026. Upstream complete license https://raw.githubusercontent.com/lucide-icons/lucide/1.62.0/LICENSE is ISC plus an MIT notice for listed Feather-derived icons. Preserve both. Purpose: consistent small outline navigation/metric icons. Alternative: original vectors or Unicode placeholders. Runtime/build transitive graph: none; vendor selected source vectors, not an npm/NuGet package or SVG engine. Record final file list, sizes and SHA256 before use. Approval does not authorize unrelated packages. Asset import follows the media measurement gate.

Bridge validation adjustment: the first post-expansion trial was rejected during warmup because repeated frame exports overwrote a BMP while another process held it. Export each diagnostic key once. Also hold the Notch exercise step until a real media read completes (bounded to 12 extra ticks) so an after-use collapsed trial cannot claim success after merely canceling initialization. These are diagnostic changes, not input restrictions on normal launches. Retain the rejected run under docs/evidence/native-bridge/post-expansion.

Approved icon subset inventory before import: docs/evidence/lucide-research/inventory.json records 18 exact verified Lucide 1.62.0 source URLs, byte lengths, SHA256 and element types. Selected names: layout-dashboard, cpu, memory-stick, hard-drive, network, battery, monitor, settings-2, app-window, x, chevron-right, arrow-down, arrow-up, audio-lines, timer, sun, moon, minus. No runtime or build transitive dependencies are added. Retain complete ISC/Feather MIT license. Native code consumes pre-flattened vector geometry; WPF may be used only by the offline conversion script already available with the Windows stack, never initialized in the ambient runtime.

## 30 October 2026 - Real hardware battery health, capacity and cycle count

The user requested real battery health reporting. Windows `GetSystemPowerStatus` only returns charge percentages and remaining time, but not design capacity or full charge capacity.
Verified Win32 API implementation on standard user rights:
1. Enumerate battery interfaces via `SetupDiGetClassDevs` with `GUID_DEVCLASS_BATTERY` (`72631e54-78a4-11d0-bcf7-00aa00b7b32a`).
2. Obtain battery tag via `IOCTL_BATTERY_QUERY_TAG` (`0x294040`).
3. Query `BATTERY_INFORMATION` via `IOCTL_BATTERY_QUERY_INFORMATION` (`0x294044`).
This provides exact hardware `DesignedCapacity` (mWh), `FullChargedCapacity` (mWh), and `CycleCount` without elevation, drivers, or external dependencies.
Battery health is computed as `Math.Clamp((double)FullChargedCapacity / DesignedCapacity * 100.0, 0, 100)`. Capacity readings are cached for 30 seconds to minimize I/O overhead.
Verified on this physical machine: DesignedCapacity 57488 mWh, FullChargedCapacity 42427 mWh, CycleCount 160 cycles, Battery Health 73.8%.
All unit tests pass; `ISystemBatterySource` default interface implementation ensures full backward-compatibility with fake test sources.

## R0 replacement brief, 30 October 2026

Latest brief retires the island/notch/native renderer and Lucide UI from the active build, returns to WPF and native Windows font icons, and tightens budgets to 50/90 MB. First perform a minimal live CPU/RAM WPF transparent and solid-fallback feasibility gate, with no AppBar reservation or system changes. Preserve all legacy sources and new battery-provider work. Retire removed UI tests explicitly; keep provider and portable logic tests active. Detailed order: docs/BAR_R0_R2_PLAN.md. No new dependency or Git operation.

## WPF retained after RAM clarification

The user clarified that application file size is not the concern, RAM usage is, and requested WPF Bar. Retain WPF and interpret this as approval for option 1 of R0_BUDGET_PROPOSAL.md: measure the already implemented solid fallback under the unchanged 50 MB RAM cap. No native renderer exception or budget increase is inferred. The solid trial uses identical binaries to the transparent trial, with only --solid changing AllowsTransparency/background alpha. No feature expansion until the gate is reviewed.

## Approved WPF-only memory experiment

The user explicitly selected 'Approve WPF memory experiment'. Implement docs/WPF_MEMORY_NEXT_EXPERIMENT.md only: bounded diagnostic --software-rendering with --solid, current-process RenderOptions preference set before Application construction, deferred context-menu creation, unchanged CPU/RAM cadence, no new dependencies/system settings/native renderer/budget relaxation. Keep normal rendering preference unchanged until measured. Record requested preference separately from actual-pipeline verification.

## WPF memory experiment result

Approved diagnostic software rendering plus deferred menu construction measured 84.627456 MB peak working set and 0.0356887884% machine CPU over 300.9961386 seconds. Memory still fails 50 MB, so R1/R2 remain stopped; software-only rendering is not made the default based on a failed gate. No budget relaxation is inferred. The lighter WPF visual host in docs/WPF_VISUAL_HOST_PROPOSAL.md is a proposal only and requires the next budget review.

## Approved lighter WPF visual host - 30 October 2026

User explicitly selected "Approve WPF visual-host experiment". Implement only the bounded feasibility comparison in docs/WPF_VISUAL_HOST_PROPOSAL.md: WPF HwndSource with drawing content, same providers/cadence, no new package or native/GDI renderer. Preserve Window comparison. Keep accessibility names and keyboard/context-menu paths; record untested interaction and DPI cases. Measure full 300 seconds under unchanged 50/90 MB gates before any R1/R2 expansion.

## Visual-host experiment result

The approved WPF visual host measured 76.337152 MB peak / 69.769985 MB mean working set and 0.0266924740% machine CPU over 300.002634 seconds. It still fails the 50 MB gate. Preserve both fixtures and diagnostic-only selection; no production-default host/architecture change or R1/R2 expansion. User review is required before the x86 comparison proposed in docs/WPF_X86_COMPARISON_PROPOSAL.md. No dependency, Git or Windows setting changes.

## Approved 32-bit WPF comparison - 30 October 2026

User explicitly selected "Approve 32-bit WPF comparison". Approval covers a bounded comparison of the same WPF visual fixture using a genuine x86 apphost, unchanged 50/90 MB limits and no default architecture or R1/R2 change. Existing SDK 8.0.414 contains Microsoft.NETCore.App.Host.win-x86 8.0.20. Its native runtime host source license is MIT (.NET Foundation and Contributors), verified from https://raw.githubusercontent.com/dotnet/runtime/v8.0.20/LICENSE.TXT on 30 October 2026. This is already installed SDK build infrastructure, not a new PackageReference or downloaded dependency. Application graph remains three project references with framework references; no new transitive runtime package is introduced. Alternative: keep the already measured x64 fixture, which failed 50 MB. Use official RuntimeIdentifier=win-x86 with PlatformTarget=x86 and SelfContained=false, local-only restore with downloads disabled, isolated intermediates/lock files. Verify PE machine and actual process architecture/pointer size; setting PlatformTarget alone is insufficient. Ask before any missing package/tool download. No Windows environment, registry or security setting changes.

## Genuine x86 experiment result and stop

Verified real PE32 apphost and in-process X86/four-byte pointers. Peak 60.882944 MB, mean 58.714908 MB, CPU 0.0403642171% over 300.0027221 seconds. RAM still fails 50 MB; budgets and defaults remain unchanged. No further speculative experiment or R1/R2 expansion. docs/WPF_RAM_BUDGET_DECISION.md proposes explicit user approval for a 75 MB bar target plus x86 WPF baseline, leaving popup 90 MB/CPU <1% unchanged. This is not approved. Runtime patch differences prevent architecture-only causality claims. No package or machine changes.

## Approved bar budget and baseline - 30 October 2026

User explicitly selected "Approve 75 MB bar + x86 WPF". Active bar working-set cap is now 75,000,000 bytes; popup remains 90,000,000 bytes; CPU remains <1% average over 300 seconds. Genuine x86 WPF HwndSource/DrawingVisual is authorized for R1-R2, with application-local software preference. No native/GDI renderer, new dependency, admin/driver or budget changes beyond this approval. Historical trials remain failures against their original 50 MB gate; do not relabel them. Measure completed bar before R2 expansion and stop on future resource failures. R3-R5 remain deferred. Normal launch will use the new host once implemented; old fixtures remain diagnostic comparisons.

## R2 verified data/layout decisions - 30 October 2026

CPU total/System/User/Idle derive from GetSystemTimes deltas (kernel includes idle). Optional reported GHz uses read-only CallNtPowerInformation ProcessorInformation=11 with PROCESSOR_POWER_INFORMATION; omit invalid/failed frequency. Per-logical-CPU PDH \\Processor(n)\\% Processor Time is probed, with two-sample warmup. Heterogeneous class mapping is optional: only label P/E after verified EfficiencyClass and correct bitness/group handling; otherwise label logical cores, with an unavailable explanation for hybrid detail. No temperature ring without an available sensor/helper.

RAM uses GetPerformanceInfo page counts times PageSize: physical used=PhysicalTotal-PhysicalAvailable, where available includes standby/free/zero; commit=CommitTotal/CommitLimit. Pressure score=max(100*(1-available/total),100*commit/limit); Normal below 80, Warning 80..<95, Critical >=95. If commit is unavailable, use physical score and explicitly label the reduced basis. Validate nonzero totals, available<=total, committed<=limit and finite ratios before rendering. This is a documented heuristic, not a Windows diagnostic classification. Two-part stack is Used/Available. SystemCache includes standby plus system working set and overlaps these categories; show it as a separate informational row, never add it to the stack. Modified/Compressed breakdowns are omitted because no verified independent source is implemented. EnumPageFilesW reports actual total/in-use pages if accessible; do not label commit as swap/page-file usage. No macOS Wired/App labels.

CPU/RAM histories allocate fixed 120-slot one-second rings only while a popup is open. Own 1/5/15-minute averages allocate bounded 900-slot history and indicate incomplete windows; reset on close/resume. No history/process query while closed. Sampling buffers reuse arrays; WPF presentation may allocate drawing/text objects and must be reported separately rather than claiming framework-wide zero allocations. Top-process samples use a visibility-scoped PDH Working Set/ID Process pair for 64-bit values even in x86, with OpenProcess/GetProcessTimes creation identity and monotonic deltas for CPU. Denied/exited/invalid rows are skipped. No terminate action. One query in flight at a time; stale generations are not displayed and disposal waits for the in-flight query without blocking the UI.

Popup kit: WPF HwndSource, 320 DIP, 13 DIP radius/subtle border, own geometric gauges and chart, Segoe UI Variable fallback, installed Segoe Fluent/MDL2 header glyphs; mirrored section dividers, rows and top-process list. Click anchors from item pixel bounds and clamps to its own monitor work area; bottom bar opens upward. Outside click via foreground change and local deactivation, Esc, repeated item click close. No new package/icons. Header launches Task Manager only on explicit user click; settings remains visibly deferred. CPU/RAM only; network/battery clicks explain that details are not yet built, without fabricating popups.

## R2 measurement-state instrumentation

First five-minute attempt was rejected despite its numerical Popup result: recorded history switched CPU/RAM seven times and closed early. Numerical window peak 84.770816 MB, lifetime peak 92.917760 MB; diagnostic render exports occurred on those closures. This is mixed-state exploratory evidence, not a certified CPU, RAM or after-close result, and it is not a reason to raise a budget silently. Retained at docs/evidence/r2-popups/cpu-300s with rejected-scenario.json.

Bounded --r1-probe launches that select a popup now hold that diagnostic view: ordinary metric-item clicks/keyboard/outside deactivation cannot alter it; explicit tray Exit still cancels the run, and the time limit always applies. Normal launches retain interaction. The diagnostic caption identifies the measurement. Exact open/close history, startup/lifetime peaks, held-input flag and worker cleanup are validated. After-use trials omit bitmap exports before observation so screenshot instrumentation does not inflate the post-popup memory path. Screenshots are generated in separate smoke runs or after open-popup observation ends. No renderer optimization, budget increase, dependency or system-setting change is inferred from this instrumentation fix.

## R2 startup peak and allocation review - implementation stopped

Held-open CPU numerical window passed at 88.498176 MB / 0.3593553745% CPU over 300.0163839 seconds; lifetime startup peak 91.062272 MB exceeds the unchanged popup cap. No startup exception or budget raise is inferred. R2 overall acceptance is blocked, remaining full RAM/after-close/hidden trials are pending, and no more feature work proceeds. Numeric rings are tested allocation-free; WPF presentation allocated 70,434,840 bytes cumulatively over 343 frames, so no allocation-free renderer claim. docs/R2_MEMORY_REVIEW.md is a proposed optimization/validation plan only, with no new dependency, settings or renderer change.

## R2 closed-bar resource gate and experimental cleanup — 30 October 2026

Corrected frozen CPU/RAM trials pass 90 MB including lifetime peaks (81.391616 / 82.391040 MB). Full after-CPU trial fails unchanged 75 MB bar gate at 77.950976 MB despite no chart/process work after close and 0.0298492404% average CPU. Stop feature expansion; no AfterRam/hidden/new UI trial follows. docs/R2_POST_POPUP_MEMORY_REVIEW.md is a proposal only. No budget increase inferred from the general UI approval.

User clarified the Network/Battery popup additions were experimental and requested removal/replacement matching the CPU/RAM kit. Removed only those experimental popup additions and related construction tests, retaining genuine bar providers. Ignored local backup preserves the retired experiment. Final cleanup builds/tests passed on both runtimes (94 each); resource results remain tied to the earlier frozen correction build, not silently transferred to a newer binary. No new dependencies, forced collection, trimming, machine/security changes or Git operations.

## Approved common 90 MB RAM limit — 30 October 2026

31 October user explicitly approved diagnostic-only memory profiling at unchanged 90 MB. Add bounded BCL own-process/GC snapshots at named phases, module basenames at a few checkpoints and a first-probe/sample decorator. No new package, helper, forced GC/trim, GC-policy changes, native resident-byte invention, Git or system settings. One full navigation attribution run; count recorder costs and report findings/correction proposal before changing production behavior. Profiling mode cannot mark application acceptance, even if a numerical window passes.

31 October single-scale result: 100% full navigation exported exactly one verified image and passed activity/lifecycle/hash checks, but lifetime/tail 91.525120 MB fails unchanged 90 MB; CPU 0.2239573382%. Peak was observed before capture, so capture-only change is insufficient. Full 150% did not start. Stop; docs/R4_MEMORY_ATTRIBUTION_PROPOSAL.md offers BCL-only diagnostic phase attribution before another correction. That next investigation is proposed, not implemented/approved. No Git, dependency, helper, forced GC/trim or system change.

31 October user requested continuation after the pending R4_CAPTURE_MEMORY_PROPOSAL.md review. Proceed with the named single-scale diagnostic correction, preserving 90 MB and counting capture/startup/tail. Add only diagnostic selection (100 or 150), bounded export metadata and separate sequential full navigation trials. Normal drawing, viewport, polling and cadence unchanged; old dual-scale diagnostics remain available for historical comparison. No new dependency/helper, forced GC/trim, Git or system change. No pass inferred before measurements.

31 October smaller-window outcome: static CPU/RAM/Dashboard lifetime peaks 87.199744/88.547328/76.832768 MB pass unchanged 90 MB. New lifecycle source passes 105 tests each architecture; short navigation/close checks pass. Full navigation observation passes at 83.898368 MB / 0.2897068881% CPU, but capture-period lifetime reaches 93.863936 MB, so stop. The original failure and dual-export costs remain counted. docs/R4_CAPTURE_MEMORY_PROPOSAL.md proposes separate single-scale diagnostic runs with complete lifetime accounting at 90 MB; not approved/implemented. No full after-close/hidden test after failure, no Git/system setting/dependency change.

31 October latest user decision: approve Option A in docs/R4_APPLICATION_NEXT_DECISION.md, 860x580 DIP initial application viewport, unchanged 90 MB. Only initial dimensions change from the opaque comparison; retain wide content/scrolling and other hosts. Do not infer the unselected 100 MB option is approved. Verify layout/tests and cold/lifetime resource costs; stop on failure. No new dependency, system setting or Git action.

31 October opaque result: 104 tests pass each architecture; actual target opacity false. Full CPU observation max 86.368256 MB, lifetime/tail 91.037696 MB and average CPU 0.3932329799%. Overall RAM failure against unchanged 90 MB. Stop; next decision is docs/R4_APPLICATION_NEXT_DECISION.md (smaller viewport or explicit application-only cap revision). Neither next option is approved. Historical failures remain unchanged.

31 October user approved the opaque WPF comparison in docs/R4_OPAQUE_WINDOW_PROPOSAL.md. Change only the large application host to UsesPerPixelOpacity=false with an opaque theme-color HwndTarget background. Keep bar/popups, x86/software preference and 90 MB unchanged. Optional corner changes are omitted from the initial comparison to keep the experiment focused; solid outer frame is an explicit visual gap. Record actual target opacity in probes. No dependency, registry/machine setting or Git action.

31 October wide-correction result: dedicated CPU/RAM pages built and tested, 103 tests each architecture; corrected CPU full trial fails RAM at 94.949376 MB lifetime (94.842880 MB observation), CPU 0.3496004998%. Stop before further pages/trials. docs/R4_OPAQUE_WINDOW_PROPOSAL.md proposes an opaque application-only WPF host at the same cap. Not approved or implemented. Preserve current failures and normal R3 Release.

31 October subsequent approval: user selected "Approve correction at 90 MB" for docs/R4_APPLICATION_MEMORY_REVIEW.md. Implement dedicated wide application-page presentation, bounded text/resource reuse, serialized provider disposal and held-input diagnostic trials. Original 94.892032 MB failure remains unchanged. Do not embed compact popups in application pages or relax budgets. No new dependency or Git operation.

31 October update: R3 four module trials pass the common limit. R4 whole-application lifetime peak 94.892032 MB fails; implementation stopped. Latest reference clarification requires full-width application module pages, with compact popups only from the top bar. docs/R4_APPLICATION_MEMORY_REVIEW.md records the result, mixed-navigation scenario rejection and a proposed bounded correction at unchanged limits. The correction is not yet approved. No new dependency or Git operation.

User explicitly selected "Approve a 90 MB bar limit; keep popup limit at 90 MB". Active bar and popup working-set limits are now 90,000,000 bytes, with average CPU <1%, approximately zero CPU hidden, no closed-popup chart/process work, no forced collection/trim and complete component accounting unchanged. Existing 77.950976 MB after-CPU run remains a historical failure against its original 75 MB policy; do not overwrite it as a pass. Future runs record the approved 90 MB policy. Continue pending AfterRam/hidden validation, then the already authorized R3-R5 UI, using the CPU/RAM kit and replacing removed test-only popups. No additional dependency, admin, driver, Git or machine-setting permission.

## R4 diagnostic memory attribution result — 31 October 2026

The approved one-run investigation is complete: 108 tests pass each architecture; full instrumented navigation observation 91.099136 MB, whole lifetime 97.660928 MB, average CPU 0.2524129159%. Costs include recorder overhead; this cannot certify the product or establish a regression against a different uninstrumented run. First cap crossing predates export; overlapping drawing/provider/inventory phases prevent exact causal attribution. Preserve all historical evidence and stop production changes.

See docs/R4_MEMORY_ATTRIBUTION_FINDINGS.md for phase data, semantics and the concrete proposed two-run provider-only diagnostic comparison of CPU process counters. That next experiment is not approved or implemented. No budget/dependency/Git/system-setting permission inferred.

## Approved provider-only attribution comparison — 31 October 2026

User selected "Approve provider comparison" for docs/R4_MEMORY_ATTRIBUTION_FINDINGS.md. Execute two fresh sequential 30-second CPU source diagnostics with and without wildcard process counters, retaining aggregate/per-core readings and identical cadence. Diagnostic dispatch bypasses WPF/application inventory; normal source construction still enables process counters. Record query/counter/probe/read/disposal phases and external own-process observation/tail/hashes. Both variants remain attribution-only, never product acceptance; report both results before production changes. No dependency, helper, budget increase, forced collection/trim, machine setting or Git action authorized.

Provider comparison completed: with/without process counters lifetime 37.736448/34.156544 MB; short-window CPU 0.434754/0.289860%. Actual x86, no WPF loaded, both natural exits, 109 tests passed each architecture. This single ordered pair supports a modest process-sampling cost but not full-application acceptance, exact native-byte attribution or a leak. Per-core-only initialization still spikes. See docs/R4_PROVIDER_COMPARISON_RESULTS.md. Proposed visibility-driven core/process sampling is not yet approved or implemented; preserve all features and 90 MB. Stop before production changes. No Git/dependency/system change.

31 October user approved the visibility correction in docs/R4_PROVIDER_COMPARISON_RESULTS.md. Add portable detail-interest flags, viewport geometry and stable warming sections; serialize query reconfiguration with reads/disposal. Preserve aggregate metrics, all popup defaults/features, x86/software/opaque WPF and 90 MB. Build/test and full uninstrumented navigation, stop on failure. No dependency/helper/Git/system setting/GC or trim change. Initial viewport test expected core bars offscreen at the page bottom, but they actually intersected the 480-DIP viewport; retain the failed result and correct the test to explicitly control core count and viewport geometry rather than altering valid intersection behavior.

Visibility correction result: 112 tests pass each architecture; full unprofiled navigation observation max 86.347776 MB, whole lifetime 91.734016 MB and CPU 0.1269504373%. Request evidence: 91 visible-core requests, zero offscreen process requests; full lifecycle/hash/capture pass. Lifetime fails unchanged 90 MB and stops work. Separate run differences do not prove exact savings. No further trials or normal Release replacement. Next viewport-drawing and invalid-core-fill guard correction in docs/R4_VISIBLE_RENDERING_PROPOSAL.md is proposed only; no new approval inferred. No dependency, system setting or Git action.

31 October user approved docs/R4_VISIBLE_RENDERING_PROPOSAL.md. Cull offscreen text/geometry construction in wide CPU/RAM pages while preserving full layout/scroll access, viewport detail interest, warming/capability states and popup defaults. Restore finite/range validation for core fill and cover missing/invalid values plus scroll round-trip rendering. Keep x86/software/opaque860x580 and 90 MB. Build/test both architectures and one full unprofiled navigation trial with lifetime costs; stop on failure. No package/helper/Git/system-setting/GC/trim permission.

Viewport drawing outcome: 113 tests pass each architecture and CPU/RAM live exports inspected. Full unprofiled navigation observation max 86.708224 MB / lifetime 93.814784 MB / machine CPU 0.0940920826%. Lifecycle/activity/hash/capture/cleanup checks pass; whole-lifetime 90 MB fails and work stops. No further trials, normal Release replacement or feature completion. docs/R4_WPF_BUDGET_DECISION.md proposes an explicit application-use-session 100 MB policy with 90 MB fresh bar/popup and post-close current observation; no budget/gate change is implemented or approved. Earlier failures remain intact. No dependency/Git/machine-setting/GC/trim action.

31 October explicit user decision: "Approve application 100 MB" in docs/R4_WPF_BUDGET_DECISION.md. New application-use sessions have 100,000,000-byte whole-process lifetime limit; fresh bar/popup sessions retain 90,000,000 bytes. After closing the application window current observed RAM must be <=90 MB; the retained prior application peak is still reported against 100 MB. CPU/hidden-work/full accounting rules unchanged. Add an explicit Application scenario and tested session policy, including tail checks. Preserve every earlier failed verdict, count all application/auxiliary costs, no peak reset/GC/trim. Sequential navigation, static CPU/RAM/overview, after-close/hidden validation before page extension; stop on future failure. No dependency/helper/Git/system setting or normal Release replacement authorized by the budget change.

New application-policy validation complete: six full baseline scenarios pass with current/lifetime limits explicitly recorded. Highest reported lifetime 91.389952 MB in navigation; hidden machine CPU 0.0032445732%, zero hidden work, after-close current max 82.112512 MB. Historical 90 MB failures and interrupted/foreground-assertion attempts remain unchanged. See docs/R4_APP100_VALIDATION.md. Proceed with previously authorized remaining monitor UI, preserving 100 MB application-use lifetime and 90 MB fresh bar/popup/post-close current, <1% CPU and stop-on-failure. No dependency/helper/Git/system change or blanket physical/CI certification.

31 October remaining wide pages: reuse existing typed Tier 1 sources and WPF visual host for GPU/Disk/Network/Battery application cards; preserve bar-only compact popups. GPU dedicated memory is capacity, physical disk/volume relationships are not inferred, network traffic does not imply Internet reachability, and missing battery details are omitted/explained. Lazy per-device buffers bounded to 32; impossible transfer rates rejected before history insertion. Final 115 tests pass each architecture; four short live previews inspected. GPU full application trial accepted at 91.451392 MB lifetime / 0.1842963271% CPU under approved 100 MB application policy; remaining trials pending. No dependency, helper, Git or system setting change. See docs/R4_MODULE_PAGES_VALIDATION.md.

31 October settings behavior: pause is distinct from environmental suppression; preserve application access, close/reject polling popups and serialize pending source cleanup before resume. Publish settings changes for open application refresh. Module header controls expose scoped settings and bar visibility. Portable transfer formatting uses binary byte units and decimal bit units; only Network follows the bits preference, raw samples remain unchanged. 120 tests pass each architecture. Full resource trial blocked before execution by automatic-review timeout then disconnection; no new resource pass inferred. See docs/R4_SETTINGS_BEHAVIOR_VALIDATION.md. Normal Release stays R3; no dependency/helper/Git/system-setting/GC/trim action.

31 October user requested immediate recent/dummy content followed by quiet live refresh. Use static skeletons on cold open and clearly marked recent real samples on reopen; never present fabricated diagnostics or chart history. Cache is session-only/two minutes/one sample per module, strips process/core/network address details and bounds hardware arrays. Present task completion on the existing dispatcher without rescheduling a poll; retain pause, visibility, cadence and disposal rules. 125 tests pass each architecture; live short previews inspected, full resource gate pending. See docs/WARM_START_VALIDATION.md. No dependency, helper, disk telemetry/cache, Git or machine setting change.

Warm-start scoped validation completed: final125 tests pass each architecture, all executed navigation/popup/GPU/paused/post-close/owned-hidden trials accepted at their explicit limits. Final hidden CPU0.0045423004% machine with zero hidden work, 826 foreground checks/zero failures; no synthetic input or real-game claim. Two-minute cache remains in-memory only; first launches show placeholders and reopened views show marked recent real readings. Final executable bin/warm-start-final; normal Release not overwritten. Earlier build-specific verdicts/setup rejection retained. See docs/WARM_START_VALIDATION.md. Broader R4/R5 polish/hardware/CI gaps remain; no Git/dependency/system setting action.

User's small UI instruction supersedes earlier no-green preference for battery icons: use existing green fill across bar/popup/application, keep low-battery warning colors, default Right alignment and save Right in current app preferences. Reuse two frozen brushes in the ambient bar to avoid loading theme parsing for that icon. App-only build and eight-second visual preview passed; full test/performance reruns explicitly skipped as requested. No new dependency/Git/system setting; app setting update only. Details docs/BAR_VISUAL_TWEAK_PLAN.md.

User requested closely paired red/upload and blue/download dots and speeds. Position a shared dot column just before the wider right-aligned speed label, retain fixed item width, and keep dimmed red/blue idle semantics. App-only Release build and six-second visual smoke passed; full suites/performance intentionally skipped per user instruction. No dependencies, settings or Git operation. See docs/NETWORK_BAR_SPACING_PLAN.md.

User requested undo of the immediately preceding traffic-dot spacing/idle-color change. Restored the exact previous drawing logic, retained green battery/right alignment and marked the spacing plan reverted. App-only Release rebuild passed without warnings/errors. No full tests/performance repeated, no Git action; historical evidence preserved.

Clarified user instruction: keep dots stationary, move upload/download values closer and leave more space before Battery. Shift speed text right edge112->84 DIP only; keep dots x3/y6,18 and fixed item slots. Red/upload and blue/download remain recognizable with matching dim idle colors. App-only build and six-second visual smoke passed; full suites/performance intentionally skipped. Earlier dot-moving change stays reverted. No dependency/Git/settings change. Details docs/NETWORK_SPEED_POSITION_PLAN.md.

Live GPU bar request: reuse existing verified GPU Engine/DXGI provider with one lazy timer-free worker, max valid adapter usage rather than sum, per-adapter details retained. GPU item membership controls source lifecycle; saved settings changes update the sampling closure, resolving the placeholder/non-live behavior. Enabled GPU in user's existing app layout. Three sampler tests plus one hit-area/action/save toggle test pass x64/x86; fresh bar full resource result76.439552 MB lifetime/0.0434748009% CPU passes. No packages/native API/helper/admin/outbound/Git/system change. Details docs/GPU_BAR_VALIDATION.md; full-suite and new hidden/co-open tests not claimed.

Application switches now permit every item, including the last, to be hidden. Portable WithItemVisibility provides idempotent changes for header/settings/context actions; empty layouts remain valid persisted settings. Hide empty native surfaces and avoid Reserve registration while retaining tray ownership. Environmental suppression is distinct from an intentionally empty layout so the application remains accessible; no visible bar means no bar sampling. Release build and four focused tests in each architecture configuration passed, plus an eight-second live empty-bar/open-application preview. No full-suite/resource repeat or Reserve hardware acceptance claim. See docs/MODULE_TOGGLE_PLAN.md. No dependency/API/Git/system-setting change; current user preferences preserved.

Toggle mouse-input correction: preserve the existing native drag hook but return HTCLIENT where WPF hits a SurfaceAction, so header switches/settings receive client mouse input. Earlier automation Invoke coverage did not cover this native routing defect. Regression now checks the production hook decision at three scales/negative origins and uses WPF mouse-up to switch all seven modules both ways. Focused tests passed x64/x86 and normal Release x86, no new package/API binding or resource trial. Initial normal build was locked by the user's old instance; rebuilt successfully after the user closed it. No automatic termination/Git/system action. See docs/MODULE_TOGGLE_PLAN.md.

Compact toggle-order clarification: persist optional validated ItemOrder independently of enabled Items. Capture legacy visible order on the next visibility change and append unconfigured modules deterministically; compact layout still uses enabled Items, so gaps close. Re-enabling filters remembered order rather than appending at the right corner. Explicit Move earlier updates both sequences and Restore defaults clears order metadata. Ten focused tests pass per x64/x86 configuration; isolated Release build zero warnings/errors. No dependencies/APIs/system setting/Git operations, no full performance repeat. Existing order before this correction cannot be reconstructed if earlier toggles already reordered it; current saved order is the migration baseline. See docs/BAR_TOGGLE_ORDER_PLAN.md.

31 October optional tools scope: latest user explicitly permits five isolated modules, including formerly excluded Git/AI utility scope. Subsequent placement instruction moves tools to LEFT, monitors retain right alignment. All tools default off on existing settings (Git/AI always off); no implicit clipboard/log/network access. Core settings/contracts/policies separate from Shell plugin views and Windows boundaries. Use existing installed Segoe glyphs/approved SDK, no package. Shared frozen theme brushes reduce repeated drawing-resource construction. Development slice has a working tested Manual provider, experimental read-only Git backend, and explicit pending capabilities for three native backends; no claim of full delivery. Native sensitive effects/input/capture are not exercised. Validate whole-lifetime90MB tool popup before extension, preserve100MB application policy and stop on failure. Details docs/TOOLS_IMPLEMENTATION_PLAN.md and docs/TOOLS_API_REGISTER.md; manual Git unchanged.

10 November user cancelled optional tools and returned Tidlet to system monitoring. Removed the tool implementations and integration rather than retaining dormant modules. Old JSON tool properties are safely ignored; preserve monitor settings/order and old performance evidence. No dependency changes, Git mutation, machine settings or cleanup of user data. See docs/MONITOR_ONLY_CLEANUP.md.

Popup appearance clarification: use real WPF per-pixel translucency from frozen background brushes, not whole-window opacity. Draw the reference-inspired gradients/edges and retain opaque text and data colors. Keep it in the existing system-monitor popup path, no desktop sampling or dependency. Four focused x86 tests and alpha-verified CPU/RAM previews pass; 90 MB final gate remains in force. Details docs/POPUP_GLASS_REFRESH.md.

Bottom placement request: use a separate compact overlay above the Windows taskbar rather than embedding in Explorer. Keep the native taskbar and existing Top/Bottom settings. Read current monitor work area; keep existing AppBar reservation negotiation for user-selected Reserve. CPU history and GPU activity reuse bar samples, with no extra poller. Verify native upward popup geometry and combined 90 MB resource gate. See docs/BOTTOM_MONITOR_BAR.md.

Combined bottom+CPU popup trial completed: 92.348416 MB lifetime (90 MB cap), 89.661440 MB observed and 0.320581% machine CPU. Placement and cleanup pass, resource gate fails. No normal Release replacement or cap change; stop and seek direction on docs/TASKBAR_GLASS_MEMORY_REVIEW.md.

## Top-edge hover placement — 10 November 2026

Make the drop-down strip an opt-in Overlay placement alongside the saved top and bottom choices. Reuse the native cursor query and WPF animation, and suspend bar sampling when all strips retract. The x86 live-GPU and CPU-popup trial reached 91.725824 MB lifetime against 90 MB, so normal Release remains held. See docs/TOP_EDGE_REVEAL.md for verification and the proposed same-cap comparison.

## Waveform application icon — 10 November 2026

Use the supplied artwork as a multi-size Windows ICO embedded through MSBuild ApplicationIcon. The tray extracts the small icon from the apphost, retains the shared Windows fallback, and releases the extracted handle after registration. No package is required. Verified 10 November 2026 against [ApplicationIcon](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties), [ExtractIconExW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-extracticonexw), and [Windows notification area icon ownership](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar#adding-and-deleting-taskbar-icons-in-the-notification-area).

## Dashboard glass styling — 10 November 2026

Use a frozen WPF backdrop and shared card brushes to reproduce the reference’s colors while keeping the opaque application window approved in R4. The device illustration is drawn in WPF and the inventory still comes from the machine. See docs/DASHBOARD_GLASS.md for verification.

## Charcoal monitor popups — 10 November 2026

The latest popup visual reference supersedes the blue glass palette for monitor popups. Use opaque neutral surfaces, frozen solid chart brushes, and a transparent exterior for rounded corners. Keep the popup host and metric pipeline. See docs/POPUP_CHARCOAL.md.

## Charcoal Dashboard — 10 November 2026

Use solid, frozen charcoal brushes and a blue active sidebar row for the full application window. The reference replaces the prior navy glass styling and leaves the monitor layout and providers intact. See docs/DASHBOARD_CHARCOAL.md.

## Charcoal monitor strip — 11 November 2026

Reuse the existing bar drawing and sampling for all placements. Keep up to 20 valid GPU readings, draw CPU utilization and RAM fill from the existing snapshot, and choose compact text on narrow displays. The live-GPU bottom bar passed its unchanged resource gate. See docs/CHARCOAL_MONITOR_STRIP.md.

## Light mode — 11 November 2026

Apply the saved Light preference across WPF surfaces using lazy frozen palettes and the existing settings refresh path. Keep System on its existing dark fallback. The mixed resource run exceeded 90 MB and was invalid for bar-only acceptance, so the build remains isolated. See docs/LIGHT_MODE.md.

## Desktop settings autosave — 11 November 2026

Keep immediate saves through the existing application JSON writer. Make success and failure visible in Dashboard Settings and verify a real file reload with the saved layout. Dark strip styling is shared across placements in the current preview.

## Windows power mode — 11 November 2026

Use the documented AC/DC Windows power-mode APIs from the Battery page. Recheck the power source before applying a user choice and reread the configured mode after the call. Fall back to the Windows Settings URI on unavailable systems. Diagnostic runs are read-only and verification uses a fake for all actions. See docs/POWER_MODE_CONTROL.md.

## Monitor UI audit — 11 November 2026

Keep the existing visual host and reduce work at viewport and provider-lifetime boundaries. Treat popup and Dashboard navigation during bar-only diagnostics as out of scenario. Preserve resource budgets and earlier verdicts. See docs/UI_AUDIT.md for the fixes, tests and repeatable render matrix.

## Battery optimization — 11 November 2026

Guide the user to the manufacturer's documented 80% charge control. Use the existing Dashboard device model as a hint and let the manufacturer utility confirm support and the active limit. Keep the guide free of polling and system actions. See docs/BATTERY_OPTIMIZATION.md.

## Startup reading states — 11 November 2026

Keep startup feedback in the existing WPF surfaces and session cache. Use clear measuring and paused labels, retain real readings during refresh, and draw a chart after it has enough samples. Preserve the sampling intervals. See docs/STARTUP_READINGS.md.

## First-reading details — 11 November 2026

Preserve known disk capacity, graphics adapter data, and network interfaces when a provider is still calculating rates. Mark pending values as Measuring, and omit empty charts after Windows reports unavailable counters.
## DPI-aware text in the monitor UI

The WPF dashboard, monitor strip, and popups share device-scale text formatting and pixel-aligned text origins. Gauge readouts use the measured text width and height to stay centered as the reading changes. Logical processor values use fixed-width, right-aligned columns so status messages fit at the default window size. No dependency was added.

Microsoft documents the DPI-aware `FormattedText` constructor and its `TextFormattingMode` parameter. [WPF FormattedText constructors](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.formattedtext.-ctor)

## Settings controls and in-app widgets

Settings choices open a keyboard-operable list with the current option marked. Selection applies through the existing validated settings store. The page reports the result and keeps related controls together in individual module pages. Opacity uses 10% steps from 20% to 100%, while imported values remain valid at their original precision.

System theme reads the documented Windows app preference when Tidlet applies its theme. The read uses the current user's `AppsUseLightTheme` value and falls back to light if the value is missing or inaccessible. Tidlet does not write to Windows personalization. [Microsoft Windows settings reference](https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-common#personalization---colors)

The Widgets page owns one sampling loop with a three-second interval and module-specific sources. It requests no per-core or process details, shares the recent-reading cache, and releases sources on navigation, pause, or close. The combined card reuses the same samples. GPU shows the busiest reported adapter, disk shows the first valid fixed volume, and the Network card shows a local address only when one adapter is present.

Desktop pins use separate non-topmost WPF tool windows and the existing card renderer. Pins share one sampling loop for the requested metrics, pause when monitoring pauses or the bar reports environmental suppression, and save only widget names and screen positions in `desktop-widgets.json`. Clicking **+** adds a card or reports that it is already pinned. The title uses Windows caption hit testing for movement, and Tidlet saves the coordinates at the end of the move. [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles), [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest), [WM_EXITSIZEMOVE](https://learn.microsoft.com/en-us/windows/desktop/winmsg/wm-exitsizemove), [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)

## Data usage totals

The usage ledger records each accepted byte delta from the shared physical adapter sampler once, even if several surfaces read it. It stores only dates, totals by transfer direction, and the selected period. A delta crossing midnight is credited to the date of the later sample. Calendar weeks start Monday. Startup baselines, counter resets, and time when monitoring is stopped do not contribute. The app checkpoints locally about once a minute when values change and on clean exit.

## In-app temp cleanup

The scan reads accessible files in the local user's Temp folder, skips reparse points, and is bounded to 5,000 files and 16 levels. Only top-level, single-link `.tmp` and `.temp` files last modified at least seven days ago enter the removal plan. Tidlet requires a separate confirmation. The executor opens each candidate exclusively, checks the resolved path, file identity, metadata and age again, and deletes by handle. The estimated reclaimable size and removed byte count use logical file sizes. The measured free-space change is separate because allocation and other disk activity can affect it. [Windows file disposition API](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle) · [Windows reparse point handling](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-operations)


## Gaming Mode FPS feasibility

The user approved evaluating PresentMon under its free-use license. PresentMon 2.6.0 upstream source uses the MIT license with Intel's copyright and permission notices retained on redistribution. Evaluated only the official standalone x64 console artifact. Its SHA-256 matched the published digest b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af. An out-of-process console would keep the current x86 WPF host and avoid loading a capture SDK into Tidlet. The MSI and service were not installed. TODO(verify) Review the standalone release's transitive notices and runtime requirements before any redistribution. [License](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/LICENSE.txt) · [Release assets](https://github.com/GameTechDev/PresentMon/releases/expanded_assets/v2.6.0)

The standalone console ran with the normal user token but could not start its ETW session. Keep FPS unavailable for this account under the existing no-admin and no-system-setting-change requirements. Gaming Mode remains unimplemented. [PresentMon permissions](https://github.com/GameTechDev/PresentMon/tree/v2.6.0#permissions)

## Gaming Mode implementation plan

Keep the existing strip host, theme and placement. A session-only tray/strip switch replaces the displayed items with FPS and frame time, GPU, CPU, RAM and supported battery status while preserving normal settings and styles. When the strip is visible, sample the foreground application and use the separately supplied, hash-verified PresentMon 2.6.0 x64 console to read display-frame intervals. Stop capture on pause, hide, target change or exit. Show permission, availability and warm-up states. Verify the parser with fixtures, build and run the x86 tests and measure the Windows access-denied path separately from live capture. Live capture stays unverified until Windows permits the trace under the current rules.
