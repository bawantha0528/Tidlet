# R0 WPF bar budget failure: review required

30 October 2026. R0-R2 feature work is stopped at the feasibility gate. This is a measured failure of the current transparent WPF configuration, not proof that every possible WPF configuration fails. The active 50 MB/90 MB limits remain unchanged.

## Executed result

A minimal .NET 8/WPF 240 x 24 DIP Overlay-style feasibility window with two live CPU/RAM text items, no icons, chart, process enumeration, AppBar registration or helper was measured. AllowsTransparency=true, WindowStyle=None, dark background alpha 204/255, default WPF rendering. CPU/RAM sampled every 3 seconds; no continuous application rendering handler. Normal non-admin interactive Windows desktop, build 26200, eight logical processors, affinity FF, actual monitor scale 125%. No working-set trimming, forced collection, system setting changes or telemetry. Other applications were active; an isolated x86 build/test verification ran during the observation. No second Tidlet instance was running when it started.

30-second warmup, then 300.0044968 seconds / 301 observations:
- Peak working set 111,497,216 bytes (111.497216 decimal MB), versus 50,000,000: exceeds by 61,497,216 bytes.
- Mean working set 100.955908 MB; p95 111.398912 MB.
- CPU 1.265625 seconds, 0.0527335846% machine normalized / 0.4218686765% core equivalent. CPU passes; memory fails.
- Window first frame observed; 115 live sample attempts across its full bounded life; owned timers stopped, natural exit code zero.
- Runtime reports no compiled legacy island shell/native renderer, no managed media assemblies and no AppBar reservations.

Evidence: docs/evidence/r0-wpf-bar/transparent contains raw CSV, full summary, source/binary hashes, lifecycle probe, independent raw-check.json and a WPF render-target PNG (not a desktop screenshot). The PNG is 125% scale; 100%/150% R2 comparison screenshots are not available because R2 has not been built. The observer's generic process-architecture TODO remains explicit; no unsupported hardware certification is inferred from the successful x86 tests.

## Options for review

1. Measure the already-requested solid WPF fallback with AllowsTransparency=false, keeping the same tiny bar, providers, 50 MB limit, warmup and full duration. This is the smallest next experiment and preserves the requested stack. It has not been measured; it might still exceed 50 MB. No R1/R2 feature work until it passes. Solid fallback is permitted by the brief, but after this failed transparency gate the engineering agreement requires review before continuing.
2. Approve a renderer exception specifically for the new tiny bar and CPU/RAM popups: native C#/.NET 8 Win32 presentation, with the same requested layout, Windows font icons, provider/capability contracts and 50/90 MB gates. No island/notch/media/creature behavior returns, no new dependency, elevation or driver. Prior native results suggest an avenue worth testing but do not certify this different surface, its data sources or its tighter budget. Measure a new prototype before further implementation. Any later WPF dashboard remains R4 and would need its own measured costs.

Do not silently raise budgets, optimize by trimming, claim acrylic/Mica performance without measuring, or substitute a native renderer for the requested WPF stack without approval. No AppBar/work-area or process-kill testing occurred. R0 cleanup/scaffold is implemented, but the milestone is not accepted while the feasibility gate fails. R1/R2, Reserve crash recovery, mixed-DPI physical hardware and hosted CI remain unverified. All legacy source files and newer battery work are preserved; no Git operations were performed.
## Solid fallback follow-up

The user requested retaining WPF and emphasized RAM usage. The full solid-WPF trial completed: 301.002362 seconds / 302 observations, peak 112.783360 MB, mean 99.447801 MB, CPU 0.0428256606% machine normalized. It also fails the unchanged 50 MB cap. Raw recomputation, matching binary hashes and lifecycle proof are retained in docs/evidence/r0-wpf-bar/solid. The native-renderer alternative was not selected; keep WPF. Review docs/WPF_MEMORY_NEXT_EXPERIMENT.md for a WPF-only current-process software-rendering experiment; no further implementation or relaxed limit is claimed.
