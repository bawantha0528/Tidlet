using System.Runtime.InteropServices;
using Microsoft.Win32;
using Islet.Core.Bar;
using Islet.Windows.Popups;
namespace Islet.Windows.Bar;
public static class DeviceOverviewSource
{
    public static DeviceOverview Read()
    {
        string model="Windows PC",processor="Processor model unavailable";var limitations=new List<string>();
        try
        {
            // TODO(verify): OEM registry value availability across Windows/OEM models. Optional, read-only fallback.
            using var bios=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            var manufacturer=bios?.GetValue("SystemManufacturer") as string;var product=bios?.GetValue("SystemProductName") as string;
            if(!string.IsNullOrWhiteSpace(product))model=(manufacturer+" "+product).Trim();
            using var cpu=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            processor=(cpu?.GetValue("ProcessorNameString") as string)?.Trim()??processor;
        }
        catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException or IOException){limitations.Add("Windows did not allow reading device model details.");}
        var facts=new List<DeviceFact>{new("Processor",processor),new("Logical processors",Environment.ProcessorCount.ToString())};
        var classes=PopupNative.Classes();if(classes.Length>0&&classes.Distinct().Count()>1){var max=classes.Max();facts.Add(new("P-class logical processors",classes.Count(x=>x==max).ToString()));facts.Add(new("E-class logical processors",classes.Count(x=>x>=0&&x!=max).ToString()));}
        var memory=PopupNative.Memory();if(memory is{Valid:true})facts.Add(new("Installed usable memory",$"{memory.Total/1073741824.0:0.0} GiB"));
        var gpu=GpuInventory.Read();if(gpu.Length==0)limitations.Add("Windows did not report a hardware GPU identity.");foreach(var adapter in gpu)facts.Add(new("Graphics",adapter.Name));
        foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType==DriveType.Fixed&&drive.IsReady)facts.Add(new("Volume "+drive.Name,$"{drive.TotalSize/1073741824.0:0.0} GiB"));}catch(IOException){}catch(UnauthorizedAccessException){}
        facts.Add(new("Uptime",$"{(int)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalDays} days, {TimeSpan.FromMilliseconds(Environment.TickCount64).Hours} hours"));
        return new(model,"Windows · build "+Environment.OSVersion.Version.Build,facts.ToArray(),limitations.ToArray());
    }
}
