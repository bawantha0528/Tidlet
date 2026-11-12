using System.Diagnostics;
using System.Runtime.InteropServices;
using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Core.Popups;
using Islet.Windows.Metrics;
namespace Islet.Windows.Popups;

/// <summary>Owned only by an open popup; no background task/timer is started by this source.</summary>
public sealed partial class WindowsPopupSource : IPopupSource,IPopupDetailSource
{
    private readonly SystemCpuProvider _cpu=new(new NativeSystemCpuSource());
    private nint _query,_working,_ids,_buffer;private readonly nint[] _cores;
    private readonly Dictionary<int,(ulong Created,ulong Cpu,long Timestamp)> _previous=[];
    private int[] _classes=[];private Dictionary<string,CapabilityProbeResult> _capabilities=[];private bool _primed;private string _counterReason="";private bool _disposed;
    private readonly Action<string,bool>? _phase;
    private PopupDetails _details=PopupDetails.All;private BarModule? _probedModule;
    internal bool ProcessCountersEnabled {get;}
    private readonly bool _includeGpuTemperature;
    private readonly Func<(double? Celsius, bool Automatic, bool Measuring)>? _cpuTemperature;
    public WindowsPopupSource(Islet.Windows.Bar.PhysicalNetworkProvider? network=null,bool includeGpuTemperature=false,Func<(double? Celsius, bool Automatic, bool Measuring)>? cpuTemperature=null):this(network,processCounters:true,phase:null)
    {
        _includeGpuTemperature=includeGpuTemperature;
        _cpuTemperature=cpuTemperature;
    }
    internal WindowsPopupSource(Islet.Windows.Bar.PhysicalNetworkProvider? network,bool processCounters,Action<string,bool>? phase)
    {
        ProcessCountersEnabled=processCounters;_phase=phase;
        var count=PopupNative.ProcessorCount;_cores=new nint[count is>0 and<=32?count:0];
        _network=network??new(new Islet.Windows.Bar.PhysicalNetworkSource());
    }
    public CapabilityProbeResult Probe(BarModule module)
    {
        if(module is not(BarModule.Cpu or BarModule.Ram))return ProbeModule(module);
        var cpu=module==BarModule.Cpu?_cpu.Probe():CapabilityProbeResult.Unsupported("cpu.closed","CPU popup is closed.");
        _capabilities["cpu"]=cpu;
        _capabilities["temperature"]=CapabilityProbeResult.NeedsHelper("temperature.helper","Temperature needs the optional hardware helper; none is running.");
        if(module==BarModule.Cpu)
        {
            _classes=PopupNative.Classes();var frequency=PopupNative.Frequency();
            _capabilities["frequency"]=frequency.Current.HasValue?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("frequency.unavailable","Windows did not report a valid clock frequency.");
            _capabilities["hybrid"]= _classes.Length>0&&_classes.All(x=>x>=0)&&_classes.Distinct().Count()>1?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("hybrid.unavailable","No verified mixed efficiency-class mapping; logical CPU bars are used.");
        }
        _probedModule=module;ConfigureCounters(module);
        var ram=module==BarModule.Ram&&PopupNative.Memory() is{Valid:true};if(module==BarModule.Ram)_capabilities["ram"]=ram?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("ram.unavailable","Windows memory information is unavailable.");
        return cpu.Availability==CapabilityAvailability.Supported||ram?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("popup.unavailable","Windows metrics could not be read.");
    }
    public void SetDetails(PopupDetails details)
    {
        if(_disposed)throw new ObjectDisposedException(nameof(WindowsPopupSource));
        details&=PopupDetails.All;if(_details==details)return;_details=details;
        if(_probedModule is{}module)ConfigureCounters(module);
    }
    private void CloseCounters()
    {
        if(_query!=0)PdhCloseQuery(_query);if(_buffer!=0)Marshal.FreeHGlobal(_buffer);
        _query=_buffer=_working=_ids=0;Array.Clear(_cores);_previous.Clear();_primed=false;_counterReason="";
    }
    private void ConfigureCounters(BarModule module)
    {
        // Earlier samples may still be displayed on the UI thread; do not mutate their capability map.
        _capabilities=new(_capabilities);
        CloseCounters();
        var (cores,processes)=CounterNeeds(module,_details,ProcessCountersEnabled);
        _capabilities["processes"]=CapabilityProbeResult.Unsupported("processes.inactive","Process details are outside the visible section.");
        _capabilities["cores"]=CapabilityProbeResult.Unsupported("cores.inactive","Core details are outside the visible section.");
        if(!processes&&!cores)return;
        _phase?.Invoke("query.before",false);
        var status=PdhOpenQueryW(null,0,out _query);
        _phase?.Invoke("query.after",false);
        if(status==0)
        {
            if(processes)_buffer=Marshal.AllocHGlobal(131072);
            _phase?.Invoke("process-counters.before",false);
            if(processes)
            {
                if(PdhAddEnglishCounterW(_query,@"\Process(*)\Working Set",0,out _working)!=0)_working=0;
                if(PdhAddEnglishCounterW(_query,@"\Process(*)\ID Process",0,out _ids)!=0)_ids=0;
            }
            _phase?.Invoke("process-counters.after",false);
            if(cores)for(int i=0;i<_cores.Length;i++)if(PdhAddEnglishCounterW(_query,$@"\Processor({i})\% Processor Time",0,out _cores[i])!=0)_cores[i]=0;
        }
        else _counterReason=$"Performance counters unavailable (0x{status:X8}).";
        _phase?.Invoke("all-counters.after",false);
        if(processes)_capabilities["processes"]=_working!=0&&_ids!=0?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("processes.unavailable","Windows process counters are unavailable.");
        if(cores)_capabilities["cores"]=_cores.Any(x=>x!=0)?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("cores.unavailable","Per-core counters are unavailable for this configuration.");
    }
    internal static (bool Cores,bool Processes) CounterNeeds(BarModule module,PopupDetails details,bool processCounters=true)=>
        (module==BarModule.Cpu&&details.HasFlag(PopupDetails.Cores),processCounters&&details.HasFlag(PopupDetails.Processes));
    public PopupSample Read(BarModule module)
    {
        if(_disposed)throw new ObjectDisposedException(nameof(WindowsPopupSource));
        if(module is not(BarModule.Cpu or BarModule.Ram))return ReadModule(module);
        var cpu=module==BarModule.Cpu?_cpu.Sample():CpuReading.Missing(CpuReadingState.Unavailable,"CPU popup is closed.");var memory=module==BarModule.Ram?PopupNative.Memory():null;var frequency=module==BarModule.Cpu?PopupNative.Frequency():(null,null);
        var cores=Enumerable.Repeat(double.NaN,_cores.Length).ToArray();PopupProcess[] processes=[];
        var collectionStarted=DateTime.UtcNow.ToFileTimeUtc();
        if(_query!=0&&PdhCollectQueryData(_query)==0)
        {
            if(module==BarModule.Cpu&&_primed)
                for(int i=0;i<_cores.Length;i++)if(_cores[i]!=0&&PdhGetFormattedCounterValue(_cores[i],0x200|0x1000|0x8000,out _,out var v)==0&&v.Status<=1&&double.IsFinite(v.Value)&&v.Value is>=0 and<=100)cores[i]=v.Value;
            if(_ids!=0&&_working!=0)
            {
            var ids=ReadArray(_ids);var working=ReadArray(_working);var now=Stopwatch.GetTimestamp();var result=new List<PopupProcess>();
            if(_previous.Count>8192)_previous.Clear();
            var next=new Dictionary<int,(ulong,ulong,long)>();
            foreach(var entry in ids)
            {
                if(entry.Key=="_Total"||entry.Value is<1 or>int.MaxValue||!working.TryGetValue(entry.Key,out var bytes)||!double.IsFinite(bytes)||bytes<0||bytes>=ulong.MaxValue)continue;
                var pid=(int)entry.Value;var handle=OpenProcess(0x1000,false,pid);if(handle==0)continue;
                try
                {
                    if(!GetProcessTimes(handle,out var created,out _,out var kernel,out var user)||created>(ulong)collectionStarted)continue;
                    var ticks=kernel+user;double? percent=null;
                    if(_previous.TryGetValue(pid,out var prior)&&prior.Created==created&&ticks>=prior.Cpu&&now>prior.Timestamp)
                        percent=PopupProcessMath.Cpu(prior.Created,created,prior.Cpu,ticks,(now-prior.Timestamp)/(double)Stopwatch.Frequency,PopupNative.ProcessorCount);
                    next[pid]=(created,ticks,now);
                    result.Add(new(pid,entry.Key,percent,(ulong)bytes));
                }
                finally{CloseHandle(handle);}
            }
            _previous.Clear();foreach(var item in next)_previous[item.Key]=item.Value;
            processes=(module==BarModule.Cpu?result.Where(x=>x.CpuPercent.HasValue).OrderByDescending(x=>x.CpuPercent):result.OrderByDescending(x=>x.WorkingSet)).Take(6).ToArray();
            }
            _primed=true;
        }
        var temperature=module==BarModule.Cpu?_cpuTemperature?.Invoke():null;
        return new(cpu.State==CpuReadingState.Available?cpu.Percent:null,cpu.SystemPercent,cpu.UserPercent,cpu.IdlePercent,frequency.Item1,frequency.Item2,
            TimeSpan.FromMilliseconds(Environment.TickCount64),memory is{Valid:true}?memory:null,cores,_classes,processes,
            _counterReason.Length>0?_counterReason:processes.Length==0?"Measuring processes; protected or exited processes are skipped.":"Protected or exited processes are skipped. Windows reported metrics.",_capabilities,WarmingUp:cpu.State==CpuReadingState.WarmingUp,
            LastCpuPackageCelsius:temperature?.Celsius,CpuTemperatureAutomatic:temperature?.Automatic==true,CpuTemperatureMeasuring:temperature?.Measuring==true);
    }
    private Dictionary<string,double> ReadArray(nint counter)
    {
        var result=new Dictionary<string,double>();if(counter==0||_buffer==0)return result;uint bytes=131072;
        if(PdhGetFormattedCounterArrayW(counter,0x200|0x1000|0x8000,ref bytes,out var count,_buffer)!=0||count>4096)return result;
        var stride=Marshal.SizeOf<CounterItem>();if((ulong)count*(uint)stride>bytes)return result;
        for(int i=0;i<count;i++)
        {
            var item=Marshal.PtrToStructure<CounterItem>(_buffer+i*stride);
            var address=unchecked((ulong)(nuint)item.Name);var start=unchecked((ulong)(nuint)_buffer);
            if(item.Value.Status>1||!double.IsFinite(item.Value.Value)||address<start||address>=start+bytes)continue;
            var max=(int)Math.Min(512UL,(start+bytes-address)/2);var chars=0;while(chars<max&&Marshal.ReadInt16(item.Name,chars*2)!=0)chars++;
            if(chars==max)continue;var name=Marshal.PtrToStringUni(item.Name,chars);if(name is not null)result[name]=item.Value.Value;
        }
        return result;
    }
    public void Dispose(){if(_disposed)return;_disposed=true;_sensors?.Dispose();_sensors=null;CloseCounters();}
    [StructLayout(LayoutKind.Explicit,Size=16)]internal struct CounterValue{[FieldOffset(0)]public uint Status;[FieldOffset(8)]public double Value;}
    [StructLayout(LayoutKind.Sequential)]internal struct CounterItem{public nint Name;public CounterValue Value;}
    [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]private static extern uint PdhOpenQueryW(string? source,nuint user,out nint query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]private static extern uint PdhAddEnglishCounterW(nint query,string path,nuint user,out nint counter);
    [DllImport("pdh.dll",ExactSpelling=true)]private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll",ExactSpelling=true)]private static extern uint PdhGetFormattedCounterValue(nint counter,uint format,out uint type,out CounterValue value);
    [DllImport("pdh.dll",ExactSpelling=true)]private static extern uint PdhGetFormattedCounterArrayW(nint counter,uint format,ref uint size,out uint count,nint buffer);
    [DllImport("pdh.dll",ExactSpelling=true)]private static extern uint PdhCloseQuery(nint query);
    [DllImport("kernel32.dll",ExactSpelling=true)]private static extern nint OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,int pid);
    [DllImport("kernel32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetProcessTimes(nint process,out ulong created,out ulong exited,out ulong kernel,out ulong user);
    [DllImport("kernel32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CloseHandle(nint handle);
}



