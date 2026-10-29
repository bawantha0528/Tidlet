using System.Runtime.InteropServices;
using Islet.Windows.Native;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class NativeRendererTests
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod]
    public void LayeredWindowAbiUsesNativePointerSizesAndPackedBlendBytes()
    {
        TestContext.WriteLine($"Native ABI pointer size: {IntPtr.Size}");
        Assert.AreEqual(IntPtr.Size == 8 ? 80 : 48, Marshal.SizeOf<NativeMethods.WindowClass>());
        Assert.AreEqual(IntPtr.Size == 8 ? 48 : 32, Marshal.SizeOf<NativeMethods.Message>());
        Assert.AreEqual(IntPtr.Size == 8 ? 976 : 956, Marshal.SizeOf<NativeMethods.NotifyIcon>());
        Assert.AreEqual(104, Marshal.SizeOf<NativeMethods.MonitorInfoEx>());
        Assert.AreEqual(40, Marshal.SizeOf<NativeMethods.BitmapHeader>());
        Assert.AreEqual(44, Marshal.SizeOf<NativeMethods.BitmapInfo>());
        Assert.AreEqual(4, Marshal.SizeOf<NativeMethods.Blend>());
        Assert.AreEqual(new nint(2), Marshal.OffsetOf<NativeMethods.Blend>("Alpha"));
        Assert.AreEqual(new nint(8), Marshal.OffsetOf<NativeMethods.WindowClass>("Proc"));
    }

    [TestMethod]
    public void ColorsArePremultipliedAndCompositedWithoutTransparentHalos()
    {
        var red = DibRenderer.Premultiply(0xFF0000, 128);
        Assert.AreEqual(0x80800000u, unchecked((uint)red));
        Assert.AreEqual(unchecked((int)0xFF00FF00), DibRenderer.Over(red, 0x00FF00, 255));
        Assert.AreEqual(red, DibRenderer.Over(red, 0xFFFFFF, 0));
        Assert.AreEqual(0, DibRenderer.Premultiply(0xFFFFFF, 0));
    }
}
