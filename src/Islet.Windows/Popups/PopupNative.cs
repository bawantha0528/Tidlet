using System.Runtime.InteropServices;
using Islet.Core.Popups;
namespace Islet.Windows.Popups;
internal static class PopupNative
{
    [StructLayout(LayoutKind.Sequential)]internal struct Performance
    {public uint Size;public nuint Commit,Limit,Peak,Physical,Available,Cache,Kernel,Paged,Nonpaged,PageSize;public uint Handles,Processes,Threads;}
    [StructLayout(LayoutKind.Sequential)]internal struct PageFile{public uint Size,Reserved;public nuint Total,Used,Peak;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)][return:MarshalAs(UnmanagedType.Bool)]private delegate bool PageCallback(nint context,ref PageFile data,nint name);
    [DllImport("psapi.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetPerformanceInfo(out Performance info,uint size);
    [DllImport("psapi.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool EnumPageFilesW(PageCallback callback,nint context);
    [DllImport("powrprof.dll",ExactSpelling=true)]private static extern uint CallNtPowerInformation(int level,nint input,uint inputSize,byte[] output,uint outputSize);
    [DllImport("kernel32.dll",ExactSpelling=true)]private static extern uint GetActiveProcessorCount(ushort group);
    [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetLogicalProcessorInformationEx(int relationship,nint buffer,ref uint size);
    public static PopupMemory? Memory()
    {
        if(!GetPerformanceInfo(out var p,(uint)Marshal.SizeOf<Performance>()))
        {var basic=new Islet.Windows.Metrics.NativeSystemRamSource();return basic.TryRead(out var basicTotal,out var available,out _)?new PopupMemory(basicTotal,available,null,null,null,null,null):null;}
        var page=(ulong)p.PageSize;if(page is 0 or >1048576)return null;
        ulong total=0,used=0;bool invalid=false;
        bool Callback(nint context,ref PageFile data,nint name){if(data.Used>data.Total){invalid=true;return false;}total+=(ulong)data.Total;used+=(ulong)data.Used;return true;}
        var files=EnumPageFilesW(Callback,0)&&!invalid;
        try{return new(checked((ulong)p.Physical*page),checked((ulong)p.Available*page),checked((ulong)p.Commit*page),checked((ulong)p.Limit*page),checked((ulong)p.Cache*page),files?checked(used*page):null,files?checked(total*page):null);}
        catch(OverflowException){return null;}
    }
    public static int ProcessorCount=>(int)GetActiveProcessorCount(0xffff);
    public static (double? Current,double? Maximum) Frequency()
    {
        var count=ProcessorCount;if(count is<1 or>64)return(null,null);var bytes=new byte[count*24];
        if(CallNtPowerInformation(11,0,0,bytes,(uint)bytes.Length)!=0)return(null,null);
        double current=0,max=0;for(int i=0;i<count;i++)
        {var m=BitConverter.ToUInt32(bytes,i*24+4);var c=BitConverter.ToUInt32(bytes,i*24+8);if(c is 0 or >100000||m is 0 or >100000)return(null,null);current+=c;max+=m;}
        return(current/count/1000,max/count/1000);
    }
    public static int[] Classes()
    {
        var count=ProcessorCount;if(count<1||(count>32&&IntPtr.Size==4)||count>64)return[];
        uint size=0;GetLogicalProcessorInformationEx(0,0,ref size);if(size is 0 or >1048576)return[];
        var ptr=Marshal.AllocHGlobal((int)size);
        try
        {
            if(!GetLogicalProcessorInformationEx(0,ptr,ref size))return[];
            var result=Enumerable.Repeat(-1,count).ToArray();
            for(int offset=0;offset<(int)size;)
            {
                if(offset+32>size)return[];int length=Marshal.ReadInt32(ptr,offset+4);if(length<32||offset+length>size)return[];
                var groupCount=(ushort)Marshal.ReadInt16(ptr,offset+30);if(groupCount!=1||length<32+IntPtr.Size+8)return[];
                var group=(ushort)Marshal.ReadInt16(ptr,offset+32+IntPtr.Size);if(group!=0)return[];
                var mask=IntPtr.Size==8?(ulong)Marshal.ReadInt64(ptr,offset+32):(uint)Marshal.ReadInt32(ptr,offset+32);
                var efficiency=Marshal.ReadByte(ptr,offset+9);
                for(int bit=0;bit<count;bit++)if((mask&(1UL<<bit))!=0)result[bit]=efficiency;
                offset+=length;
            }
            return result;
        }
        finally{Marshal.FreeHGlobal(ptr);}
    }
}


