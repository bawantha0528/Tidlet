using System.Diagnostics;
using System.Runtime.InteropServices;
using Islet.Core.Metrics;
namespace Islet.Windows.Bar;

/// <summary>Counts each physical LUID once; virtual/filter/tunnel/down rows are excluded. No network requests.</summary>
public sealed class PhysicalNetworkSource : ISystemPhysicalNetworkSource
{
    public static bool Include(uint type,byte flags,int oper)=>type is 6 or 71 && (flags&1)!=0 && (flags&2)==0 && oper==1;
    public bool TryRead(out PhysicalCounter[] counters,out string reason)
    {
        counters=[];var error=GetIfTable2(out var table);
        if(error!=0){reason=$"Windows interface query failed ({error}).";return false;}
        try
        {
            var count=Marshal.ReadInt32(table);if(count is <0 or >4096){reason="Invalid interface table.";return false;}
            var rows=new List<PhysicalCounter>();var seen=new HashSet<ulong>();
            for(int i=0;i<count;i++)
            {
                var row=Marshal.PtrToStructure<Row>(table+8+i*Marshal.SizeOf<Row>());
                if(Include(row.Type,row.Flags,row.Oper)&&seen.Add(row.Luid))rows.Add(new(row.Luid,row.InOctets,row.OutOctets,row.Alias,row.Guid,
                    row.AddressLength is>0 and<=32?string.Join(":",row.Address.Take((int)row.AddressLength).Select(x=>x.ToString("X2"))):null));
            }
            counters=rows.ToArray();reason=counters.Length>0?"Physical Ethernet/Wi-Fi adapters only.":"No connected physical Ethernet or Wi-Fi adapter.";
            return counters.Length>0;
        }
        finally{FreeMibTable(table);}
    }
    public static int NativeRowSize=>Marshal.SizeOf<Row>();
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=8)]
    private struct Row
    {
        public ulong Luid;public uint Index;public Guid Guid;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)]public string Alias;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)]public string Description;
        public uint AddressLength;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Address;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] PermanentAddress;
        public uint Mtu,Type,Tunnel,Media,PhysicalMedia,Access,Direction;public byte Flags;
        public int Oper,Admin,MediaConnect;public Guid Network;public uint Connection;
        public ulong TxSpeed,RxSpeed,InOctets,InUnicast,InNonUnicast,InDiscards,InErrors,InUnknown,InUnicastBytes,InMulticastBytes,InBroadcastBytes,
            OutOctets,OutUnicast,OutNonUnicast,OutDiscards,OutErrors,OutUnicastBytes,OutMulticastBytes,OutBroadcastBytes,OutQueue;
    }
    [DllImport("iphlpapi.dll",ExactSpelling=true)]private static extern uint GetIfTable2(out nint table);
    [DllImport("iphlpapi.dll",ExactSpelling=true)]private static extern void FreeMibTable(nint table);
}
public readonly record struct PhysicalCounter(ulong Id,ulong Received,ulong Sent,string Name,Guid Guid=default,string? Mac=null);
public interface ISystemPhysicalNetworkSource{bool TryRead(out PhysicalCounter[] counters,out string reason);}
public sealed record PhysicalNetworkSnapshot(NetworkReading Reading,PhysicalCounter[] Adapters,ulong RecordedReceived,ulong RecordedSent);
public sealed class PhysicalNetworkProvider(ISystemPhysicalNetworkSource source,Action<ulong,ulong>? recordUsage=null)
{
    private PhysicalCounter[] _previous=[];private long _timestamp;
    private readonly object _gate=new();private ulong _recordedReceived,_recordedSent;
    public int Reads{get;private set;}
    public Islet.Core.Capabilities.CapabilityProbeResult Probe()=>source.TryRead(out _,out var reason)?Islet.Core.Capabilities.CapabilityProbeResult.Supported:Islet.Core.Capabilities.CapabilityProbeResult.Unsupported("network.no-physical",reason);
    public void Reset(){lock(_gate){_previous=[];_timestamp=0;}}
    public NetworkReading Sample(){lock(_gate)return SampleUnlocked();}
    public PhysicalNetworkSnapshot Snapshot(){lock(_gate){var reading=SampleUnlocked();return new(reading,_previous,_recordedReceived,_recordedSent);}}
    private NetworkReading SampleUnlocked()
    {
        Reads++;if(!source.TryRead(out var now,out var reason)){Reset();return NetworkReading.Missing(NetworkReadingState.Unavailable,reason);}
        var time=Stopwatch.GetTimestamp();var elapsed=(time-_timestamp)/(double)Stopwatch.Frequency;
        var valid=_timestamp!=0&&elapsed>0&&now.Length==_previous.Length;double down=0,up=0;
        ulong received=0,sent=0;var matched=false;
        foreach(var row in now)
        {
            var old=_previous.FirstOrDefault(x=>x.Id==row.Id);
            if(old.Id==0||row.Received<old.Received||row.Sent<old.Sent){valid=false;continue;}
            received=Add(received,row.Received-old.Received);sent=Add(sent,row.Sent-old.Sent);matched=true;
            down+=(row.Received-old.Received)/elapsed;up+=(row.Sent-old.Sent)/elapsed;
        }
        _previous=now;_timestamp=time;
        _recordedReceived=Add(_recordedReceived,received);_recordedSent=Add(_recordedSent,sent);
        if(matched)recordUsage?.Invoke(received,sent);
        return valid?new(NetworkReadingState.Available,(ulong)Math.Max(0,down),(ulong)Math.Max(0,up),string.Join(", ",now.Select(x=>x.Name)),reason)
            :NetworkReading.Missing(NetworkReadingState.WarmingUp,"Measuring physical adapters; counter baseline reset.");
    }
    private static ulong Add(ulong a,ulong b)=>ulong.MaxValue-a<b?ulong.MaxValue:a+b;
}

