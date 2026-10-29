# Tidlet Monitor — progress

Updated: 29 October 2026 (Asia/Colombo).

## M0 — implemented and locally verified; external acceptance pending

**What was built**

- .NET 8 solution with Islet.Core, Islet.Shell, Islet.Windows and IsletMonitor, plus three real test projects.
- Plain runnable WPF bootstrap, portable product identity, capability-probe invariants and initial settings-schema contracts. No M1 island/sensors/persistence/fixers implemented.
- Exact SDK pin, test-only central package versions, seven lock files, deterministic/warnings-as-errors builds, EditorConfig, Git attributes/ignore, MIT license, README, engineering agreement and plan.
- Read-only process measurement script, deterministic/integration tooling checks, nonempty/failed-suite guard and application startup/natural-exit smoke check.
- Windows GitHub Actions build/test workflow with verified commit pins, locked restore, real test-result guard, tooling/smoke checks, x64/x86 builds and retained evidence.
- Active source project is now directly in `C:\Users\bawan\OneDrive\Documents\Tidlet`. All 56 transfer files were moved with SHA-256 verification; the extra extraction wrapper was removed. No Git repository was initialized or copied into this folder.

**How it was verified (commands run, results)**

All build/test invocations used child-process telemetry opt-outs. Local SDK/cache paths and MSBuildEnableWorkloadResolver=false were supplied only to their process environment. Builds/tests and benchmark ran outside the restricted process sandbox, under a verified non-administrator token; no UAC elevation, OS configuration change or driver installation.

| Command/check actually run | Result |
|---|---|
| dotnet --version | 8.0.414 |
| dotnet restore TidletMonitor.sln | Passed; exact disclosed test versions restored |
| dotnet restore TidletMonitor.sln --locked-mode | Passed |
| dotnet build -c Release | Passed; 0 warnings / 0 errors |
| dotnet test -c Release --no-build --no-restore --logger trx --results-directory TestResults | Passed; 14 executed, 0 failed, 0 skipped, three suites |
| dotnet test | Passed; 14 tests in default Debug configuration |
| scripts/verify-test-results.ps1 | Passed; all three expected suites, 14 passed |
| scripts/test-measurement.ps1 | Passed; 24 checks |
| scripts/smoke-app.ps1 | Passed; two app startup/argument cases, natural exit only |
| x64 / x86 app builds with PlatformTarget and separate OutputPath | Both passed; 0 warnings / 0 errors |
| PE-header inspection | x64 AMD64 apphost; x86 I386 apphost + Requires32Bit IL verified; default measured host AMD64 |
| Application deps.json/output audit | Project assemblies only; no test or other NuGet runtime dependency |
| NuGet lock + downloaded nuspec inspection | Resolved test graph recorded; declared licenses MIT |
| git diff --check | Passed |
| scripts/measure.ps1 -ProcessId <owned-host-pid> -Scenario ScaffoldIdle -DurationSeconds 300 -WarmupSeconds 30 -OutputDirectory artifacts/measurements/m0-scaffold | Passed completeness; Result=BaselineOnly |
| GitHub-hosted CI | Not run: no remote/repository was specified or created |

Tooling coverage includes CPU normalization, memory statistics/boundaries, incomplete windows, counter reversal, nonfinite/missing samples, invalid PID, a naturally exiting target, diagnostic-only short runs and rejection of empty/failed/unexecuted synthetic TRX. No real process termination or system-setting action was tested or executed.

The initial sandboxed compiler task was canceled; the authorized execution channel resolved that environment limitation. A template option and two verification-fixture/exit-handling defects were corrected before the successful final runs. These earlier failures are not counted as passes.

**Measured RAM and CPU**

One plain **M0 scaffold baseline**, not a transparent island or expanded-graph acceptance run:

| Observation | Actual result |
|---|---:|
| Post-warmup duration | 300.0059021 s |
| Samples | 301 |
| Working set mean | 98.223 MB / 93.67 MiB |
| Working set median | 106.373 MB |
| Working set p95 / sampled maximum | 111.534 MB / 106.367 MiB |
| Lifetime peak captured | 111.546 MB |
| Process CPU consumed | 0.34375 CPU-seconds |
| Average CPU, core-equivalent | 0.114581% |
| Average CPU, normalized by 8 logical processors | 0.0143226% |
| Observer CPU, including warmup/tooling work | 3.65625 CPU-seconds, outside the app |
| Host reported physical memory | 8,375,296,000 bytes / 7.8001 GiB |
| Host reported Windows build | 10.0.26200.0 |
| Administrator token | False |
| Target runtime raw product version | 8,0,2025,41914 (as read from loaded coreclr) |

