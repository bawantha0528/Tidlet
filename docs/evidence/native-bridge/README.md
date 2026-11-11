# Native media bridge evidence - 30 October 2026

The user approved MEDIA_BRIDGE_PROPOSAL.md and the Lucide subset. Normal launches now use raw Windows media interfaces on a demand-started MTA thread. The old SDK worker remains an explicitly invoked comparison fixture only. There is no driver, elevation, new runtime package or system setting change.

| Trial | Seconds / samples | Peak working set, decimal MB | Mean process CPU / eight logical processors |
|---|---:|---:|---:|
| Expanded Notch | 300.0001653 / 301 | 57.012224 | 0.2337238296% |
| Collapsed after completed media read and style exercise | 300.9943934 / 302 | 55.873536 | 0.0168711614% |
| Explicitly hidden | 300.9983209 / 302 | 38.400000 | 0% (0 recorded CPU seconds) |

Each trial has binaries.json, context.json, raw samples.csv, summary.json, probe.json, empty workers.csv/journal, render buffer BMP when visible, and scenario-check.json. Expanded uses the first bridge binary; the later two use a diagnostic-only revision that exports each frame once and waits for completed media initialization in the exercise. Islet.Windows.dll and Core binaries match across all three. App/Shell hashes differ only for those diagnostic changes. These are baseline bridge results, not acceptance for the later desktop redesign.

Native checks: x64 ambient process, no WPF/media projection assemblies; zero child workers; media thread stopped on close; zero native timers at exit; no focus steal; real NoSession status, no playback commands. The post-expansion run completed a real read before closing, unlike the earlier worker exercise. Explicitly hidden had zero sensor attempts, pointer checks, render calls and media thread starts. Raw replay validates numeric results and state/lifecycle evidence. No collection, working-set trimming, profiler or system configuration changes.

The post-expansion and hidden trials overlap as independent diagnostic instances, each measured with its own reporter. They share the normal interactive Windows desktop with other applications; this is not an isolated lab. Windows build 26200, .NET 8.0.20, SDK 8.0.414, eight logical processors/FF affinity, one physical display at DPI 120, normal non-admin token. No child/auxiliary cost of an instance is excluded.

Commands actually run: Release solution build, isolated x86 fixture build; dotnet test with TRX in TestResults/native-bridge-complete-x64 and -x86 (110 passed per architecture); smoke-control-center.ps1 (three views); test-worker-measurement.ps1 (8 checks); test-measurement.ps1 (24 checks); measure-native-shell.ps1 Expanded/Notch, PostExpansion/Notch and Hidden/Notch; verify-native-scenario.ps1 for all three. Synthetic ABI mocks cover owned strings, byte values, timeline ticks, mock-only command slots, stale identity/token, async error/cancellation and reference balances. Real OS probes read only; no real player commands.

Rejected evidence: post-expansion/ failed during warmup because repeated BMP writes hit a file lock; no performance pass claimed. Its files remain. Initial test-host module assertion failure and intermediate compile errors remain in command/test history; final suites passed. The verifier initially expected the historical worker in every expanded Notch; it now distinguishes an explicit native bridge and requires no workers plus completed native initialization/teardown. Historical worker proof remains unchanged.

Gaps: real populated-player getters/playback, exclusive-D3D fullscreen, physical mixed-DPI monitors, Windows 10, 8 GB hardware and remote hosted CI are not certified. Existing sensor limitations remain. All Git operations remain manual.