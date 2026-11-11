using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Islet.Core.Bar;
using Islet.Core.Popups;
using Islet.Shell.Bar;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class PopupDrawingTests
{
    [TestMethod]
    public void ReusedDrawingTextDoesNotKeepStaleValuesAfterDataAndDpiChanges()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var type=typeof(PopupCoordinator).Assembly.GetType("Islet.Shell.Bar.PopupVisual",true)!;
                foreach(var module in new[]{BarModule.Cpu,BarModule.Ram})
                {
                    var visual=(UIElement)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public,null,
                        [module,684.0,1.0,(Action)(()=>{}),(Action)(()=>{}),false],null)!;
                    visual.Measure(new Size(320,684));visual.Arrange(new Rect(0,0,320,684));
                    var history=new SampleRing(121);var averages=new SampleRing(901);
                    for(int i=0;i<30;i++){history.Push(i,20);averages.Push(i,20);}
                    PopupSample Sample(double cpu,string name)=>new(cpu,5,cpu-5,100-cpu,2.5,4,TimeSpan.FromHours(1),
                        new(100,50,50,100,20,null,null),[10,20],[0,0],[new(12,name,cpu,25)],"Read-only fixture");
                    var update=type.GetMethod("Update")!;var redraw=type.GetMethod("Redraw")!;
                    var original=Sample(20,"Original process");
                    type.GetMethod("ShowPrevious")!.Invoke(visual,[original]);
                    update.Invoke(visual,[original,history,averages,29.0]);var before=Pixels(visual);
                    Assert.AreEqual((byte)0,before[3],"The rounded corner should remain transparent");
                    var backgroundPixel=(24*320+160)*4;
                    Assert.AreEqual((byte)255,before[backgroundPixel+3],"The popup surface should be opaque");
                    Assert.AreEqual(before[backgroundPixel],before[backgroundPixel+2],"The popup surface should be neutral charcoal");
                    for(int i=0;i<180;i++)update.Invoke(visual,[Sample(20+i%70,"Process "+i),history,averages,29.0]);
                    redraw.Invoke(visual,[1.5]);redraw.Invoke(visual,[1.0]);
                    update.Invoke(visual,[original,history,averages,29.0]);var after=Pixels(visual);
                    CollectionAssert.AreEqual(before,after,$"{module} retained stale drawing content after data/DPI changes");
                }
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)));
        if(failure is not null)throw new AssertFailedException("Popup drawing reuse failed",failure);
    }
    private static byte[] Pixels(Visual visual)
    {
        var bitmap=new RenderTargetBitmap(320,684,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var bytes=new byte[320*684*4];bitmap.CopyPixels(bytes,320*4,0);return bytes;
    }
}
