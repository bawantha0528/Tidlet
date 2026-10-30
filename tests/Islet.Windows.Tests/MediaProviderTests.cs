using Islet.Core.Activities;
using Islet.Windows.Media;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class MediaProviderTests
{
    [TestMethod]
    public async Task BackendIsLazyAndDenialIsAnExplicitState()
    {
        var creations=0; var backend=new Backend { Denied=true };
        var provider=new SystemMediaProvider(()=>{creations++;return backend;});
        provider.Probe(); Assert.AreEqual(0,creations);
        var reading=await provider.ReadAsync();
        Assert.AreEqual(MediaState.Unavailable,reading.State);
        Assert.IsFalse(reading.CanToggle); Assert.IsTrue(reading.Subtitle.Contains("denied"));
    }
    [TestMethod]
    public async Task ControlsOnlyReachMockExecutorForCurrentSupportedSession()
    {
        var backend=new Backend(); var provider=new SystemMediaProvider(()=>backend);
        await provider.ReadAsync();
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.Next,"one")).Success);
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"old")).Success);
        Assert.AreEqual(0,backend.Commands);
        Assert.IsTrue((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"one")).Success);
        Assert.AreEqual(1,backend.Commands);
        backend.Token="two"; await provider.ReadAsync();
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"one")).Success);
        backend.Denied=true;
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"two")).Success);
    }
    [TestMethod]
    public async Task SlowPlayerCannotAccumulateConcurrentRequestsOrCommands()
    {
        var pending=new TaskCompletionSource<MediaReading>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend=new Backend { Pending=pending.Task }; var provider=new SystemMediaProvider(()=>backend);
        var first=provider.ReadAsync();
        for(var i=0;i<20;i++) await provider.ReadAsync();
        Assert.AreEqual(1,backend.Reads);
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"one")).Success);
        pending.SetResult(new(MediaState.NoSession,"Not playing","No player"));
        Assert.AreEqual(MediaState.NoSession,(await first).State);
    }
    private sealed class Backend : IMediaSessionBackend
    {
        public int Commands,Reads; public bool Denied; public string Token="one"; public Task<MediaReading>? Pending;
        public Task<MediaReading> ReadAsync()
        {
            Reads++; if(Denied) throw new UnauthorizedAccessException();
            return Pending ?? Task.FromResult(new MediaReading(MediaState.Available,"Mock title","Mock artist",Token,CanToggle:true));
        }
        public Task<MediaCommandResult> ExecuteAsync(MediaCommand command,string token)
        { if(Denied)throw new UnauthorizedAccessException(); Commands++;return Task.FromResult(new MediaCommandResult(true,"Mock only")); }
    }
}
