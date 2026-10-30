using Islet.Core.Activities;
using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Core.Presentation;
using Islet.Core.Settings;
using Islet.Shell.Island;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class DashboardHostTests
{
    [TestMethod]
    public void UnsupportedProbeAndGarbageReadingsNeverBecomeZeroGaugesOrHistory()
    {
        var desktop = new Desktop(); var ram = new Ram(); var battery = new Battery(); var network = new Network();
        using var host = new NativeIslandHost(desktop, new Cpu(), ram, network, battery);
        host.Start(new NativeIslandOptions { State = IslandState.Expanded });
        desktop.Tick(IslandTimer.Metric);
        var cards = desktop.Frame!.Dashboard!.Cards;
        var memory = cards.Single(c => c.Page == MonitorPage.Memory);
        Assert.IsNull(memory.Percent); Assert.IsTrue(memory.History.All(v => v is null));
        Assert.AreEqual("Unavailable", memory.Value);
        Assert.IsTrue(cards.Single(c => c.Page == MonitorPage.Battery).Absent);
        Assert.AreEqual(0, battery.Reads);
        Assert.AreEqual("Unavailable", cards.Single(c => c.Page == MonitorPage.Network).Value);
        Assert.AreEqual("Unavailable", cards.Single(c => c.Page == MonitorPage.Gpu).Value);
    }
    [TestMethod]
    public void ExpandedPollingStopsWhenCollapsedOrSuppressedAndResetsOnResume()
    {
        var desktop = new Desktop(); var ram = new Ram();
        using var host = new NativeIslandHost(desktop, new Cpu(), ram, null, null);
        host.Start(new NativeIslandOptions { State = IslandState.Expanded });
        desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(1, ram.Reads);
        host.SetState(IslandState.Collapsed); desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(1, ram.Reads);
        host.SetHidden(true); desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(1, ram.Reads);
        host.SetHidden(false); host.SetState(IslandState.Expanded);
        Assert.IsTrue(ram.Resets >= 2);
        Assert.IsNull(desktop.Frame!.Dashboard!.Cards.Single(c => c.Page == MonitorPage.Memory).Percent);
    }
    [TestMethod]
    public void ShortClicksUseSignedClientCoordinatesAtCurrentDpiAndThemeChangesWithoutSampling()
    {
        var desktop = new Desktop();
        using var host = new NativeIslandHost(desktop, new Cpu());
        host.Start(new NativeIslandOptions { State = IslandState.Expanded });
        var tab = DashboardLayout.Tab(3, 860);
        desktop.Signal(new(IslandInputKind.LeftClick, X: (int)((tab.X + 5) * 1.5), Y: (int)((tab.Y + 5) * 1.5)));
        Assert.AreEqual(MonitorPage.Network, host.SelectedPage);
        var theme = DashboardLayout.ThemeButton(860);
        desktop.Signal(new(IslandInputKind.LeftClick, X: (int)((theme.X + 5) * 1.5), Y: (int)((theme.Y + 5) * 1.5)));
        Assert.AreEqual(ThemeMode.Light, desktop.Frame!.Dashboard!.Theme);
        Assert.AreEqual(0, host.SampleAttempts);
    }
    [TestMethod]
    public async Task CompactNotchRoutesRealActionsAndStopsMediaPollingWhenInvisible()
    {
        var desktop=new Desktop(); var media=new Media(); var disk=new Disk(); double now=0;
        using var host=new NativeIslandHost(desktop,new Cpu(),null,null,null,()=>now,disk:disk,media:media);
        host.Start(new NativeIslandOptions { Style=IslandStyle.Notch, State=IslandState.Expanded });
        Assert.IsNotNull(desktop.Frame!.Dashboard!.ControlCenter);
        Assert.AreEqual(930,desktop.Frame.Bounds.Width); // 620 DIP at 150%.
        desktop.Tick(IslandTimer.Metric);
        for(var i=0;i<100 && media.Reads==0;i++) await Task.Delay(10);
        desktop.Tick(IslandTimer.Pointer);
        desktop.Signal(new(IslandInputKind.Command,ControlCenterLayout.FocusToggle));
        now=1500; Assert.AreEqual("24:59",host.FocusStatus.Display);
        host.SetHidden(true); now=1_600_000;
        for(var i=0;i<3;i++) desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(1,media.Reads); Assert.IsFalse(media.Visible); Assert.AreEqual(0,disk.Reads);
        Assert.AreEqual(FocusState.Complete,host.FocusStatus.State);
        host.SetHidden(false);
        desktop.Signal(new(IslandInputKind.Command,ControlCenterLayout.FullMonitor));
        Assert.IsNull(desktop.Frame.Dashboard!.ControlCenter);
        Assert.IsTrue(desktop.Frame.Dashboard.CanReturnToNotch);
        desktop.Signal(new(IslandInputKind.Command,ControlCenterLayout.ReturnToNotch));
        Assert.IsNotNull(desktop.Frame.Dashboard!.ControlCenter);
        host.SetState(IslandState.Collapsed); now+=5000; desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(1,media.Reads);
    }
    [TestMethod]
    public async Task NativeClickRoutesOnlyAllowedPlaybackToMockAndNeverWhileHidden()
    {
        var desktop=new Desktop(); var media=new ControllableMedia();
        using var host=new NativeIslandHost(desktop,new Cpu(),null,null,null,media:media);
        host.Start(new NativeIslandOptions { Style=IslandStyle.Notch, State=IslandState.Expanded });
        desktop.Tick(IslandTimer.Metric);
        for(var i=0;i<100 && host.MediaStatus.State!=MediaState.Available;i++)
        { await Task.Delay(10);desktop.Tick(IslandTimer.Pointer); }
        Assert.AreEqual(MediaState.Available,host.MediaStatus.State);
        desktop.Signal(new(IslandInputKind.LeftClick,X:210,Y:375)); // Playback button at 150% DPI.
        for(var i=0;i<100 && media.Commands==0;i++) await Task.Delay(10);
        Assert.AreEqual(1,media.Commands); Assert.AreEqual(MediaCommand.TogglePlayback,media.LastCommand);
        host.SetHidden(true); desktop.Signal(new(IslandInputKind.Command,ControlCenterLayout.Next));
        Assert.AreEqual(1,media.Commands); Assert.AreEqual(MediaState.Waiting,host.MediaStatus.State);
    }
    private sealed class ControllableMedia : IMediaProvider
    {
        public int Commands; public MediaCommand? LastCommand;
        public CapabilityProbeResult Probe()=>CapabilityProbeResult.Supported;
        public Task<MediaReading> ReadAsync()=>Task.FromResult(new MediaReading(MediaState.Available,"Mock song","Mock artist","mock",CanToggle:true,CanNext:true));
        public Task<MediaCommandResult> ExecuteAsync(MediaCommand command,string token)
        { LastCommand=command;Interlocked.Increment(ref Commands);return Task.FromResult(new MediaCommandResult(true,"Mock playback only")); }
    }
    private sealed class Disk : IDiskProvider
    {
        public int Reads; public bool Visible;
        public void SetVisible(bool visible)=>Visible=visible;
        public CapabilityProbeResult Probe()=>CapabilityProbeResult.Supported;
        public DiskReading Sample() { Reads++;return new(DiskReadingState.Available,100,200,null,"Mock disk"); }
        public void Reset() { }
    }    private sealed class Media : Islet.Core.Activities.IMediaProvider, IMediaVisibilityAware
    {
        public int Reads; public bool Visible;
        public void SetVisible(bool visible)=>Visible=visible;
        public CapabilityProbeResult Probe()=>CapabilityProbeResult.Supported;
        public Task<Islet.Core.Activities.MediaReading> ReadAsync()
        { Interlocked.Increment(ref Reads);return Task.FromResult(new Islet.Core.Activities.MediaReading(Islet.Core.Activities.MediaState.NoSession,"Not playing","No player")); }
        public Task<Islet.Core.Activities.MediaCommandResult> ExecuteAsync(Islet.Core.Activities.MediaCommand command,string token)=>throw new InvalidOperationException("No real media commands in tests");
    }    private sealed class Cpu : ICpuProvider
    {
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public CpuReading Sample() => new(CpuReadingState.Available, 25, "CPU OK");
        public void Reset() { }
    }
    private sealed class Ram : IRamProvider
    {
        public int Reads, Resets;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public RamReading Sample() { Reads++; return new(RamReadingState.Available, 100, 200, 10, 50, "Bad memory sample"); }
        public void Reset() => Resets++;
    }
    private sealed class Battery : IBatteryProvider
    {
        public int Reads; public bool Visible;
        public void SetVisible(bool visible)=>Visible=visible;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Unsupported("battery.none", "No battery detected.");
        public BatteryReading Sample() { Reads++; throw new InvalidOperationException("Must not sample absent battery"); }
        public void Reset() { }
    }
    private sealed class Network : INetworkProvider
    {
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public NetworkReading Sample() => throw new UnauthorizedAccessException("Access denied");
        public void Reset() { }
    }
    private sealed class Desktop : IInteractiveIslandDesktop
    {
        public event Action<IslandInput>? Input;
        public IslandFrame? Frame;
        public RecoveryStatus Recovery { get; } = new(true, true, true, false, []);
        public void Tick(IslandTimer t) => Signal(new(IslandInputKind.Timer, (int)t));
        public void Signal(IslandInput i) => Input?.Invoke(i);
        public void Create() { }
        public IslandDisplay ReadDisplay() => new("display", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 144);
        public void Render(IslandFrame frame) => Frame = frame;
        public void Show(bool visible) { }
        public void SetTimer(IslandTimer timer, int? milliseconds) { }
        public NativeIslandStatus Observe() => new(true, true, true, true, false, false, 144, Frame!.Bounds, 0, 0);
        public void Close() { }
        public int Run() => 0;
        public void Dispose() { }
        public void EnableRecovery() { }
        public IReadOnlyList<IslandDisplay> ReadDisplays() => [ReadDisplay()];
        public void SelectDisplay(string id) { }
        public PointerSnapshot ReadPointer() => new(false, 0, 0, false, false);
        public DesktopSuppression ReadSuppression() => new(false, false, null);
        public void SetClickThrough(bool enabled) { }
        public void ObserveForeground(bool enabled) { }
        public int OpenMenu(IReadOnlyList<IslandMenuItem> items) => 0;
    }
}