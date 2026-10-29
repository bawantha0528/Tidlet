using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Core.Settings;
using Islet.Shell.Island;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class InteractiveIslandTests
{
    [TestMethod]
    public void PointerHoverClickExpandAndEscapeRespectClickThroughAndFiniteClocks()
    {
        var platform = new Desktop();
        var cpu = new Cpu();
        double clock = 0;
        using var host = new NativeIslandHost(platform, cpu, () => clock);
        host.Start(new NativeIslandOptions());
        Assert.AreEqual(100, platform.Timers[IslandTimer.Pointer]);
        Assert.IsTrue(platform.ClickThrough);
        var bounds = platform.Frame!.Bounds;
        platform.Pointer = new(true, bounds.X + bounds.Width/2, bounds.Y + bounds.Height/2, false, false);
        platform.Tick(IslandTimer.Pointer);
        Assert.AreEqual(IslandState.Hover, host.State);
        Assert.IsFalse(platform.ClickThrough);
        clock = 250; platform.Tick(IslandTimer.Animation);
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Animation));
        platform.Signal(new(IslandInputKind.LeftClick));
        clock = 500; platform.Tick(IslandTimer.Animation);
        Assert.AreEqual(IslandState.Expanded, host.State);
        Assert.AreEqual(1000, platform.Timers[IslandTimer.Metric]);
        platform.Tick(IslandTimer.Metric);
        Assert.AreEqual(1, host.HistoryCount);
        platform.Pointer = platform.Pointer with { EscapeDown = true };
        platform.Tick(IslandTimer.Pointer);
        Assert.AreEqual(IslandState.Collapsed, host.State);
        Assert.IsTrue(platform.ClickThrough);
        clock = 750; platform.Tick(IslandTimer.Animation);
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Animation));
        host.Stop();
        Assert.AreEqual(0, platform.Timers.Count);
    }

    [TestMethod]
    public void SuppressionCancelsWorkAndUserHideDisablesForegroundReconciliation()
    {
        var platform = new Desktop();
        var cpu = new Cpu();
        using var host = new NativeIslandHost(platform, cpu);
        host.Start(new NativeIslandOptions { State = IslandState.Expanded, ExitAfterSeconds = 60 });
        platform.Tick(IslandTimer.Metric);
        var reads = cpu.Reads;
        var renders = platform.Renders;
        platform.Suppression = new(true,false,"Fullscreen");
        platform.Tick(IslandTimer.Reconcile);
        Assert.IsTrue(host.Suppressed);
        Assert.IsFalse(platform.Visible);
        Assert.IsTrue(platform.Watching);
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Metric));
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Pointer));
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Animation));
        platform.Tick(IslandTimer.Metric); platform.Tick(IslandTimer.Animation); platform.Tick(IslandTimer.Pointer);
        Assert.AreEqual(reads,cpu.Reads);
        Assert.AreEqual(renders,platform.Renders);
        host.SetHidden(true);
        Assert.IsFalse(platform.Watching);
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Reconcile));
        platform.Suppression = new(false,false,null);
        platform.Signal(new(IslandInputKind.Toggle));
        Assert.IsFalse(host.Suppressed);
        Assert.IsTrue(platform.Visible);
        Assert.AreEqual(0,host.HistoryCount);
        Assert.AreEqual(2,cpu.Resets);
        host.Stop();
        platform.Tick(IslandTimer.Metric);
        Assert.AreEqual(reads,cpu.Reads);
    }

    [TestMethod]
    public void DpiAndMonitorChangesUseCurrentPhysicalBoundsWithoutInventingSensorValues()
    {
        var platform = new Desktop();
        var cpu = new Cpu { Reading = new(CpuReadingState.Available,double.NaN,"Bad sample") };
        using var host = new NativeIslandHost(platform,cpu);
        host.Start(new NativeIslandOptions());
        platform.Tick(IslandTimer.Metric);
        Assert.AreEqual(0,host.AvailableSamples);
        Assert.AreEqual("CPU  unavailable",host.LastLabel);
        platform.Display = platform.Displays[1];
        platform.Signal(new(IslandInputKind.DpiChanged));
        Assert.AreEqual(new PixelRect(-1125,12,330,54),platform.Frame!.Bounds);
        host.Stop();
    }

    [TestMethod]
    public void RuntimeMenuCommandsSwitchStyleAndSelectedMonitorWithHotkeyConflictFallback()
    {
        var platform = new Desktop();
        using var host = new NativeIslandHost(platform,new Cpu());
        host.Start(new NativeIslandOptions());
        platform.MenuCommand = 201;
        platform.Signal(new(IslandInputKind.ContextMenu));
        Assert.AreEqual(IslandStyle.Notch,host.Style);
        platform.MenuCommand = 301;
        platform.Signal(new(IslandInputKind.ContextMenu));
        Assert.AreEqual("secondary",platform.Display.Id);
        Assert.AreEqual(0,platform.Frame!.Bounds.Y);
        Assert.IsTrue(platform.LastMenu!.Any(i => !i.Enabled && i.Label.Contains("Ctrl+Alt+I",StringComparison.Ordinal)));
        host.Stop();
    }

    [TestMethod]
    public void HistoryIsBoundedAndPowerSuppressionDoesNotPollWhileAwaitingResumeEvent()
    {
        var platform = new Desktop();
        using var host = new NativeIslandHost(platform,new Cpu());
        host.Start(new NativeIslandOptions { State = IslandState.Expanded });
        for(var index=0;index<200;index++) platform.Tick(IslandTimer.Metric);
        Assert.AreEqual(60,host.HistoryCount);
        platform.Suppression = new(false,true,"Display off");
        platform.Signal(new(IslandInputKind.PowerChanged));
        Assert.IsTrue(host.Suppressed);
        Assert.IsFalse(platform.Watching);
        Assert.IsFalse(platform.Timers.ContainsKey(IslandTimer.Reconcile));
        platform.Suppression = new(false,false,null);
        platform.Signal(new(IslandInputKind.PowerChanged));
        Assert.IsFalse(host.Suppressed);
        Assert.IsTrue(platform.Timers.ContainsKey(IslandTimer.Reconcile));
        host.Stop();
    }

    [TestMethod]
    public void LaunchArgumentsRejectInvalidDuplicatesAndRequireTimedProbeRecovery()
    {
        Assert.IsTrue(IslandLaunchArguments.TryParse(Array.Empty<string>(),out var normal,out _));
        Assert.IsFalse(normal.Minimal);
        Assert.IsTrue(IslandLaunchArguments.TryParse(["--style","Notch","--state","Expanded"],out var selected,out _));
        Assert.AreEqual(IslandStyle.Notch,selected.Style);
        Assert.AreEqual(IslandState.Expanded,selected.State);
        foreach(var invalid in new string[][] {
            ["--native-probe"],["--exit-after-seconds","0"],["--style","999"],["--unknown"],
            ["--hidden","--hidden"],["--probe-report"],["--exit-after-seconds","2","--exit-after-seconds","3"]
        }) Assert.IsFalse(IslandLaunchArguments.TryParse(invalid,out _,out _));
    }

    private sealed class Cpu : ICpuProvider
    {
        public int Reads, Resets;
        public CpuReading Reading = new(CpuReadingState.Available,25,"CPU");
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public CpuReading Sample() { Reads++;return Reading; }
        public void Reset() => Resets++;
    }

    private sealed class Desktop : IInteractiveIslandDesktop
    {
        public event Action<IslandInput>? Input;
        public Dictionary<IslandTimer,int> Timers { get; } = new();
        public IReadOnlyList<IslandDisplay> Displays { get; } = [
            new("primary",new(0,0,1920,1080),new(0,0,1920,1040),96),
            new("secondary",new(-1920,0,1920,1080),new(-1920,0,1920,1040),144)];
        public IslandDisplay Display;
        public Desktop() => Display = Displays[0];
        public RecoveryStatus Recovery { get; } = new(true,false,false,false,["Hotkey conflict"]);
        public PointerSnapshot Pointer;
        public DesktopSuppression Suppression = new(false,false,null);
        public IslandFrame? Frame;
        public int Renders,MenuCommand;
        public bool Visible,ClickThrough=true,Watching;
        public IReadOnlyList<IslandMenuItem>? LastMenu;
        public void Tick(IslandTimer timer) => Signal(new(IslandInputKind.Timer,(int)timer));
        public void Signal(IslandInput input) => Input?.Invoke(input);
        public void Create() { }
        public IslandDisplay ReadDisplay() => Display;
        public void Render(IslandFrame frame) { Renders++; Frame=frame with { History=frame.History.ToArray() }; }
        public void Show(bool visible) => Visible=visible;
        public void SetTimer(IslandTimer timer,int? milliseconds) { if(milliseconds is { } value) Timers[timer]=value; else Timers.Remove(timer); }
        public NativeIslandStatus Observe() => new(Visible,true,true,true,ClickThrough,false,Display.Dpi,Frame?.Bounds??new(),Timers.Count,Renders);
        public void Close() => Visible=false;
        public int Run() => 0;
        public void Dispose() { }
        public void EnableRecovery() { }
        public IReadOnlyList<IslandDisplay> ReadDisplays() => Displays;
        public void SelectDisplay(string id) => Display=Displays.First(d=>d.Id==id);
        public PointerSnapshot ReadPointer() => Pointer;
        public DesktopSuppression ReadSuppression() => Suppression;
        public void SetClickThrough(bool enabled) => ClickThrough=enabled;
        public void ObserveForeground(bool enabled) => Watching=enabled;
        public int OpenMenu(IReadOnlyList<IslandMenuItem> items) { LastMenu=items;return MenuCommand; }
    }
}
