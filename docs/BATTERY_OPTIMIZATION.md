# Battery optimization

The Dashboard Battery page provides model-aware instructions for setting an 80% charge limit through the laptop manufacturer's utility. The utility or firmware applies and reports the limit. Windows Smart Charging behavior varies by manufacturer, and Tidlet's existing power-mode APIs configure the performance and efficiency preference. [Windows Smart Charging](https://support.microsoft.com/en-us/windows/experience/power-battery/use-smart-charging-in-windows) · [Windows power-mode API](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfigureddcpowermode)

The guide uses the Dashboard's existing device model as a manufacturer hint. Model support and active charge limits are verified in the manufacturer's utility. The guide has no additional provider, polling, or system action.

Manufacturer paths checked 11 November 2026. [Acer Care Center](https://community.acer.com/en/kb/articles/140-calibrate-your-battery-using-acer-care-center) · [AcerSense](https://community.acer.com/en/kb/articles/17933-how-to-manage-optimized-battery-charging-in-acersense) · [ASUS](https://www.asus.com/support/faq/1045651/) · [Dell](https://www.dell.com/support/manuals/en-us/mydell/mydell_3_3_ug/Customizing-a-battery-setting?guid=guid-539096da-6e56-4d86-a728-3788b06947b8&lang=en-us) · [Surface](https://support.microsoft.com/en-us/surface/battery/smart-charging-on-surface)

## Verification

The isolated Release build completed with zero warnings or errors. All 18 x86 Dashboard tests passed, including opening the guide from Battery, leaving power actions untouched, and returning to Battery. The local x86 preview showed the Acer instructions at 100% and 150%, exited cleanly, and recorded zero Dashboard metric reads. The manufacturer control and an actual charging cycle require hardware verification. The build is available under `src/IsletMonitor/bin/battery-optimization`. Fresh five-minute resource acceptance is pending.
