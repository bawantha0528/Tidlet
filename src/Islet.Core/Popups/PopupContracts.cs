using Islet.Core.Bar;
using Islet.Core.Capabilities;
namespace Islet.Core.Popups;
public sealed record PopupMemory(ulong Total,ulong Available,ulong? Committed,ulong? CommitLimit,ulong? Cache,ulong? PageFileUsed,ulong? PageFileTotal)
{
    public bool Valid=>Total>0&&Available<=Total;
    public bool CommitValid=>Committed.HasValue&&CommitLimit>0&&Committed<=CommitLimit;
    public double UsedPercent=>Valid?100.0*(Total-Available)/Total:double.NaN;
    public double Pressure=>Valid?Math.Max(UsedPercent,CommitValid?100.0*Committed!.Value/CommitLimit!.Value:0):double.NaN;
    public string PressureName=>!Valid?"Unavailable":Pressure>=95?"Critical":Pressure>=80?"Warning":"Normal";
}
public enum SensorKind { Temperature, Fan, Clock, Power, Voltage, Control }
public readonly record struct SensorItem(string Hardware,string Group,string Name,SensorKind Kind,double Value,string Unit);
public sealed record SensorDetails(double? CpuPackageCelsius,double? MaxTemperatureCelsius,int? MaxFanRpm,SensorItem[] Items,bool IsElevated,string Explanation);
public readonly record struct PopupProcess(int Id,string Name,double? CpuPercent,ulong WorkingSet);
public sealed record PopupSample(double? Cpu,double? System,double? User,double? Idle,double? FrequencyGhz,double? MaxGhz,
    TimeSpan Uptime,PopupMemory? Memory,double[] Cores,int[] Classes,PopupProcess[] Processes,string Explanation,
    IReadOnlyDictionary<string,CapabilityProbeResult>? Capabilities=null,NetworkDetails? Network=null,
    BatteryDetails? Battery=null,DiskDetails? Disk=null,GpuDetails? Gpu=null,bool WarmingUp=false,SensorDetails? Sensors=null,double? LastCpuPackageCelsius=null,bool CpuTemperatureAutomatic=false,bool CpuTemperatureMeasuring=false,DateTimeOffset? CpuTemperatureSampledAt=null);
public interface IPopupSource:IDisposable
{
    CapabilityProbeResult Probe(BarModule module);
    PopupSample Read(BarModule module);
}
[Flags]
public enum PopupDetails { None=0,Cores=1,Processes=2,All=Cores|Processes }
/// <summary>Set on the same serialized worker as Probe/Read/Dispose, before probing or reading.</summary>
public interface IPopupDetailSource { void SetDetails(PopupDetails details); }
/// <summary>Fixed monotonic sample ring. Push/Average allocate no objects; WPF drawing is a separate path.</summary>
public sealed class SampleRing(int capacity)
{
    private readonly double[] _times=new double[capacity],_values=new double[capacity];private int _next,_count;
    public int Count=>_count;
    public void Push(double seconds,double value)
    {
        if(!double.IsFinite(seconds)||!double.IsFinite(value)||value is<0 or>100)return;
        if(_count>0&&seconds<=At(_count-1).Seconds)return;
        _times[_next]=seconds;_values[_next]=value;_next=(_next+1)%capacity;_count=Math.Min(capacity,_count+1);
    }
    public (double Seconds,double Value) At(int chronologicalIndex)
    {
        if(chronologicalIndex<0||chronologicalIndex>=_count)throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
        var i=(_next-_count+capacity+chronologicalIndex)%capacity;return(_times[i],_values[i]);
    }
    public (double? Percent,bool Complete) Average(double now,double window)
    {
        double total=0;int count=0;for(int i=0;i<_count;i++){var s=At(i);if(s.Seconds>=now-window&&s.Seconds<=now){total+=s.Value;count++;}}
        return(count>0?total/count:null,_count>0&&At(0).Seconds<=now-window+1.5);
    }
}

