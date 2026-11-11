using System.IO;
using Islet.Core.Metrics;
namespace Islet.Shell.Bar;
public readonly record struct BarValue(string Value,string Explanation);
public static class BarText
{
    public static BarValue Cpu(CpuReading reading)=>reading.State==CpuReadingState.Available&&reading.Percent is{} v&&double.IsFinite(v)&&v is>=0 and<=100
        ?new($"{v:0}%",reading.Explanation):new("—",reading.Explanation);
    public static BarValue Ram(RamReading reading)=>reading.State==RamReadingState.Available&&reading.TotalBytes>0&&reading.AvailableBytes<=reading.TotalBytes&&reading.UsedPercent is{} v&&double.IsFinite(v)&&v is>=0 and<=100
        ?new($"{v:0}%",reading.Explanation):new("—",reading.Explanation);
}
public sealed record BarProbeOptions(bool Transparent=true,int? ExitSeconds=null,string? Report=null,string? Capture=null,bool SoftwareRendering=false,bool VisualHost=false)
{
    public static bool TryParse(string[] args,out BarProbeOptions options)
    {
        options=new();var seen=new HashSet<string>(StringComparer.Ordinal);
        for(var i=0;i<args.Length;i++)
        {
            if(!seen.Add(args[i]))return false;
            switch(args[i])
            {
                case "--r0-probe":break;
                case "--visual-host":options=options with{VisualHost=true};break;
                case "--software-rendering":options=options with{SoftwareRendering=true};break;
                case "--solid":options=options with{Transparent=false};break;
                case "--exit-after-seconds":
                    if(++i>=args.Length||!int.TryParse(args[i],out var seconds)||seconds is<1 or>3600)return false;
                    options=options with{ExitSeconds=seconds};break;
                case "--probe-report":if(++i>=args.Length||string.IsNullOrWhiteSpace(args[i]))return false;options=options with{Report=Path.GetFullPath(args[i])};break;
                case "--capture":if(++i>=args.Length||string.IsNullOrWhiteSpace(args[i]))return false;options=options with{Capture=Path.GetFullPath(args[i])};break;
                default:return false;
            }
        }
        if((options.SoftwareRendering || options.VisualHost) && (!seen.Contains("--r0-probe") || options.Transparent || !options.ExitSeconds.HasValue || options.Report is null))return false;
        return !seen.Contains("--r0-probe")||options.ExitSeconds.HasValue&&options.Report is not null;
    }
}
