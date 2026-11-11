# Compact bar with remembered order

User clarification: hiding an item closes its gap; showing it again restores its previous order. Keep enabled Items as the compact visible layout and persist a separate module order. Migrate older settings by retaining their current visible order and appending unconfigured modules. Use the same remembered order for header/settings/context toggles and explicit Move earlier actions. Preserve all-off behavior and sampling rules.

Add focused order/spacing/export-import/reorder tests, build Release and run affected tests. No new dependency/API or full performance rerun for this settings/layout correction. No Git or machine-setting action.

## Validation

Release solution build using -p:OutputPath=bin/toggle-order/ passed with zero warnings/errors. Ten focused tests passed in the x64 configuration (seven Core bar policy tests and three Shell toggle/lifecycle tests). Layout checks cover all alignments, compact 10-DIP gaps, every original item hidden/restored, reverse-order re-enabling after all-off, export/import, and explicit Move earlier while another item is hidden. Toggle mouse routing and all-off pause behavior remain covered. No Windows tests matched this filter. No full resource rerun or new hardware acceptance claim.

The same ten focused tests also passed in the x86 test-host configuration. Build output: src/IsletMonitor/bin/toggle-order/IsletMonitor.exe. No separate native input or performance trial; existing native header correction retained.

After user exit, normal Release solution build passed with zero warnings/errors and was updated from this source. README points to that executable.
