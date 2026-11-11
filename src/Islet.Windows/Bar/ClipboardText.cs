using System.Runtime.InteropServices;
namespace Islet.Windows.Bar;
public static class ClipboardText
{
    [DllImport("user32.dll")]private static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll")]private static extern bool CloseClipboard();
    [DllImport("user32.dll")]private static extern nint GetClipboardData(uint format);
    [DllImport("kernel32.dll")]private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")]private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")]private static extern nuint GlobalSize(nint handle);
    /// <summary>Read only for an explicit Paste command; never monitors or writes the clipboard.</summary>
    public static string Read()
    {
        if(!OpenClipboard(0))return "";
        try
        {
            var handle=GetClipboardData(13);if(handle==0)return "";var bytes=(ulong)GlobalSize(handle);if(bytes<2||bytes>1048576)return "";
            var pointer=GlobalLock(handle);if(pointer==0)return "";
            try{var max=(int)Math.Min(1024UL,bytes/2);int length=0;while(length<max&&Marshal.ReadInt16(pointer,length*2)!=0)length++;return Marshal.PtrToStringUni(pointer,length)??"";}
            finally{GlobalUnlock(handle);}
        }
        finally{CloseClipboard();}
    }
}
