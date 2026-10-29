using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Shell.Island;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class NativeIslandHostTests
{
    [TestMethod]
    public void DemandDrawingAndNaturalExitDetachTimersAndIgnoreStaleTicks()
    {
        var desktop = new Desktop();
        var cpu = new Cpu();
        using var host = new NativeIslandHost(desktop, cpu);
        host.Start(12);
        Assert.AreEqual(1, desktop.Renders);
        Assert.AreEqual(4000, desktop.Timers[IslandTimer.Metric]);
        desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(2, desktop.Renders);
        desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(2, desktop.Renders, "Identical CPU labels must not repaint at rest.");
        Assert.AreEqual(2, host.AvailableSamples);
        desktop.Tick(IslandTimer.Exit);
        Assert.IsTrue(host.Stopped);
        Assert.AreEqual(0, desktop.Timers.Count);
        desktop.Tick(IslandTimer.Metric);
        Assert.AreEqual(2, cpu.Reads);
    }

    private sealed class Cpu : ICpuProvider
    {
        public int Reads;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public CpuReading Sample() { Reads++; return new(CpuReadingState.Available, 25, "CPU"); }
        public void Reset() { }
    }
    private sealed class Desktop : IIslandDesktop
    {
        public event Action<IslandInput>? Input;
        public Dictionary<IslandTimer,int> Timers { get; } = new();
        public int Renders;
        public bool Visible;
        public void Tick(IslandTimer timer) => Input?.Invoke(new(IslandInputKind.Timer, (int)timer));
        public void Create() { }
        public IslandDisplay ReadDisplay() => new("primary",new(0,0,1920,1080),new(0,0,1920,1040),96);
        public void Render(IslandFrame frame) => Renders++;
        public void Show(bool visible) => Visible=visible;
        public void SetTimer(IslandTimer timer,int? milliseconds) { if (milliseconds is { } value) Timers[timer]=value; else Timers.Remove(timer); }
        public NativeIslandStatus Observe() => new(Visible,true,true,true,true,false,96,new(850,8,220,36),Timers.Count,Renders);
        public void Close() => Visible=false;
        public int Run() => 0;
        public void Dispose() { }
    }
}
