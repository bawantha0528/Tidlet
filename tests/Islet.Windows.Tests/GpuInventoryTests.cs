using Islet.Windows.Popups;
namespace Islet.Windows.Tests;
[TestClass]
public sealed class GpuInventoryTests
{
    [TestMethod]
    public void DxgiDescriptionMatchesSdkOnCurrentArchitecture()
    {Assert.AreEqual(IntPtr.Size==8?312:296,GpuInventory.NativeDescriptionSize);Assert.AreEqual(IntPtr.Size==8?296:284,GpuInventory.NativeLuidOffset);}
}
