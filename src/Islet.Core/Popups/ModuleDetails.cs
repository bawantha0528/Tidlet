using Islet.Core.Metrics;
namespace Islet.Core.Popups;

public sealed record NetworkAdapterDetails(string Name,string? Mac,string? Address,string? Gateway);
public sealed record NetworkDetails(double? Download,double? Upload,ulong RecordedDownload,ulong RecordedUpload,
    NetworkAdapterDetails[] Adapters,string Explanation,double? Latency=null,double? Jitter=null);
public sealed record BatteryDetails(BatteryReading Reading,bool? OnAc,string CapacityScope,double? TemperatureCelsius=null);
public sealed record DiskActivity(string Name,double? Read,double? Write,double? Active);
public sealed record VolumeCapacity(string Name,ulong Total,ulong Free)
{public bool Valid=>Total>0&&Free<=Total;public double UsedPercent=>Valid?100.0*(Total-Free)/Total:double.NaN;}
public sealed record DiskDetails(DiskActivity[] Disks,VolumeCapacity[] Volumes,string Explanation);
public sealed record GpuActivity(ulong Luid,string Name,double? Usage,ulong DedicatedMemory,string Explanation,double? TemperatureCelsius=null);
public sealed record GpuDetails(GpuActivity[] Adapters,string Explanation);

/// <summary>Fixed two-series history. Invalid values and non-monotonic times cannot poison scales.</summary>
public sealed class TransferHistory(int capacity=121)
{
    private readonly double[] _times=new double[capacity],_down=new double[capacity],_up=new double[capacity];
    private int _next,_count;public int Count=>_count;
    public void Push(double seconds,double down,double up)
    {
        if(!double.IsFinite(seconds)||!double.IsFinite(down)||!double.IsFinite(up)||down<0||up<0||(_count>0&&seconds<=At(_count-1).Seconds))return;
        _times[_next]=seconds;_down[_next]=down;_up[_next]=up;_next=(_next+1)%capacity;_count=Math.Min(capacity,_count+1);
    }
    public (double Seconds,double Down,double Up) At(int index)
    {if(index<0||index>=_count)throw new ArgumentOutOfRangeException(nameof(index));var i=(_next-_count+capacity+index)%capacity;return(_times[i],_down[i],_up[i]);}
    public double Scale(double now){double max=1024;for(int i=0;i<_count;i++){var p=At(i);if(now-p.Seconds<=120)max=Math.Max(max,Math.Max(p.Down,p.Up));}return max;}
}
