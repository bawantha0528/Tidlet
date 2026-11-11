using System.Runtime.InteropServices;
using Islet.Core.Popups;
namespace Islet.Windows.Popups;

/// <summary>DXGI 1.1 identity only. No device/swap chain, driver install or rendering context.</summary>
public static class GpuInventory
{
    [StructLayout(LayoutKind.Sequential)]private struct Luid{public uint Low;public int High;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Description
    {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Name;
        public uint Vendor,Device,Subsystem,Revision;public nuint Video,System,Shared;public Luid Id;public uint Flags;
    }
    public static int NativeDescriptionSize=>Marshal.SizeOf<Description>();
    public static int NativeLuidOffset=>Marshal.OffsetOf<Description>(nameof(Description.Id)).ToInt32();
    [DllImport("dxgi.dll",ExactSpelling=true)]private static extern int CreateDXGIFactory1(ref Guid iid,out nint factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Enumerate(nint self,uint index,out nint adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int Describe(nint self,out Description description);
    public static GpuActivity[] Read()
    {
        var iid=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");nint factory=0;
        try
        {
            if(CreateDXGIFactory1(ref iid,out factory)<0||factory==0)return [];
            var enumerate=Marshal.GetDelegateForFunctionPointer<Enumerate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory),12*IntPtr.Size));
            var result=new List<GpuActivity>();
            for(uint i=0;i<32;i++)
            {
                nint adapter=0;try
                {
                    if(enumerate(factory,i,out adapter)<0||adapter==0)break;
                    var describe=Marshal.GetDelegateForFunctionPointer<Describe>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(adapter),10*IntPtr.Size));
                    if(describe(adapter,out var info)>=0&&(info.Flags&2)==0&&!string.IsNullOrWhiteSpace(info.Name))
                        result.Add(new(((ulong)(uint)info.Id.High<<32)|info.Id.Low,info.Name.Trim(),null,(ulong)info.Video,"Waiting for valid Windows GPU engine samples."));
                }
                finally{if(adapter!=0)Marshal.Release(adapter);}
            }
            return result.ToArray();
        }
        catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or COMException){return [];}
        finally{if(factory!=0)Marshal.Release(factory);}
    }
}
