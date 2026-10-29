using System.Runtime.ExceptionServices;
using Islet.Core.Island;
using Islet.Core.Settings;
using Islet.Windows.Native;

namespace Islet.Windows.Tests;

[TestClass]
public sealed class NativeResourceTests
{
    [TestMethod]
    public void RealNativeSurfacesAndFontsAreReleasedAcrossRepeatedResizeAndClose()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Establish a GDI baseline after the first font/DC mapping.
                RenderAndDispose();
                var before = NativeMethods.GetGuiResources(-1,0);
                for(var cycle=0;cycle<8;cycle++) RenderAndDispose();
                var after = NativeMethods.GetGuiResources(-1,0);
                Assert.IsTrue(after <= before + 2,$"GDI resources grew from {before} to {after} after disposal.");
            }
            catch(Exception ex) { failure=ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(25)),"Native GDI cleanup check timed out.");
        if(failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void RenderAndDispose()
    {
        using var desktop = new NativeIslandDesktop();
        desktop.Create();
        var display=desktop.ReadDisplay();
        foreach(var style in Enum.GetValues<IslandStyle>())
            foreach(var state in Enum.GetValues<IslandState>())
            {
                var size=IslandSize.For(style,state);
                desktop.Render(new(style,state,IslandGeometry.Place(display,size.Width,size.Height,style),
                    display.Dpi/96.0,"CPU  25%","Fixture values for resource tests only.",
                    [20,25,null,40,25]));
                Assert.IsTrue(NativeMethods.GetGuiResources(-1,0)>0,"Live native surfaces should own GDI resources.");
            }
    }
}
