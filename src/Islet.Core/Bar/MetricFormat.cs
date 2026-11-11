namespace Islet.Core.Bar;

/// <summary>Display-only units; native samples remain bytes per second.</summary>
public static class MetricFormat
{
    public static string TransferRate(double? bytesPerSecond,bool bits=false,string unavailable="Not reported")
    {
        if(bytesPerSecond is not{}value||!double.IsFinite(value)||value<0||value>=ulong.MaxValue)return unavailable;
        if(bits)
        {
            value*=8;
            return value>=1_000_000_000?$"{value/1_000_000_000:0.0} Gb/s":value>=1_000_000?$"{value/1_000_000:0.0} Mb/s":value>=1000?$"{value/1000:0.0} kb/s":$"{value:0} b/s";
        }
        return value>=1_073_741_824?$"{value/1_073_741_824:0.0} GiB/s":value>=1_048_576?$"{value/1_048_576:0.0} MiB/s":value>=1024?$"{value/1024:0.0} KiB/s":$"{value:0} B/s";
    }
}
