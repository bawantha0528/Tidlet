using System.Runtime.InteropServices;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Windows.Metrics;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class PdhDiskTests
{
    [TestMethod]
    public void CounterAbiAndRateValidationRejectGarbage()
    {
        Assert.AreEqual(16, Marshal.SizeOf<NativeDiskRateQuery.CounterValue>());
        Assert.AreEqual(new nint(8), Marshal.OffsetOf<NativeDiskRateQuery.CounterValue>("Value"));
        foreach (var rate in new[] { double.NaN, double.PositiveInfinity, -1, (double)ulong.MaxValue }) Assert.IsFalse(NativeDiskRateQuery.ValidRate(rate));
        Assert.IsTrue(NativeDiskRateQuery.ValidRate(0));
        Assert.IsTrue(NativeDiskRateQuery.ValidRate(1048576));
    }
    [TestMethod]
    public void FakeNativeSourcesNoLongerPublishRepresentativeOrUnqueriedValues()
    {
        Assert.IsFalse(new NativeSystemDiskSource().TryRead(out _, out _, out _, out _));
        Assert.IsFalse(new NativeSystemGpuSource().TryRead(out _, out _, out _, out _, out _));
    }
    [TestMethod]
    public void OnePendingRequestNeverBlocksUiAndOldGenerationCannotSurviveReset()
    {
        using var release = new ManualResetEventSlim();
        var query = new Query(release);
        using var provider = new PdhDiskProvider(query);
        Assert.AreEqual(CapabilityAvailability.Supported, provider.Probe().Availability);
        Assert.AreEqual(DiskReadingState.WarmingUp, provider.Sample().State);
        for (var i = 0; i < 20; i++) provider.Sample();
        Assert.AreEqual(1, provider.Requests);
        provider.Reset(); release.Set();
        var until = DateTime.UtcNow.AddSeconds(4);
        while (provider.Requests < 2 && DateTime.UtcNow < until) { Thread.Sleep(5); provider.Sample(); }
        Assert.AreEqual(2, provider.Requests);
        Assert.AreEqual(DiskReadingState.WarmingUp, provider.Sample().State);
    }
    private sealed class Query(ManualResetEventSlim release) : IDiskRateQuery
    {
        public CapabilityProbeResult Open() => CapabilityProbeResult.Supported;
        public DiskReading Collect(bool prime)
        {
            release.Wait(TimeSpan.FromSeconds(4));
            return prime ? DiskReading.Missing(DiskReadingState.WarmingUp, "Priming") : new(DiskReadingState.Available, 100, 50, null, "Fixture");
        }
        public void Dispose() { }
    }
}