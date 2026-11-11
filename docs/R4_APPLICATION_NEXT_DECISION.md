# Application memory decision after opaque comparison

31 October 2026. User selected "Test smaller 860×580 window at 90 MB": Option A is approved as a bounded experiment. Option B is not approved. No RAM limit changes.

The user-approved comparison changes only the large application HwndSource to UsesPerPixelOpacity=false with an opaque theme-color HwndTarget background. Bar/popups and x86/software rendering remain unchanged. Actual target opacity is recorded false in the smoke and full diagnostic reports. There is no DWM corner code, framework replacement, dependency, admin action, forced collection or memory trimming.

Release builds pass with zero warnings/errors. All **104 active tests pass on each actual x64 and x86 runtime** (70 Core / 13 Shell / 21 Windows). The new test verifies actual opaque target creation and zero metric factories/reads for the static overview. The bounded CPU smoke exits naturally, drains workers and exports 100/150 render scales. Render-target exports omit the HWND background and are not physical desktop screenshots. Actual Windows10/11 outer-frame appearance and mixed-DPI interaction remain unverified.

The opaque CPU full trial is docs/evidence/r4-dashboard/opaque-cpu-300s. Final numbers and lifecycle proof are recorded in PROGRESS.md after natural exit. An observed 91.037696 MB lifetime peak already exceeds 90 MB, so implementation stopped before another page/trial. Passing tests and lower observed RAM than the earlier transparent trial do not make this a resource pass. Different runs cannot isolate transparency as the exact cause of the difference.

Final result: 300.9904587 seconds/302 samples, observation max 86.368256 MB, lifetime/tail max 91.037696 MB, average machine CPU 0.3932329799%. Single CPU page, 343 reads/342 frames, actual per-pixel opacity false, lifecycle/activity/hash checks pass. The observation-only summary is Pass but the lifetime wrapper correctly rejects OverallAccepted. No subsequent page trial was started.

## Option A — keep 90 MB and test a smaller initial window

Bounded comparison of **860 × 580 DIP** instead of 940 × 650 DIP, preserving the same sidebar, wide cards, fonts and scrolling. Only initial application viewport dimensions change; bar and popup geometry stay fixed. Smaller WPF backing surfaces may reduce cost, but no improvement is claimed until measured. This preserves 90 MB without trimming, excluding costs or changing graphics/system settings.

Build/test, verify layout and full 300-second CPU/RAM/overview measurements including lifetime/captures. Stop again if any exceeds 90 MB. Do not convert more module pages until this gate passes. This is an experiment, not a promise that a slightly smaller window is sufficient or that 150% physical DPI will pass.

## Option B — explicitly allow 100 MB for the application window only

Keep the current 940 × 650 DIP design. Change only the large application-window cap to 100,000,000 bytes while it is open; bar-only and compact popups remain <=90,000,000 bytes, average CPU <1%, and hidden/closed-view work remains stopped. The old 90 MB trials stay failures against their original limit. Add separate application measurement context/gates before any continuation, then rerun CPU/RAM/overview/navigation/after-close/hidden tests. A 100 MB limit is not a claim that all future pages will pass.

If neither is approved, retain the current source and stop. Normal Release remains the frozen R3 build; opaque development output stays isolated in bin/r4-opaque-comparison. No Git operation or new dependency is requested by either option.
