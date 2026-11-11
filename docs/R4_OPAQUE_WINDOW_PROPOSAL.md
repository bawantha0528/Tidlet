# Bounded opaque WPF application-window comparison

31 October 2026. User selected "Approve opaque WPF comparison". Execute this bounded comparison; keep the 90 MB limits and CPU/hidden-work gates unchanged. A pass is not claimed in advance.

The approved wide-page correction is implemented for CPU/RAM. Bounded smokes exported 100/150 render scales and exited cleanly. All 103 active tests passed on actual x64 and x86 runtimes (70 Core / 12 Shell / 21 Windows), including changing-data/DPI pixel regression and predecessor-wait/closed-page provider tests. Five-minute corrected CPU evidence is docs/evidence/r4-dashboard/wide-cpu-300s; final result is recorded in PROGRESS.md. An observed lifetime peak above 90 MB stopped implementation before more pages or another resource trial.

Completed result: 300.007616 s/301 samples, observation max 94.842880 MB, whole lifetime/tail max 94.949376 MB, average machine CPU 0.3496004998%. RAM fails; CPU passes. Single held CPU page, 345 reads/344 frames, lifecycle/activity checks and binary hashes pass. No fixed-page ambiguity in this run. RAM/overview/navigation trials were not started after the failure.

The new layout uses the full application content width. Top-bar popups remain independent. CPU/RAM alone are converted; GPU/Disk/Network/Battery application pages currently explain that wide-page conversion awaits validation. Sensor helper remains absent. Do not call this completed R4/R5.

## Proposed next experiment

Change only the large application HwndSource from per-pixel transparency to an opaque WPF host with a solid theme background. Preserve the wide layout, font, colors, charts and sidebar. Leave the bar and compact popup hosts unchanged. Keep software-rendering preference and x86 architecture so the experiment does not simultaneously change unrelated factors.

A large transparent backing surface is a possible contributor, not a proven cause. Compare hashes, loaded components and complete cold/lifetime memory before attributing savings. No resource-shifting worker, forced collection or working-set trimming. No change to Windows graphics/security settings.

Windows11 system-rounded outer corners may be requested only through verified per-window DWM APIs; unsupported versions use a solid rectangular outer frame with rounded inner cards. Corner support is cosmetic and does not justify a driver/package. No region hacks or compositor hooks. The purpose is to compare the documented opaque WPF path, not to introduce a new renderer or framework.

Verify current HwndSourceParameters.UsesPerPixelOpacity and HwndTarget.BackgroundColor behavior before implementation. Sources checked for proposal: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsourceparameters.usesperpixelopacity ; optional Windows11 corner guidance: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-rounded-corners and https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference . Actual memory improvement and cross-version visuals are unverified.

Acceptance: Release build and both-architecture tests; bounded 100/150 visual exports; full 300-second held-input CPU/RAM/overview trials; complete lifecycle and unchanged binary hashes; count capture/startup/tail peaks. Then navigation/after-close/hidden tests before remaining module-page conversion and polish. Stop on another failure. No new dependency, admin/system setting or Git operation.

AGENTS.md requires review after a budget failure. This proposal does not raise the cap or claim a pass in advance.
