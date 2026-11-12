using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Islet.Core.Bar;
using Islet.Core.Capabilities;
using Islet.Core.Metrics;
using Islet.Core.Popups;
using Islet.Windows.Bar;
using Islet.Windows.Metrics;
namespace Islet.Windows.Popups;

public sealed partial class WindowsPopupSource
{
    private readonly PhysicalNetworkProvider _network;
    private readonly NativeSystemBatterySource _batteryNative=new(includeTemperature:true);
    private SystemBatteryProvider? _battery;private nint _readDisk,_writeDisk,_idleDisk,_gpuEngine;
    private GpuActivity[] _gpuAdapters=[];
    private LibreHardwareSensorReader? _sensors;
    private CapabilityProbeResult ProbeModule(BarModule module)
    {
        CapabilityProbeResult status;
        if(module==BarModule.Network)status=_network.Probe();
        else if(module==BarModule.Battery){_battery=new(_batteryNative);status=_battery.Probe();}
        else if(module==BarModule.Sensors)
        {
            _sensors??=new();
            var probed=_sensors.Read();
            status=probed.Items.Length>0
                ?CapabilityProbeResult.Supported
                :CapabilityProbeResult.NeedsHelper("sensors.helper",probed.Explanation);
        }
        else
        {
            var error=PdhOpenQueryW(null,0,out _query);
            if(error==0)
            {
                _buffer=Marshal.AllocHGlobal(131072);
                if(module==BarModule.Disk)
                {
                    error=PdhAddEnglishCounterW(_query,@"\PhysicalDisk(*)\Disk Read Bytes/sec",0,out _readDisk);
                    if(error==0)error=PdhAddEnglishCounterW(_query,@"\PhysicalDisk(*)\Disk Write Bytes/sec",0,out _writeDisk);
                    PdhAddEnglishCounterW(_query,@"\PhysicalDisk(*)\% Idle Time",0,out _idleDisk);
                }
                else
                {
                    _gpuAdapters=GpuInventory.Read();
                    error=PdhAddEnglishCounterW(_query,@"\GPU Engine(*)\Utilization Percentage",0,out _gpuEngine);
                    if(_gpuAdapters.Length==0)error=50;
                }
            }
            status=error==0?CapabilityProbeResult.Supported:CapabilityProbeResult.Unsupported("module.counters",$"Windows {module} counters or adapter identities are unavailable (0x{error:X8}). No permission changes were made.");
        }
        _capabilities[module.ToString()]=status;return status;
    }
    private PopupSample ReadModule(BarModule module)
    {
        var reason=_capabilities.TryGetValue(module.ToString(),out var capability)?capability.Explanation??"Windows reported metrics.":"Not probed.";
        var uptime=TimeSpan.FromMilliseconds(Environment.TickCount64);
        if(module==BarModule.Network)
        {
            var snapshot=_network.Snapshot();var rate=snapshot.Reading;var adapters=new List<NetworkAdapterDetails>();
            var all=NetworkInterface.GetAllNetworkInterfaces();
            foreach(var physical in snapshot.Adapters)
            {
                string? address=null,gateway=null;
                try
                {
                    var nic=all.FirstOrDefault(n=>Guid.TryParse(n.Id,out var id)&&id==physical.Guid);
                    var ip=nic?.GetIPProperties();address=ip?.UnicastAddresses.FirstOrDefault(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)?.Address.ToString();
                    gateway=ip?.GatewayAddresses.FirstOrDefault(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)?.Address.ToString();
                }
                catch(NetworkInformationException){}
                adapters.Add(new(physical.Name,physical.Mac,address,gateway));
            }
            var details=new NetworkDetails(rate.State==NetworkReadingState.Available?rate.BytesReceivedPerSec:null,rate.State==NetworkReadingState.Available?rate.BytesSentPerSec:null,
                snapshot.RecordedReceived,snapshot.RecordedSent,adapters.ToArray(),rate.Explanation+" Totals cover recorded traffic; paused gaps and counter resets are excluded. Internet/ICMP checks are off.");
            return new(null,null,null,null,null,null,uptime,null,[],[],[],details.Explanation,_capabilities,Network:details,WarmingUp:rate.State==NetworkReadingState.WarmingUp);
        }
        if(module==BarModule.Battery)
        {
            var reading=_battery?.Sample()??BatteryReading.NoBattery();
            bool? ac=_batteryNative.TryRead(out var line,out _,out _,out _,out _)?line switch{0=>false,1=>true,_=>null}:null;
            return new(null,null,null,null,null,null,uptime,null,[],[],[],reading.Explanation,_capabilities,Battery:new(reading,ac,"First battery reported by Windows · mWh",_batteryNative.TemperatureCelsius));
        }
        if(module==BarModule.Sensors)
        {
            _sensors??=new();
            var sensors=_sensors.Read();
            return new(null,null,null,null,null,null,uptime,null,[],[],[],sensors.Explanation,_capabilities,Sensors:sensors);
        }
        var collected=_query!=0&&PdhCollectQueryData(_query)==0;var ready=collected&&_primed;_primed=collected;
        if(module==BarModule.Disk)
        {
            var disks=new List<DiskActivity>();
            if(ready)
            {
                var reads=ReadArray(_readDisk);var writes=ReadArray(_writeDisk);var idle=ReadArray(_idleDisk);
                foreach(var (name,read) in reads)
                {
                    if(name=="_Total"||!ValidRate(read)||!writes.TryGetValue(name,out var write)||!ValidRate(write))continue;
                    double? active=idle.TryGetValue(name,out var value)&&value is>=0 and<=100?100-value:null;
                    disks.Add(new(name,read,write,active));
                }
            }
            var volumes=new List<VolumeCapacity>();
            foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType==DriveType.Fixed&&drive.IsReady&&drive.TotalSize>0&&drive.TotalFreeSpace>=0&&drive.TotalFreeSpace<=drive.TotalSize)volumes.Add(new(drive.Name,(ulong)drive.TotalSize,(ulong)drive.TotalFreeSpace));}catch(IOException){}catch(UnauthorizedAccessException){}
            var explanation=disks.Count>0?"Per-physical-disk throughput. Volume space is listed separately.":
                collected&&!ready?"Windows is collecting the next disk counter sample. Volume capacity is already available where reported.":
                "Windows did not provide valid disk activity readings.";
            return new(null,null,null,null,null,null,uptime,null,[],[],[],reason,_capabilities,Disk:new(disks.ToArray(),volumes.ToArray(),explanation),WarmingUp:collected&&!ready);
        }
        var usage=ready?GpuEngineMath.Usage(ReadArray(_gpuEngine)):new Dictionary<ulong,double>();
        var gpu=_gpuAdapters.Select(adapter=>
        {
            var available=usage.TryGetValue(adapter.Luid,out var percent);
            var explanation=available?"Busiest Windows engine; process samples sharing an engine are combined.":
                collected&&!ready?"Windows is collecting the next engine counter sample.":
                "Windows did not provide a valid engine sample for this adapter.";
            var temperature=_includeGpuTemperature?GpuTemperature.Read(adapter.Luid):null;
            return adapter with{Usage=available?percent:null,Explanation=explanation,TemperatureCelsius=temperature};
        }).ToArray();
        return new(null,null,null,null,null,null,uptime,null,[],[],[],reason,_capabilities,Gpu:new(gpu,gpu.Length>0?"Adapters are shown separately. Memory is dedicated hardware capacity, not current allocation.":reason),WarmingUp:collected&&!ready);
    }
    private static bool ValidRate(double value)=>double.IsFinite(value)&&value>=0&&value<ulong.MaxValue;
}
