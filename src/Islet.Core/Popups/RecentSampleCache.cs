using System.Diagnostics;
using Islet.Core.Bar;
using Islet.Core.Metrics;
namespace Islet.Core.Popups;

/// <summary>At most one real reading per module, for two minutes in this session only. No polling or persistence.</summary>
public sealed class RecentSampleCache
{
    private readonly Dictionary<BarModule,(double At,PopupSample Sample)> _samples=[];
    private readonly Func<double> _now;
    public RecentSampleCache(Func<double>? now=null){var clock=Stopwatch.StartNew();_now=now??(()=>clock.Elapsed.TotalSeconds);}
    public void Store(BarModule module,PopupSample sample)
    {
        if(!Enum.IsDefined(module))return;
        if(sample.WarmingUp)return; // A baseline is not a failure and must not erase a recent real reading.
        var valid=module switch
        {
            BarModule.Cpu=>Percent(sample.Cpu),BarModule.Ram=>sample.Memory?.Valid==true,
            BarModule.Network=>Rate(sample.Network?.Download)&&Rate(sample.Network?.Upload),
            BarModule.Battery=>sample.Battery?.Reading is{State:BatteryReadingState.Available} b&&Percent(b.Percent),
            BarModule.Gpu=>sample.Gpu?.Adapters.Any(a=>Percent(a.Usage))==true,
            BarModule.Disk=>sample.Disk is{}d&&(d.Disks.Any(x=>Percent(x.Active)||Rate(x.Read)||Rate(x.Write))||d.Volumes.Any(v=>v.Valid)),_=>false
        };
        var time=_now();if(!valid||!double.IsFinite(time)){_samples.Remove(module);return;}
        // Never retain process lists, local addresses, MACs, gateways or per-core detail between views.
        var net=sample.Network;
        _samples[module]=(time,sample with{Cores=[],Classes=[],Processes=[],Capabilities=null,Explanation="Previous reading · refreshing",
            Network=module!=BarModule.Network||net is null?null:new(net.Download,net.Upload,net.RecordedDownload,net.RecordedUpload,[],"Previous traffic reading · refreshing"),
            Battery=module==BarModule.Battery?sample.Battery:null,
            Gpu=module==BarModule.Gpu&&sample.Gpu is{}g?g with{Adapters=g.Adapters.Take(32).ToArray()}:null,
            Disk=module==BarModule.Disk&&sample.Disk is{}disk?disk with{Disks=disk.Disks.Take(32).ToArray(),Volumes=disk.Volumes.Take(32).ToArray()}:null});
    }
    public bool TryGet(BarModule module,out PopupSample? sample)
    {
        sample=null;if(!_samples.TryGetValue(module,out var entry))return false;
        var age=_now()-entry.At;if(!double.IsFinite(age)||age<0||age>120){_samples.Remove(module);return false;}
        sample=entry.Sample;return true;
    }
    private static bool Percent(double? value)=>value is{}v&&double.IsFinite(v)&&v is>=0 and<=100;
    private static bool Rate(double? value)=>value is{}v&&double.IsFinite(v)&&v>=0&&v<ulong.MaxValue;
}
