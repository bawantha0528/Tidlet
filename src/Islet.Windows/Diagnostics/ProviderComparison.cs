using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Islet.Core.Bar;
using Islet.Windows.Popups;
namespace Islet.Windows.Diagnostics;

/// <summary>Explicit bounded provider attribution; never a normal application mode or acceptance run.</summary>
public static class ProviderComparison
{
    internal static bool TryParse(string[] args,out bool processCounters,out string report)
    {
        processCounters=true;report="";
        if(args.Length!=3||args[0]!="--provider-comparison"||args[1] is not("with" or "without")||!Path.IsPathFullyQualified(args[2])||!Directory.Exists(Path.GetDirectoryName(args[2]))||File.Exists(args[2]))return false;
        processCounters=args[1]=="with";report=args[2];return true;
    }
    public static int Run(string[] args)
    {
        if(!TryParse(args,out var processes,out var report))return 2;
        var recorder=new MemoryPhaseRecorder();recorder.Record("diagnostic.start",true);
        Thread.Sleep(2000);var reads=0;var validCpu=0;var validCoreReads=0;var processRows=0;string? failure=null;
        using(var source=new WindowsPopupSource(null,processes,recorder.Record))
        {
            try
            {
                recorder.Record("probe.before");source.Probe(BarModule.Cpu);recorder.Record("probe.after",true);
                var clock=Stopwatch.StartNew();
                for(var i=0;i<30;i++)
                {
                    if(i==0)recorder.Record("first-read.before");
                    var sample=source.Read(BarModule.Cpu);reads++;
                    if(sample.Cpu.HasValue)validCpu++;
                    if(sample.Cores.Any(double.IsFinite))validCoreReads++;
                    processRows+=sample.Processes.Length;
                    if(i==0)recorder.Record("first-read.after");
                    var remaining=TimeSpan.FromSeconds(i+1)-clock.Elapsed;if(remaining>TimeSpan.Zero)Thread.Sleep(remaining);
                }
                recorder.Record("sampling.complete");
            }
            catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or ExternalException){failure=ex.GetType().Name;}
            recorder.Record("dispose.before");
        }
        recorder.Record("dispose.after",true);
        var assemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).ToArray();
        var wpf=assemblies.Any(n=>n is "PresentationCore" or "PresentationFramework" or "WindowsBase");
        var code=failure is null&&reads==30&&validCpu>0&&validCoreReads>0&&!wpf?0:1;
        File.WriteAllText(report,JsonSerializer.Serialize(new{AttributionOnly=true,OverallAccepted=false,ProcessCounters=processes,
            ProcessArchitecture=RuntimeInformation.ProcessArchitecture.ToString(),PointerSize=IntPtr.Size,Reads=reads,ValidCpuReads=validCpu,ValidCoreReads=validCoreReads,
            ProcessRows=processRows,Failure=failure,WpfLoaded=wpf,LoadedAssemblies=assemblies,Records=recorder.Snapshot(),NaturalExit=true,ExitCode=code},new JsonSerializerOptions{WriteIndented=true}));
        Thread.Sleep(2000);return code;
    }
}
