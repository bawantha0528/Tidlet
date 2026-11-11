using System.Runtime.InteropServices;
using Islet.Windows.Popups;
namespace Islet.Windows.Tests;
[TestClass]
public sealed class PopupNativeTests
{
    [TestMethod]
    public void PopupNativeLayoutsMatchSdkOnBothProcessArchitectures()
    {
        Assert.AreEqual(IntPtr.Size==4?56:104,Marshal.SizeOf<PopupNative.Performance>());
        Assert.AreEqual(IntPtr.Size==4?20:32,Marshal.SizeOf<PopupNative.PageFile>());
        Assert.AreEqual(24,Marshal.SizeOf<WindowsPopupSource.CounterItem>());
        Assert.AreEqual(new nint(8),Marshal.OffsetOf<WindowsPopupSource.CounterItem>("Value"));
    }
}
