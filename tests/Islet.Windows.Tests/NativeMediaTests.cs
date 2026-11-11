using Islet.Core.Activities;
using Islet.Windows.Media;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class NativeMediaTests
{
    [TestMethod]
    public async Task NativeActorIsLazySingleFlightAndReleasesOnItsOwningThread()
    {
        var backend = new FakeSession(); var starts = 0;
        using var provider = new NativeMediaProvider(() => { Interlocked.Increment(ref starts); return backend; });
        await provider.ReadAsync(); Assert.AreEqual(0, starts);
        provider.SetVisible(true);
        Assert.AreEqual(MediaState.Available, (await provider.ReadAsync()).State);
        Assert.AreEqual(1, starts);
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.Next, "old")).Success);
        Assert.IsTrue((await provider.ExecuteAsync(MediaCommand.TogglePlayback, "current")).Success);
        Assert.AreEqual(1, backend.Commands);
        provider.SetVisible(false);
        await provider.WaitForExitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(0, provider.ActiveThreads); Assert.IsTrue(backend.Disposed);
        Assert.AreEqual(backend.ReadThread, backend.DisposeThread);
        Assert.AreEqual(0, provider.WorkerStarts);
    }
    [TestMethod]
    public async Task HidingCancelsPendingNativeWorkAndCannotPublishLateReading()
    {
        using var entered = new ManualResetEventSlim();
        var backend = new FakeSession { Entered = entered };
        using var provider = new NativeMediaProvider(() => backend);
        provider.SetVisible(true); var read = provider.ReadAsync();
        Assert.IsTrue(entered.Wait(3000));
        for (var i=0; i<10; i++) Assert.AreEqual(MediaState.Waiting, (await provider.ReadAsync()).State);
        provider.SetVisible(false);
        Assert.AreEqual(MediaState.Waiting, (await read).State);
        await provider.WaitForExitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(0,provider.ActiveThreads); Assert.IsTrue(backend.Disposed);
    }
    [TestMethod]
    public async Task DeniedActivationCompletesCallerAndCanRecoverOnNextDemand()
    {
        var starts=0;
        using var provider = new NativeMediaProvider(() => { if (Interlocked.Increment(ref starts)==1) throw new UnauthorizedAccessException(); return new FakeSession(); });
        provider.SetVisible(true);
        var denied=await provider.ReadAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(MediaState.Unavailable,denied.State); StringAssert.Contains(denied.Subtitle,"denied");
        Assert.AreEqual(MediaState.Available,(await provider.ReadAsync().WaitAsync(TimeSpan.FromSeconds(3))).State);
        provider.SetVisible(false); await provider.WaitForExitAsync(TimeSpan.FromSeconds(3));
    }
    [TestMethod]
    public void CommandPolicyUsesSdkSlotsAndRejectsDisabledAndUnknownActions()
    {
        Assert.AreEqual(0,NativeMediaCommandPolicy.Slot((MediaCommand)99,true,true,true,true,true,true));
        Assert.AreEqual(0,NativeMediaCommandPolicy.Slot(MediaCommand.Next,false,false,false,false,false,false));
        Assert.AreEqual(17,NativeMediaCommandPolicy.Slot(MediaCommand.Previous,false,false,false,false,true,false));
        Assert.AreEqual(16,NativeMediaCommandPolicy.Slot(MediaCommand.Next,false,false,false,false,false,true));
        Assert.AreEqual(20,NativeMediaCommandPolicy.Slot(MediaCommand.TogglePlayback,true,false,false,true,false,false));
        Assert.AreEqual(11,NativeMediaCommandPolicy.Slot(MediaCommand.TogglePlayback,true,false,true,false,false,false));
        Assert.AreEqual(10,NativeMediaCommandPolicy.Slot(MediaCommand.TogglePlayback,false,true,false,false,false,false));
    }
    [TestMethod]
    public async Task ReadOnlyNativeProbeAndRepeatedTeardownLoadNoProjectionOrWorker()
    {
        // No playback commands; this test reads only the user's current OS media state.
        var projectionBefore = AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime");
        using var provider = new NativeMediaProvider();
        for(var i=0;i<3;i++)
        {
            provider.SetVisible(true);
            var reading=await provider.ReadAsync().WaitAsync(TimeSpan.FromSeconds(12));
            Assert.AreNotEqual(MediaState.Waiting,reading.State);
            Assert.AreNotEqual(MediaState.Unavailable,reading.State,reading.Subtitle);
            provider.SetVisible(false); await provider.WaitForExitAsync(TimeSpan.FromSeconds(6));
            Assert.AreEqual(0,provider.ActiveThreads);
        }
        Assert.AreEqual(3,provider.ThreadStarts); Assert.AreEqual(0,provider.WorkerStarts);
        // Other test classes deliberately exercise the old projection worker in a separate process.
        Assert.AreEqual(projectionBefore, AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.GetName().Name is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime"));
    }
    private sealed class FakeSession : INativeMediaSession
    {
        public int ReadThread, DisposeThread, Commands; public bool Disposed; public ManualResetEventSlim? Entered;
        public MediaReading Read(CancellationToken cancel)
        {
            ReadThread=Environment.CurrentManagedThreadId;
            if(Entered is not null) { Entered.Set(); cancel.WaitHandle.WaitOne(5000); cancel.ThrowIfCancellationRequested(); }
            return new(MediaState.Available,"Mock media","Mock player","current",CanToggle:true);
        }
        public MediaCommandResult Execute(MediaCommand command,string token,CancellationToken cancel)
        { if(token!="current" || command!=MediaCommand.TogglePlayback)return new(false,"Stale or disabled mock"); Commands++;return new(true,"Mock only"); }
        public void Dispose() { Disposed=true; DisposeThread=Environment.CurrentManagedThreadId; }
    }
}