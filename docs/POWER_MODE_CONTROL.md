# Windows power mode control

Add a Power mode control to the Dashboard Battery page. Show the configured mode for the current power source and let the user explicitly choose Best power efficiency, Balanced, or Best performance. Recheck the source before applying a change. Use the Windows Power & battery page when direct control is unavailable.

Use the Windows 11 AC/DC power-mode APIs and the existing power-source probe. Avoid periodic power-mode reads and keep the control outside the strip and popup sampling paths. Diagnostic runs are read-only and automated action tests use a fake implementation. Windows policy and other system signals can override the effective mode.

API references checked 11 November 2026. [AC read](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfiguredacpowermode) · [AC write](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfiguredacpowermode) · [DC read](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfigureddcpowermode) · [DC write](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfigureddcpowermode) · [Power status](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-system_power_status) · [Windows Settings URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings-app)

## Verification

- Isolated Release solution build passed with zero warnings and errors. All 18 focused x86 application-page tests passed. The mock exercised an accepted change, a rejected change, and the Windows Settings fallback.
- A native read-only x86 Battery preview read the configured plugged-in mode and rendered the selector at 100% and 150%. It exited naturally with workers drained and the tray removed. No live write or Settings launch was performed.
- Another Tidlet instance was running, so no new five-minute trial was started. The preview is in `src/IsletMonitor/bin/power-mode`. Normal Release remains at the prior build.
