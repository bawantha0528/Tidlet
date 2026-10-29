using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Islet.Core.Capabilities;
using Islet.Core.Display;
using Islet.Core.Metrics;
using Islet.Shell.Island;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class IslandProbeWindowTests
{
    [TestMethod]
    public void VisibleLifetimeOwnsPollingAndReprobesAfterHide()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var cpu = new Cpu();
                var platform = new Platform();
                var window = new IslandProbeWindow(cpu, platform);
                Assert.IsTrue(window.AllowsTransparency);
                Assert.IsTrue(window.Topmost);
                Assert.IsFalse(window.ShowActivated);
                Assert.IsFalse(window.ShowInTaskbar);
                Assert.AreEqual(WindowStyle.None, window.WindowStyle);
                window.Show();
                PumpUntil(() => window.AvailableSamples == 1);
                Assert.IsTrue(window.Polling, $"Failure: {window.Failure}; visible: {window.IsVisible}; label: {window.LastLabel}");
                Assert.AreEqual(1, platform.Configures);
                window.Hide();
                Assert.IsFalse(window.Polling);
                var samples = window.SampleAttempts;
                PumpUntil(() => true);
                Assert.AreEqual(samples, window.SampleAttempts);
                window.Show();
                PumpUntil(() => window.AvailableSamples == 2);
                Assert.AreEqual(2, cpu.Probes);
                Assert.IsTrue(window.LastLabel.Contains("35%", StringComparison.Ordinal));
                window.Close();
                Assert.IsFalse(window.Polling);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "STA lifecycle check timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void MissingNativeApiReportsFailureWithoutStartingPolling()
    {
        OnSta(() =>
        {
            var window = new IslandProbeWindow(new Cpu(), new MissingApi());
            var failures = 0;
            window.ProbeFailed += (_, _) => failures++;
            try
            {
                window.Show();
                Assert.AreEqual(1, failures);
                Assert.AreEqual("Fixture missing DPI API", window.Failure);
                Assert.IsFalse(window.Polling);
                Assert.AreEqual(0, window.SampleAttempts);
            }
            finally { window.Close(); }
        });
    }

    [TestMethod]
    public void InFlightReadIsNotPublishedAcrossHideAndResume()
    {
        OnSta(() =>
        {
            using var cpu = new BlockingCpu();
            var window = new IslandProbeWindow(cpu, new Platform());
            try
            {
                window.Show();
                PumpUntil(() => cpu.Entered.IsSet);
                window.Hide();
                Assert.IsFalse(window.Polling);
                window.Show();
                cpu.Release.Set();
                PumpUntil(() => window.AvailableSamples == 1);
                Assert.AreEqual(2, cpu.Reads);
                Assert.AreEqual(2, cpu.Probes);
                Assert.AreEqual(1, window.SampleAttempts);
                Assert.IsTrue(window.LastLabel.Contains("65%", StringComparison.Ordinal));
            }
            finally { cpu.Release.Set(); window.Close(); }
        });
    }

    private static void OnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { test(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "STA condition timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class MissingApi : IIslandProbePlatform
    {
        public void Configure(nint window, double widthDip, double heightDip, double gapDip)
            => throw new EntryPointNotFoundException("Fixture missing DPI API");
        public IslandProbeObservation Observe(nint window) => throw new AssertFailedException("Should not observe after failure.");
    }

    private sealed class BlockingCpu : ICpuProvider, IDisposable
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public int Reads;
        public int Probes;
        public CapabilityProbeResult Probe() { Interlocked.Increment(ref Probes); return CapabilityProbeResult.Supported; }
        public CpuReading Sample()
        {
            var read = Interlocked.Increment(ref Reads);
            if (read == 1)
            {
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new InvalidOperationException("Fixture wait timed out.");
            }
            return new(CpuReadingState.Available, read == 1 ? 35 : 65, "Fixture CPU usage");
        }
        public void Reset() { }
        public void Dispose() { Entered.Dispose(); Release.Dispose(); }
    }
    private static void PumpUntil(Func<bool> predicate)
    {
        var frame = new DispatcherFrame();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => { if (predicate() || clock.Elapsed.TotalSeconds > 5) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.IsTrue(predicate(), "Dispatcher condition timed out.");
    }

    private sealed class Cpu : ICpuProvider
    {
        public int Probes { get; private set; }
        public CapabilityProbeResult Probe() { Probes++; return CapabilityProbeResult.Supported; }
        public CpuReading Sample() => new(CpuReadingState.Available, 35, "Fixture CPU usage");
        public void Reset() { }
    }

    private sealed class Platform : IIslandProbePlatform
    {
        public int Configures { get; private set; }
        public void Configure(nint window, double widthDip, double heightDip, double gapDip) => Configures++;
        public IslandProbeObservation Observe(nint window) => new(true, true, true, false, 96, 850, 8, 220, 36);
    }
}