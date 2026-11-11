# WPF RAM budget decision - required user review

30 October 2026. No budget or default architecture change is approved by this document. The current bar limit remains 50,000,000 bytes and popup limit 90,000,000 bytes. R1/R2 remain stopped when a gate fails.

The approved measured configurations so far are transparent WPF Window (111.497216 MB peak), solid Window (112.783360 MB), software-preferred solid Window with lazy menu (84.627456 MB), and software-preferred lightweight x64 WPF visual host (76.337152 MB). These are running-process working sets, not application disk size. No absolute WPF minimum is proved. The genuine x86 visual host completed at 60.882944 MB peak / 58.714908 MB mean and 0.0403642171% machine CPU over 300.0027221 seconds (301 samples). It fails 50 MB by 10.882944 MB. Raw replay, seven hashes and actual X86/four-byte-pointer diagnostics agree; evidence is docs/evidence/r0-wpf-bar/genuine-x86. Its installed runtime patch differs from the x64 run, so no bitness-only causality is claimed.

Because the x86 trial also fails 50 MB, recommended review option: authorize a 75,000,000-byte bar working-set limit and the genuine x86 WPF visual host as the R1 baseline. Keep the popup limit at 90,000,000 bytes and average process CPU below 1% over 300 seconds. This is an explicit requested requirement change, not a claim of meeting the original 50 MB target. A 75 MB bar is approximately 0.9% of 8 GiB; this comparison is arithmetic, not a measurement of comfort under memory pressure.

This allows measured headroom for the still-missing network/battery items, tray, menus and capability handling. It does not guarantee that R1 or R2 will fit: measure bar, open popup and after-close behavior, count every auxiliary cost, and stop again on a failure. WPF, no-driver/no-admin defaults, no telemetry, no new dependency without approval and all safety/compatibility requirements remain. Mixed-DPI, Windows 10 and all-hardware support are acceptance gaps until tested. Future architecture/release packaging remains unverified; no ARM64 work begins.

Alternative: retain the 50 MB limit and stop feature implementation here. Further profiling may find savings, but another speculative experiment is not automatically authorized. No native renderer switch, working-set trimming, forced collection, private-bytes substitution or discarded child cost is proposed.

Approval requested: 75 MB bar limit and genuine x86 WPF visual host for R1-R2, with existing popup/CPU limits and stop-on-failure rules. Until explicitly approved, the 50 MB bar limit and current default launch remain unchanged. No software configuration is changed by this document.


## Explicit user approval - 30 October 2026

User selected "Approve 75 MB bar + x86 WPF". This approves the 75,000,000-byte bar cap and genuine x86 WPF visual host baseline for R1-R2. Popup <=90,000,000 bytes and CPU <1% over 300 seconds remain unchanged. Historical 50 MB results remain failed under their original gate. No additional dependency, system change or later milestone is approved. Current operative status is this approval, superseding the proposal's pending wording above.
