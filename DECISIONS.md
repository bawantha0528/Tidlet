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

