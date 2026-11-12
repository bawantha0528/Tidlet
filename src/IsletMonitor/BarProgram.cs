using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using Islet.Core.Capabilities;
using Islet.Shell.Bar;
using Islet.Windows.Bar;
using Islet.Windows.Metrics;
namespace IsletMonitor;
internal static class BarProgram
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args is [CpuTemperatureHelper.Argument])return CpuTemperatureHelper.RunWorker();
        if(args.Length>0&&args[0]=="--provider-comparison")return Islet.Windows.Diagnostics.ProviderComparison.Run(args);
        return RunApplication(args);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication(string[] args)
    {
        if(args.Length==0||args.Contains("--r1-probe"))return BarApplication.Run(args);
        if(!BarProbeOptions.TryParse(args,out var options))return 2;
        if(options.SoftwareRendering) System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        var cpu=new SystemCpuProvider(new NativeSystemCpuSource());var ram=new SystemRamProvider(new NativeSystemRamSource());
        var cpuProbe=cpu.Probe();var ramProbe=ram.Probe();
        (BarValue Cpu,BarValue Ram) Read()=> (
            cpuProbe.Availability==CapabilityAvailability.Supported?BarText.Cpu(cpu.Sample()):new BarValue("—",cpuProbe.Explanation??"CPU unavailable"),
            ramProbe.Availability==CapabilityAvailability.Supported?BarText.Ram(ram.Sample()):new BarValue("—",ramProbe.Explanation??"RAM unavailable"));
        return options.VisualHost?RunVisual(options,Read,cpuProbe,ramProbe):RunWindow(options,Read,cpuProbe,ramProbe);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunVisual(BarProbeOptions options,Func<(BarValue Cpu,BarValue Ram)> read,object cpuProbe,object ramProbe)
    {
        using var host=new BarVisualHost(options,read,VisualHostPlacement.Position);
        host.Run();
        WriteReport(options,new{
            Mode="R0 WPF visual-host feasibility only",options.Transparent,options.SoftwareRendering,options.VisualHost,
            ProcessArchitecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),PointerSize=IntPtr.Size,
            ProcessRenderPreference=System.Windows.Media.RenderOptions.ProcessRenderMode.ToString(),ActualRenderPipelineVerified=false,
            host.MenuCreations,host.FirstFrame,host.Samples,host.TimersStopped,host.Scale,host.AccessibleName,host.AccessibilityPeerVerified,
            CpuCapability=cpuProbe,RamCapability=ramProbe,BarHeightDip=24,ChartWork=0,ProcessWork=0,AppBarReservations=0,
            LoadedMediaAssemblies=MediaAssemblies(),LoadedAssemblies=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).ToArray(),
            LegacyNativeRendererPresent=LegacyNative(),LegacyShellPresent=LegacyShell(),ExitCode=0,NaturalExit=true});
        return 0;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunWindow(BarProbeOptions options,Func<(BarValue Cpu,BarValue Ram)> read,object cpuProbe,object ramProbe)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
        var window=new BarFeasibilityWindow(options,read);
        var exit=app.Run(window);
        WriteReport(options,new{
            Mode="R0 WPF feasibility only",options.Transparent,options.SoftwareRendering,options.VisualHost,
            ProcessArchitecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),PointerSize=IntPtr.Size,
            ProcessRenderPreference=System.Windows.Media.RenderOptions.ProcessRenderMode.ToString(),ActualRenderPipelineVerified=false,
            window.MenuCreations,window.FirstFrame,window.Samples,window.TimersStopped,window.Scale,
            CpuCapability=cpuProbe,RamCapability=ramProbe,BarHeightDip=24,ChartWork=0,ProcessWork=0,AppBarReservations=0,
            LoadedMediaAssemblies=MediaAssemblies(),LegacyNativeRendererPresent=LegacyNative(),LegacyShellPresent=LegacyShell(),ExitCode=exit,NaturalExit=true});
        return exit;
    }
    private static string?[] MediaAssemblies()=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name).Where(n=>n is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime").ToArray();
    private static bool LegacyNative()=>typeof(NativeSystemCpuSource).Assembly.GetType("Islet.Windows.Native.NativeIslandDesktop") is not null;
    private static bool LegacyShell()=>typeof(BarValue).Assembly.GetType("Islet.Shell.Island.NativeIslandHost") is not null;
    private static void WriteReport(BarProbeOptions options,object report){if(options.Report is{} path)File.WriteAllText(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
}

