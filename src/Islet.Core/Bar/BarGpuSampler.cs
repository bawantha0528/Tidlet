using Islet.Core.Capabilities;
using Islet.Core.Popups;
namespace Islet.Core.Bar;

public sealed record BarGpuReading(double? Percent,string Explanation,bool WarmingUp=false);

/// <summary>Owner-thread API. One worker, no timer; native source work/disposal never overlap.</summary>
public sealed class BarGpuSampler(Func<IPopupSource> factory):IDisposable
{
    private IPopupSource? _source;private CapabilityProbeResult? _probe;
    private Task<BarGpuReading>? _pending;private Task _cleanup=Task.CompletedTask;private bool _active,_disposed;
    private BarGpuReading _current=new(null,"Preparing GPU readings…",true);
    public int Requests{get;private set;}
    public BarGpuReading Sample(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!enabled){Stop();return new(null,"GPU monitoring is off.");}
        if(!_cleanup.IsCompleted)return _current;
        if(_pending?.IsCompleted==true){_current=_pending.GetAwaiter().GetResult();_pending=null;}
        _active=true;
        if(_pending is null){Requests++;_pending=Task.Run(Read);}
        return _current;
    }
    private BarGpuReading Read()
    {
        try
        {
            _source??=factory();_probe??=_source.Probe(BarModule.Gpu);
            if(_probe.Availability!=CapabilityAvailability.Supported)return new(null,_probe.Explanation??"Windows GPU readings are unavailable.");
            var sample=_source.Read(BarModule.Gpu);
            return Summarize(sample);
        }
        catch(Exception ex) when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {return new(null,"GPU readings unavailable: "+ex.Message);}
    }
    public static BarGpuReading Summarize(PopupSample sample)
    {
        if(sample.WarmingUp)return new(null,"Preparing GPU counter readings…",true);
        var adapters=sample.Gpu?.Adapters.Where(a=>a.Usage is{}v&&double.IsFinite(v)&&v is>=0 and<=100).Take(32).ToArray()??[];
        if(adapters.Length==0)return new(null,sample.Gpu?.Explanation??sample.Explanation);
        return new(adapters.Max(a=>a.Usage),"Highest reported adapter usage. "+string.Join("; ",adapters.Select(a=>$"{a.Name}: {a.Usage:0}%"))+". Open GPU details for separate adapters.");
    }
    public void Stop()
    {
        if(!_active)return;_active=false;var pending=_pending;_pending=null;
        _current=new(null,"Preparing GPU readings…",true);_cleanup=Release(pending);
    }
    private async Task Release(Task<BarGpuReading>? pending)
    {
        if(pending is not null)await pending.ConfigureAwait(false);
        try{_source?.Dispose();}
        catch(Exception ex) when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException){}
        finally{_source=null;_probe=null;}
    }
    public bool Drain(TimeSpan timeout)=>_cleanup.Wait(timeout);
    public void Dispose(){Stop();_disposed=true;}
}
