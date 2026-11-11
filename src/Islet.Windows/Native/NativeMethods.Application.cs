using System.Runtime.InteropServices;
namespace Islet.Windows.Native;
internal static partial class NativeMethods
{
    // Shared system cursor; do not destroy. Verified LoadCursorW documentation in API_REGISTER.md.
    [DllImport("user32.dll",ExactSpelling=true,SetLastError=true)] internal static extern nint LoadCursorW(nint instance,nint resource);
}