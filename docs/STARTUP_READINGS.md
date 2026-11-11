# Startup readings

Show a compact measuring status in the strip and a module-specific first-reading explanation in each popup and Dashboard page. Keep real recent readings in the existing session cache and label them during refresh. When there is no recent reading, explain the pending work. Label charts until enough real samples arrive. Keep missing readings distinct from measuring and preserve the current monitoring intervals.

The strip can reuse the last CPU, GPU, or network reading for up to two minutes while the corresponding counters restart. Popups and pages reuse their existing sanitized session cache. On an app restart, the first views show measuring text until data arrives. The cache stays in memory.

Native x86 diagnostic runs captured the bottom strip, GPU popup, CPU page and Disk page before the second strip poll. The strip showed its immediately available RAM and battery values alongside Measuring labels. The GPU popup explained its first read, and the CPU page displayed a real utilization value with a waiting label in the chart. The previews are under `artifacts/startup-readings-preview`. The earlier UI audit instance was running, so the five-minute resource trial remains pending.

## First-reading details

Display verified disk volume capacity, GPU adapter inventory and network interfaces from the first provider read when they are already available. Keep pending rates labeled Measuring. Once Windows reports a missing reading, show its reason and skip charts that have no samples. The smoke script accepts `-Startup` to render the full UI before the second poll.

The final isolated Release build passed with zero warnings or errors. All 143 x86-configured tests passed. The startup smoke matrix passed all 42 Dark and Light cases, including every strip placement, popup, Dashboard module, Settings, About, and Battery optimization. The popup and page images were rendered at 100% and 150%. The older startup preview was still running, so five-minute resource acceptance remains pending. The audited executable is under `src/IsletMonitor/bin/startup-complete-final`.
