# R0 source preservation and active-build inventory

30 October 2026. The latest change request supersedes the old island/notch UI and native renderer. No files in the old implementation were deleted, no Git history was rewritten, and no branch/tag/commit was created during feasibility work. Legacy sources remain available for the user's manual Git snapshot; preservation on a Git tag/branch is not claimed.

Production exclusions:
- Islet.Shell: Island/**/*.cs and ScaffoldWindow.cs. Includes WPF island experiments, native island host and the unfinished desktop redesign.
- Islet.Windows: Native/**/*.cs, Display/**/*.cs and Media/**/*.cs. Includes native island/dashboard renderer, Lucide geometry, media projection/worker and native bridge.
- IsletMonitor: old Program.cs, App.xaml.cs and App.xaml markup. New entry is BarProgram and the R0 WPF fixture.

The compile exclusions are explicit in the project files. Existing portable settings/presentation/geometry contracts remain temporarily for migration and pure test coverage; there is no active style-switching or creature UI. Existing hardware providers, including the user's newer battery-capacity work, remain compiled. No network ping/public-IP operation is started by the R0 entry point.

Retired presentation tests (preserved source, not executed/passing claims): Shell ScaffoldWindowTests, NativeIslandHostTests, M2HostTests, IslandProbeWindowTests, InteractiveIslandTests and DashboardHostTests; Windows IslandProbeTests, NativeRendererTests, NativeResourceTests, MediaWorkerTests, MediaProviderTests, NativeMediaTests and NativeMediaAbiTests. Their UI/runtime no longer exists in the active production build. New BarScaffoldTests cover missing/invalid display values and diagnostic launch validation. Core math/schema/storage and Windows provider/PDH/platform-boundary tests remain active.

Actual first R0 run: 74 tests passed (Core 57, Shell 2, Windows 15); no failures/skips. This smaller count is deliberate retirement, not proof that the removed UI remains working. Hosted CI is not claimed. CI source now targets the R0 surface, not obsolete CLI routes.

Legacy third-party assets under assets/lucide remain archival and unreferenced by production; the R0 WPF fixture uses Segoe UI text only. No icon pack is added. Screenshots copied to docs/reference are ignored and not intended for redistribution. No R1 AppBar registration, R2 popup kit or R4 dashboard feature is claimed.