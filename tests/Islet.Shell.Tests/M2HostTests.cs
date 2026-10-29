using Microsoft.VisualStudio.TestTools.UnitTesting;
using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Shell.Island;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class M2HostTests
{
    private sealed class FakeCpu : ICpuProvider
    {
        public int Reads;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public CpuReading Sample() { Reads++; return new(CpuReadingState.Available, 30.0, "CPU OK"); }
        public void Reset() { }
    }

    private sealed class FakeRam : IRamProvider
    {
        public int Reads;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public RamReading Sample() { Reads++; return new(RamReadingState.Available, 16000, 8000, 8000, 50.0, "50% RAM"); }
        public void Reset() { }
    }

    private sealed class FakeNet : INetworkProvider
    {
        public int Reads;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public NetworkReading Sample() { Reads++; return new(NetworkReadingState.Available, 1024 * 1024, 512 * 1024, "Wi-Fi", "Net OK"); }
        public void Reset() { }
    }

    private sealed class FakeBattery : IBatteryProvider
    {
        public int Reads;
        public CapabilityProbeResult Probe() => CapabilityProbeResult.Supported;
        public BatteryReading Sample() { Reads++; return new(BatteryReadingState.Available, 80.0, BatteryChargingStatus.Discharging, TimeSpan.FromHours(3), null, null, "80% Bat"); }
        public void Reset() { }
    }

    private sealed class MockDesktop : IIslandDesktop
    {
        public event Action<IslandInput>? Input;
        public Dictionary<IslandTimer, int> Timers { get; } = new();
        public IslandFrame? LastRenderedFrame;
        public int Renders;
        public bool Visible;

        public void Tick(IslandTimer timer) => Input?.Invoke(new(IslandInputKind.Timer, (int)timer));
        public void Create() { }
        public IslandDisplay ReadDisplay() => new("display1", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 96);
        public void Render(IslandFrame frame) { LastRenderedFrame = frame; Renders++; }
        public void Show(bool visible) => Visible = visible;
        public void SetTimer(IslandTimer timer, int? ms) { if (ms is { } val) Timers[timer] = val; else Timers.Remove(timer); }
        public NativeIslandStatus Observe() => new(Visible, true, true, true, true, false, 96, new(800, 8, 220, 36), Timers.Count, Renders);
        public void Close() => Visible = false;
        public int Run() => 0;
        public void Dispose() { }
    }

    [TestMethod]
    public void CollapsedMode_SamplesCpuOnly_DoesNotSampleRamOrNetwork()
    {
        var desktop = new MockDesktop();
        var cpu = new FakeCpu();
        var ram = new FakeRam();
        var net = new FakeNet();
        var bat = new FakeBattery();

        using var host = new NativeIslandHost(desktop, cpu, ram, net, bat);
        host.Start(new NativeIslandOptions { Minimal = true, State = IslandState.Collapsed, ExitAfterSeconds = 10 });

        desktop.Tick(IslandTimer.Metric);

        Assert.AreEqual(1, cpu.Reads);
        Assert.AreEqual(0, ram.Reads, "RAM must not be polled when collapsed.");
        Assert.AreEqual(0, net.Reads, "Network must not be polled when collapsed.");
        Assert.AreEqual(0, bat.Reads, "Battery must not be polled when collapsed.");
        Assert.IsNull(desktop.LastRenderedFrame?.SecondaryLabel);
    }

    [TestMethod]
    public void ExpandedMode_SamplesAllSubscribedMetrics_PopulatesSecondaryLabel()
    {
        var desktop = new MockDesktop();
        var cpu = new FakeCpu();
        var ram = new FakeRam();
        var net = new FakeNet();
        var bat = new FakeBattery();

        using var host = new NativeIslandHost(desktop, cpu, ram, net, bat);
        host.Start(new NativeIslandOptions { Minimal = true, State = IslandState.Expanded, ExitAfterSeconds = 10 });

        desktop.Tick(IslandTimer.Metric);

        Assert.AreEqual(1, cpu.Reads);
        Assert.AreEqual(1, ram.Reads);
        Assert.AreEqual(1, net.Reads);
        Assert.AreEqual(1, bat.Reads);

        var secondary = desktop.LastRenderedFrame?.SecondaryLabel;
        Assert.IsNotNull(secondary);
        StringAssert.Contains(secondary, "RAM: 50%");
        StringAssert.Contains(secondary, "Net: ↓ 1.0 MB/s");
        StringAssert.Contains(secondary, "Bat: 80%");
    }
}
