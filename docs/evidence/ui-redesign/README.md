# Dashboard revision evidence

30 October 2026. Original Stats-inspired native dashboard, not a new M4-M7 milestone. The user handles all Git operations.

These records belong to the full Release app, with real CPU/RAM/network/battery/disk/process providers connected. GPU is explicitly unavailable. Individual binary SHA-256 hashes accompany every measurement; compare managed DLL hashes as well as the apphost executable. Diagnostic launches skip personal settings and write only requested reports. Metrics never modify machine configuration.

- collapsed-island/: 30-second warmup + 300.0096 measured seconds / 301 raw samples; numerical pass, strict collapsed state/lifecycle proof, independently reproduced CSV summary.
- expanded-island/: 30-second warmup + 300.0196 measured seconds / 301 samples; peak 64.598016 MB, machine-normalized CPU 0.37823044%; expanded state (module navigation ended on Battery detail) and independent CSV/lifecycle proof passed.
- hidden/: 30-second warmup + 300.0107 measured seconds / 301 samples; peak 37.154816 MB, zero recorded CPU seconds; native visibility false and zero sampling/disk/pointer/render work, independently verified.

Measurements report decimal MB and both core-equivalent and machine-normalized CPU (eight logical processors). Sampled working-set maximum is the memory budget gate; lifetime peak is retained separately. There is no working-set trimming or forced GC, and target/fixtures exit naturally. Other normal host applications and development tooling were present, so these are practical machine observations, not an isolated laboratory benchmark.

Executed validation:

- Release build: zero warnings/errors.
- x64 tests: 84 passed; x86 tests: 84 passed, actual ABI pointer size 4 logged in TRX.
- Shell smoke: seven cases, nine rendered style/state combinations and owned-borderless fullscreen hide/resume; no actual process termination.
- Dashboard smoke: 16 rendered pages/themes, 23 dashboard samples/disk requests; no WPF assemblies or timers left after natural exit. Visually reviewed actual frames in artifacts/ui-dashboard-final/frames/.
- Measurement tooling: 24 checks; native evidence validator: six checks including rejection of unexpected style/state history.
- Hosted CI: configured but not executed from this chat.

Full-sized BMPs and PNG previews remain under ignored artifacts/ui-dashboard-final; they show actual accessible application process names and are not a privacy-filtered compatibility export. They are the app's own buffer, not an OS desktop screenshot. No screenshot is uploaded or published.

Hardware limits: one physical display at 125% DPI, Windows build 26200. Mixed-DPI dual-display hardware, older Windows versions, absent battery, hybrid GPUs, screen-reader behavior and exclusive games remain untested. Missing-hardware fixtures do not establish physical-hardware acceptance. Existing virtual/VPN network filtering remains unfinished. See PROGRESS.md and docs/API_REGISTER.md for details.