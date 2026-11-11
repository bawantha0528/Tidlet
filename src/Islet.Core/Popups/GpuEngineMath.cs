using System.Globalization;
namespace Islet.Core.Popups;
public static class GpuEngineMath
{
    /// <summary>Sum processes sharing an engine, then select the busiest engine per adapter.</summary>
    public static Dictionary<ulong,double> Usage(IReadOnlyDictionary<string,double> samples)
    {
        var engines=new Dictionary<(ulong Adapter,uint Physical,uint Engine),double>();
        foreach(var (name,value) in samples)
        {
            if(!double.IsFinite(value)||value<0)continue;
            var p=name.Split('_');
            if(p.Length<11||p[0]!="pid"||p[2]!="luid"||p[5]!="phys"||p[7]!="eng"||p[9]!="engtype"||!uint.TryParse(p[1],out _)
                ||!Hex(p[3],out var high)||!Hex(p[4],out var low)||!uint.TryParse(p[6],out var physical)||!uint.TryParse(p[8],out var engine))continue;
            var key=(((ulong)high<<32)|low,physical,engine);engines.TryGetValue(key,out var prior);engines[key]=Math.Min(100,prior+Math.Min(100,value));
        }
        var result=new Dictionary<ulong,double>();foreach(var (key,value) in engines){result.TryGetValue(key.Adapter,out var prior);result[key.Adapter]=Math.Max(prior,value);}return result;
    }
    private static bool Hex(string text,out uint value){value=0;return text.StartsWith("0x",StringComparison.OrdinalIgnoreCase)&&uint.TryParse(text.AsSpan(2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value);}
}
