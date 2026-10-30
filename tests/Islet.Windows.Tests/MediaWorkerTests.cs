using System.Buffers.Binary;
using Islet.Core.Activities;
using Islet.Windows.Media;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class MediaWorkerTests
{
    [TestMethod]
    public async Task ProtocolRejectsOversizeTruncatedAndInvalidJsonWithoutUnboundedAllocation()
    {
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header,int.MaxValue);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(new MemoryStream(header)));
        BinaryPrimitives.WriteInt32LittleEndian(header,2);
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(()=>MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(new MemoryStream(header)));
        Assert.IsNull(await MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(new MemoryStream()));
        using var roundTrip = new MemoryStream(); await MediaWorkerProtocol.WriteAsync(roundTrip,new MediaWorkerRequest());
        roundTrip.Position=0; Assert.AreEqual(1,(await MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(roundTrip))!.Version);
    }
    [TestMethod]
    public async Task WorkerStartsOnlyOnDemandAndOldResponsesCannotReviveHiddenMedia()
    {
        var session = new FakeSession(); var starts=0;
        using var provider = new WorkerMediaProvider(()=>{ starts++; return session; });
        provider.Probe(); await provider.ReadAsync(); Assert.AreEqual(0,starts);
        provider.SetVisible(true); var pending=provider.ReadAsync();
        Assert.AreEqual(1,starts);
        provider.SetVisible(false);
        session.Response.SetResult(Response());
        Assert.AreNotEqual(MediaState.Available,(await pending).State);
        Assert.IsTrue(session.Closed);
        await provider.WaitForExitAsync(TimeSpan.FromSeconds(1));
        Assert.IsNotNull(provider.Workers.Single().ExitedUtc);
        await provider.ReadAsync(); Assert.AreEqual(1,starts);
    }
    [TestMethod]
    public async Task MockControlsRespectSessionAndWorkerFailureIsRecoverable()
    {
        var session=new FakeSession();session.Response.SetResult(Response());
        using var provider=new WorkerMediaProvider(()=>session);provider.SetVisible(true);
        Assert.AreEqual(MediaState.Available,(await provider.ReadAsync()).State);
        Assert.IsFalse((await provider.ExecuteAsync(MediaCommand.Next,"old")).Success);
        Assert.IsTrue((await provider.ExecuteAsync(MediaCommand.TogglePlayback,"mock")).Success);
        Assert.AreEqual(1,session.Commands);
        session.Break=true;
        Assert.AreEqual(MediaState.Unavailable,(await provider.ReadAsync()).State);
        Assert.IsTrue(session.Closed);
        provider.SetVisible(false);await provider.WaitForExitAsync(TimeSpan.FromSeconds(1));
    }
    [TestMethod]
    public async Task ReopeningWaitsForOldWorkerExitAndStartupFailureHasAReason()
    {
        var session=new FakeSession { DelayExit=true };var starts=0;
        using var provider=new WorkerMediaProvider(()=>{starts++;return session;});
        provider.SetVisible(true);var pending=provider.ReadAsync();
        provider.SetVisible(false);provider.SetVisible(true);
        Assert.AreNotEqual(MediaState.Available,(await pending).State);
        await provider.ReadAsync(); Assert.AreEqual(1,starts);
        session.Exited.SetResult(new(0.01,1000,0));provider.SetVisible(false);
        await provider.WaitForExitAsync(TimeSpan.FromSeconds(1));
        using var failing=new WorkerMediaProvider(()=>throw new IOException("Mock startup failure"));
        failing.SetVisible(true);Assert.AreEqual(MediaState.Unavailable,(await failing.ReadAsync()).State);
    }
    [TestMethod]
    public async Task ServerProcessesMockCommandsAndExitsNaturallyOnIdleOrEofDuringHungOperation()
    {
        using var encoded=new MemoryStream();await MediaWorkerProtocol.WriteAsync(encoded,new MediaWorkerRequest());
        using var input=new PendingInput(encoded.ToArray());using var output=new MemoryStream();
        var mock=new Provider();
        Assert.AreEqual(0,await MediaWorkerServer.RunAsync(input,output,mock,TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(50)));
        output.Position=0;Assert.AreEqual(MediaState.NoSession,(await MediaWorkerProtocol.ReadAsync<MediaWorkerResponse>(output))!.Reading!.State);
        using var stalled=new PendingInput(encoded.ToArray());using var ignored=new MemoryStream();
        var hung=new Provider { Hang=true };var run=MediaWorkerServer.RunAsync(stalled,ignored,hung,TimeSpan.FromSeconds(2));
        for(var i=0;i<100 && hung.Reads==0;i++)await Task.Delay(5);
        stalled.Eof.TrySetResult(0);
        Assert.AreEqual(0,await run.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.AreEqual(0,hung.Commands);
    }
    [TestMethod]
    public async Task OperationDeadlineAndMalformedRequestReturnWithoutTouchingRealPlayback()
    {
        using var encoded=new MemoryStream();await MediaWorkerProtocol.WriteAsync(encoded,new MediaWorkerRequest());
        using var input=new PendingInput(encoded.ToArray());using var output=new MemoryStream();var provider=new Provider { Hang=true };
        Assert.AreEqual(0,await MediaWorkerServer.RunAsync(input,output,provider,TimeSpan.FromMilliseconds(40)).WaitAsync(TimeSpan.FromSeconds(2)));
        using var invalid=new MemoryStream();await MediaWorkerProtocol.WriteAsync(invalid,new MediaWorkerRequest(Version:2));
        using var badInput=new PendingInput(invalid.ToArray());
        Assert.AreEqual(2,await MediaWorkerServer.RunAsync(badInput,output,new Provider()));
        Assert.AreEqual(0,provider.Commands);
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActualWorkerReadsOnlyAndExitsNaturallyOnPrivatePipeClosure(bool x86)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"TidletMonitor.sln")))directory=directory.Parent;
        Assert.IsNotNull(directory,"Solution root must be available for the native integration fixture.");
        var executable=x86 ? Path.Combine(directory.FullName,"src","IsletMonitor","bin","x86","IsletMonitor.exe")
            : Path.Combine(directory.FullName,"src","IsletMonitor","bin","Release","net8.0-windows","IsletMonitor.exe");
        Assert.IsTrue(File.Exists(executable),"Build the Release app before this fixture.");
        var session=new MediaWorkerProcessSession(executable,start =>
        {
            // Testhost can inject its own architecture-specific runtime root. Let this fixture apphost resolve its installed runtime.
            foreach(var key in start.Environment.Keys.Where(k=>k.StartsWith("DOTNET_ROOT",StringComparison.Ordinal)).ToArray()) start.Environment.Remove(key);
        });
        Exception? failure=null;
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response=await session.RequestAsync(new(),timeout.Token);
            Assert.AreEqual(1,response.Version);Assert.AreEqual(0,response.Info.Commands);
            Assert.AreEqual(0,response.Info.WpfAssemblies.Length);Assert.IsNotNull(response.Reading);
        }
        catch(Exception ex) { failure=ex; }
        finally { session.CloseInput(); }
        var exit=await session.Exit.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsNull(failure,$"Worker x86={x86}: {failure}; exit={System.Text.Json.JsonSerializer.Serialize(exit)}");
        Assert.AreEqual(0,exit.ExitCode);Assert.IsTrue(exit.PeakWorkingSetBytes>0);
        Assert.IsNotNull(exit.WpfAssemblies);Assert.AreEqual(0,exit.WpfAssemblies.Length);
    }    private static MediaWorkerResponse Response() => new(1,new(MediaState.Available,"Mock song","Mock artist","mock",CanToggle:true),null,new(123,1,0,[],[]));
    private sealed class FakeSession : IMediaWorkerSession
    {
        public int ProcessId=>123;public DateTimeOffset StartedUtc { get; }=DateTimeOffset.UtcNow;
        public TaskCompletionSource<MediaWorkerResponse> Response { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<MediaWorkerExit> Exited { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<MediaWorkerExit> Exit=>Exited.Task;
        public bool Closed,Break,DelayExit;public int Commands;
        public async Task<MediaWorkerResponse> RequestAsync(MediaWorkerRequest request,CancellationToken token)
        {
            if(Break)throw new EndOfStreamException("Mock worker failure");
            if(request.Command.HasValue){Commands++;return new(1,null,new(true,"Mock only"),new(123,1,Commands,[],[]));}
            return await Response.Task.WaitAsync(token);
        }
        public void CloseInput(){Closed=true;if(!DelayExit)Exited.TrySetResult(new(0.01,1000,0));}
    }
    private sealed class Provider : IMediaProvider
    {
        public bool Hang;public int Reads,Commands;
        public Islet.Core.Capabilities.CapabilityProbeResult Probe()=>Islet.Core.Capabilities.CapabilityProbeResult.Supported;
        public Task<MediaReading> ReadAsync(){Interlocked.Increment(ref Reads);return Hang ? new TaskCompletionSource<MediaReading>().Task : Task.FromResult(new MediaReading(MediaState.NoSession,"Not playing","Mock only"));}
        public Task<MediaCommandResult> ExecuteAsync(MediaCommand command,string token){Commands++;return Task.FromResult(new MediaCommandResult(true,"Mock only"));}
    }
    private sealed class PendingInput(byte[] data) : Stream
    {
        private int _position;
        public TaskCompletionSource<int> Eof {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(_position<data.Length){var count=Math.Min(buffer.Length,data.Length-_position);data.AsSpan(_position,count).CopyTo(buffer.Span);_position+=count;return ValueTask.FromResult(count);}
            return new(Eof.Task.WaitAsync(cancellationToken));
        }
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>_position;set=>throw new NotSupportedException();}
        protected override void Dispose(bool disposing) { Eof.TrySetResult(0);base.Dispose(disposing); }
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush(){}
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