No forced GC, working-set trimming, affinity/power-plan tuning or omitted child helper cost. The app had no helper. The host was started with a Hidden startup preference, but exposed a nonzero main window handle/title; no physical visibility/render-tier/DPI assertion is inferred from that. Concurrent development occurred on the host; this is not an isolated lab result. Preserve the unfavorable memory sample rather than discarding it.

Raw evidence: [summary](docs/evidence/m0/scaffold-summary.json), [301 samples](docs/evidence/m0/scaffold-samples.csv), [environment](docs/evidence/m0/environment.json), [architecture/hash inspection](docs/evidence/m0/build-architectures.json), [checks](docs/evidence/m0/tooling-checks.json), [verification record](docs/evidence/m0/verification.json).

**Known gaps**

- **Memory risk:** 111.534 MB sampled maximum is above both future island memory targets (60 MB collapsed, 100 MB expanded). The scaffold is a plain WPF window, so this does not establish a transparent-window failure or a budget pass. Budgets are unchanged. M1 must begin with the actual small transparent-window feasibility experiment and STOP/report/propose an alternative if transparency cannot meet them.
- **CI acceptance remains open:** configured workflow is not a successful hosted run. No remote or public repository was guessed/created.
- Requested placement and local build access are now resolved; see the relocation verification below. Earlier access failures are historical. The agent changed no Windows security settings.
- x86 compiled and PE architecture inspected; x86 execution and actual Win32 structure layouts are not verified. Native interop starts in M1.
- Two physical monitors/mixed DPI, full sensor coverage, fullscreen/hidden behavior and final full-feature 8 GB compatibility are not tested. M0 was observed on a nominal 8 GB-class system only.
- .NET 8 support ends 10 November 2026; installed SDK/runtime are older servicing builds. No retargeting or machine upgrade performed.
- No screenshot-level UI correctness or render-tier claim. A source build plus an actual startup/exit was verified.
- No M1–M3 feature work has begun.

**Next step**

Run the configured workflow on an authorized GitHub remote to close the remaining M0 CI gate. Future M1 work must begin with the WPF feasibility measurement. Active development remains in the requested Tidlet folder; the user performs all Git operations manually.

## Historical source commits and current acceptance

- Foundation plan commit: 75eb2f5.
- Historical M0 implementation/evidence commit in the old output backup: 060ee41. This predates the manual-commit instruction; it was not copied as Git history into this folder.
- Local implementation and verification: complete.
- Requested workspace placement: verified. Full M0 acceptance still awaits a hosted CI run.

M1–M3 not started. M4–M7 out of scope.

## Previous transfer attempts and Git policy

The user designated `C:\Users\bawan\OneDrive\Documents\Tidlet` as the only active project folder and will commit manually every time. Updated AGENTS, decisions, README and plan to override prior auto-commit directions. No staging, commit, Git initialization or push was performed in this turn.

Earlier transfer attempts were blocked by Windows Controlled folder access. The source was then extracted into an extra child folder. These failures are resolved by the verified relocation below; they are preserved here as history.

## Relocation verified in the canonical project folder

All 56 extracted files were moved from TidletMonitor-project-transfer into the Tidlet root after explicit source/destination boundary and overwrite checks. SHA-256 matched every moved file before documentation updates. The empty wrapper was removed only after verification. TidletMonitor.sln, src, tests, scripts and docs are now directly in the requested project folder.

Verification executed here:

- `dotnet --version`: 8.0.414.
- `dotnet restore TidletMonitor.sln --locked-mode`: passed; cache/output stays under this project's ignored artifacts folder.
- `dotnet build -c Release --no-restore`: passed, 0 warnings / 0 errors.
- `dotnet test -c Release --no-build --no-restore --logger trx --results-directory TestResults`: 14 passed, 0 failed, 0 skipped.
- `scripts/verify-test-results.ps1`: all three expected suites and 14 passed.
- `scripts/test-measurement.ps1`: 24 checks passed.
- `scripts/smoke-app.ps1`: two startup/argument/natural-exit cases passed.

Only project-location/rule/progress documents changed; application code was not changed. The original five-minute M0 baseline above remains historical and was not repeated for this directory-only correction. No staging, commits, Git initialization, push, system-setting changes or real process termination were performed. The user reported changing Windows protection before this successful retry; the agent did not change or verify that security configuration.

Machine-readable result: [relocation verification](docs/evidence/m0/relocation-verification.json).
