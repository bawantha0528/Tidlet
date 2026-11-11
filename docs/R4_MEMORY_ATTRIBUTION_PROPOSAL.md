# Attribute application memory before further UI changes

31 October 2026. User explicitly approved memory profiling at 90 MB. Implement only this bounded attribution investigation; keep all existing 90 MB, CPU and hidden-work limits unchanged. Report findings and a correction proposal before changing production behavior.

The requested single-scale capture option is implemented and passes the active build/tests and short capture checks. The first full 100% navigation trial exceeded 90 MB before its export. Final trial numbers are recorded in PROGRESS.md. The 150% run must not start after this failure. Earlier static passes remain historical observations, not proof of a dependable memory margin across runs.

Completed first run: 300.001333 s/301 samples, observation peak 88.408064 MB, lifetime/tail 91.525120 MB, average machine CPU 0.2239573382%; RAM fail at unchanged 90 MB. One verified 100% PNG (860x580), 87 page visits, 201 requests/157 frames, lifecycle/hash/cleanup/capture checks passed. Peak was observed before capture. Full 150% trial was skipped by the sequential stop-on-failure wrapper. Current suites pass 106 each on actual x64/x86.

## Bounded investigation

Add a diagnostic-only phase recorder using existing .NET APIs. Record process working set, reported lifetime peak, private committed bytes, GC collection counts, approximate cumulative allocation and the latest available GC heap/committed statistics at: before bar creation; after first bar frame; before/after application HWND creation; completion of device inventory; first module probe/sample; selected navigation transitions; capture start/end; and window close. Record elapsed time and native module basenames at a few checkpoints, with no usernames, full paths, serials, IPs or process lists. Cap record counts and write only inside the project evidence directory.

GC.GetGCMemoryInfo describes collection snapshots; mark unavailable/pre-first-collection values and do not label them live object size. Private committed bytes, GC committed bytes, module virtual image size and resident working set are different quantities. Do not subtract them to invent a native-resident byte count. Module presence identifies a loading phase, not its exact resident cost.

Run one bounded full navigation diagnostic with the external observer and retain raw data/hashes/lifecycle. This is attribution work, not a resource pass or permission to continue building module UI. Include the diagnostic's own process costs; no helper, GC.Collect, working-set trim, VirtualLock, heap-limit/latency-mode manipulation or machine/registry/security change.

Use the evidence to propose one focused correction. Current source review found DeviceOverviewSource.Read unconditionally enumerates DXGI adapters even when the window opens on CPU/RAM; that is a candidate to investigate, not a demonstrated cause of the failure. The navigation sequence also includes Dashboard, so simply postponing inventory is not assumed to solve the cap. Other candidates are WPF drawing/text churn, provider snapshots and retained native surfaces. Do not claim attribution without measurements.

Verify each added counter against current official docs before implementation. Already checked: GC committed memory semantics https://learn.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo.totalcommittedbytes ; collection snapshot implementation https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/GCMemoryInfo.cs . Any optional deeper native resident-page analysis needs separate verified bindings; it is not included in this initial BCL-only phase recorder. No new dependency or admin requirement.

After attribution, report the evidence and concrete correction proposal before modifying production behavior. Do not run further page-completion or resource acceptance trials after the current failure without this review. Normal Release remains R3 and all newer output stays isolated. The user's Git policy is unchanged.
