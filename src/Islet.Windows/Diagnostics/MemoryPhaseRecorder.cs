using System.Diagnostics;
using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Popups;
namespace Islet.Windows.Diagnostics;

public sealed record CollectionSnapshot(long Index,int Generation,long HeapSizeBytes,long FragmentedBytes,long CommittedBytes);
public sealed record MemoryPhase(string Name,double Seconds,int ThreadId,long? WorkingSetBytes,long? PeakWorkingSetBytes,long? PrivateBytes,
    int Gen0,int Gen1,int Gen2,long ApproximateAllocatedBytes,CollectionSnapshot? LastCollection,string[]? ModuleBasenames,string? ReadError);

/// <summary>Opt-in bounded diagnostic snapshots. Does not collect, trim, lock pages or change GC policy.</summary>
public sealed class MemoryPhaseRecorder
{
    private readonly object _gate=new();private readonly Stopwatch _clock=Stopwatch.StartNew();private readonly List<MemoryPhase> _entries=[];private readonly int _capacity;
    public MemoryPhaseRecorder(int capacity=512){if(capacity is<1 or>512)throw new ArgumentOutOfRangeException(nameof(capacity));_capacity=capacity;}
    public MemoryPhase[] Snapshot(){lock(_gate)return _entries.ToArray();}
    public void Record(string name,bool modules=false)
    {
        lock(_gate)
        {
            if(_entries.Count>=_capacity)return;
            var seconds=_clock.Elapsed.TotalSeconds;long? working=null,peak=null,privateBytes=null;string[]? names=null;string? error=null;
            try
            {
                using var process=Process.GetCurrentProcess();process.Refresh();working=process.WorkingSet64;peak=process.PeakWorkingSet64;privateBytes=process.PrivateMemorySize64;
                if(modules)names=process.Modules.Cast<ProcessModule>().Select(m=>Path.GetFileName(m.ModuleName)??"").Where(n=>n.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(128).ToArray();
            }
            catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){error=ex.GetType().Name;}
            var info=GC.GetGCMemoryInfo();CollectionSnapshot? collection=info.Index>0?new(info.Index,info.Generation,info.HeapSizeBytes,info.FragmentedBytes,info.TotalCommittedBytes):null;
            _entries.Add(new(name,seconds,Environment.CurrentManagedThreadId,working,peak,privateBytes,GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),GC.GetTotalAllocatedBytes(false),collection,names,error));
        }
    }
}

/// <summary>Records only the first sample/probe of a source; preserves its original call/dispose behavior.</summary>
public sealed class ProfilingPopupSource(IPopupSource source,Action<string,bool> phase):IPopupSource,IPopupDetailSource
{
    private bool _first=true;
    public void SetDetails(PopupDetails details){if(source is IPopupDetailSource detail)detail.SetDetails(details);}
    public CapabilityProbeResult Probe(BarModule module){phase("provider."+module+".probe.before",false);var result=source.Probe(module);phase("provider."+module+".probe.after",false);return result;}
    public PopupSample Read(BarModule module)
    {if(!_first)return source.Read(module);_first=false;phase("provider."+module+".first-sample.before",false);var result=source.Read(module);phase("provider."+module+".first-sample.after",false);return result;}
    public void Dispose()=>source.Dispose();
}
