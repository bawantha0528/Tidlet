using Islet.Core.Bar;
namespace Islet.Core.Tests;
[TestClass]
public sealed class BarPolicyTests
{
    [TestMethod]
    public void ToggleCompactsTheBarAndRestoresOrderAcrossImportAndAllOff()
    {
        var original=new[]{BarModule.Gpu,BarModule.Cpu,BarModule.Ram,BarModule.Network,BarModule.Battery};
        foreach(var alignment in Enum.GetValues<BarAlignment>())
        foreach(var module in original)
        {
            var settings=new BarSettings{Items=original,Alignment=alignment};
            var before=BarLayout.Items(settings,1280,true);
            settings=settings.WithItemVisibility(module,false);
            CollectionAssert.AreEqual(original.Where(m=>m!=module).ToArray(),settings.Items);
            var compact=BarLayout.Items(settings,1280,true);
            for(var i=1;i<compact.Length;i++)Assert.AreEqual(10,compact[i].X-compact[i-1].X-compact[i-1].Width,.001);
            Assert.IsFalse(compact.Any(item=>item.Module==module));
            settings=BarSettingsFile.Import(BarSettingsFile.Export(settings)).WithItemVisibility(module,true);
            CollectionAssert.AreEqual(original,settings.Items);CollectionAssert.AreEqual(before,BarLayout.Items(settings,1280,true));
            foreach(var item in original)settings=settings.WithItemVisibility(item,false);
            settings=BarSettingsFile.Import(BarSettingsFile.Export(settings));
            foreach(var item in original.Reverse())settings=settings.WithItemVisibility(item,true);
            CollectionAssert.AreEqual(original,settings.Items);
        }
    }
    [TestMethod]
    public void BatteryStaysLastUntilTheUserChoosesADifferentOrder()
    {
        var settings=new BarSettings{Items=[BarModule.Cpu,BarModule.Battery,BarModule.Ram]};
        CollectionAssert.AreEqual(new[]{BarModule.Cpu,BarModule.Ram,BarModule.Battery},settings.NormalItems);
        settings=settings.WithItemVisibility(BarModule.Gpu,true);
        Assert.AreEqual(BarModule.Battery,settings.NormalItems[^1]);
        settings=settings.MoveItemEarlier(BarModule.Battery);
        var ordered=settings.NormalItems.ToArray();
        Assert.AreNotEqual(BarModule.Battery,ordered[^1]);
        settings=BarSettingsFile.Import(BarSettingsFile.Export(settings));
        settings=settings.WithItemVisibility(BarModule.Battery,false).WithItemVisibility(BarModule.Battery,true);
        CollectionAssert.AreEqual(ordered,settings.NormalItems);
        Assert.AreEqual(BarModule.Battery,settings.MoveItemLater(BarModule.Battery).NormalItems[^1]);
        Assert.AreEqual(BarModule.Battery,(settings with { GamingMode=true }).VisibleItems[^1]);
    }

    [TestMethod]
    public void ExplicitReorderingIsRememberedBySubsequentToggles()
    {
        var settings=new BarSettings{Items=[BarModule.Cpu,BarModule.Gpu,BarModule.Ram]};
        settings=settings.WithItemVisibility(BarModule.Gpu,false).MoveItemEarlier(BarModule.Ram);
        settings=BarSettingsFile.Import(BarSettingsFile.Export(settings)).WithItemVisibility(BarModule.Gpu,true);
        CollectionAssert.AreEqual(new[]{BarModule.Ram,BarModule.Gpu,BarModule.Cpu},settings.Items);
        settings=settings.WithItemVisibility(BarModule.Ram,false).WithItemVisibility(BarModule.Ram,true);
        CollectionAssert.AreEqual(new[]{BarModule.Ram,BarModule.Gpu,BarModule.Cpu},settings.Items);
        Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{ItemOrder=[BarModule.Cpu,BarModule.Cpu]}.Validate());
        Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{ItemOrder=null!}.Validate());
    }
    [TestMethod]
    public void EveryItemCanBeSwitchedOffAndEmptyLayoutSurvivesExportImport()
    {
        foreach(var module in BarSettings.ConfigurableModules)
        {
            var settings=new BarSettings{Items=[module]}.WithItemVisibility(module,false);Assert.AreEqual(0,settings.Items.Length);
            var restored=BarSettingsFile.Import(BarSettingsFile.Export(settings));Assert.AreEqual(0,restored.Items.Length);Assert.AreEqual(0,BarLayout.Items(restored,1200,true).Length);
            restored=restored.WithItemVisibility(module,true).WithItemVisibility(module,true);CollectionAssert.AreEqual(new[]{module},restored.Items);
        }
    }
    [TestMethod]
    public void MissingBatteryAndReorderingKeepBoundsAndHitTargetsConsistent()
    {
        var settings=new BarSettings{Items=[BarModule.Network,BarModule.Cpu,BarModule.Battery],Alignment=BarAlignment.Right};
        var items=BarLayout.Items(settings,1280,false);
        Assert.AreEqual(2,items.Length);Assert.AreEqual(BarModule.Network,items[0].Module);
        Assert.AreEqual(10,items[1].X-(items[0].X+items[0].Width),.001);
        Assert.AreEqual(BarModule.Cpu,BarLayout.Hit(items,items[1].X+1));Assert.IsNull(BarLayout.Hit(items,0));
    }
    [TestMethod]
    public void ExplicitMeterStylesCompactToTextWhenTheStripHasLimitedSpace()
    {
        foreach(var module in new[]{BarModule.Cpu,BarModule.Gpu,BarModule.Ram,BarModule.Battery})
        {
            Assert.AreEqual(BarItemStyle.Ring,BarStyles.Resolve(module,BarItemStyle.Ring));
            Assert.AreEqual(BarItemStyle.TwoLine,BarStyles.Resolve(module,BarItemStyle.Ring,false));
        }
        var settings=new BarSettings{Items=[BarModule.Cpu,BarModule.Ram],
            Modules=new(){[BarModule.Cpu]=new(){Style=BarItemStyle.IconOnly},[BarModule.Ram]=new(){Style=BarItemStyle.Bar}}};
        var items=BarLayout.Items(settings,220,true);
        Assert.AreEqual(32.0,items[0].Width);
        Assert.AreEqual(84.0,items[1].Width);
        Assert.AreEqual(BarModule.Ram,BarLayout.Hit(items,items[1].X+1));
    }
    [TestMethod]
    public void TopStripFitsMetersWhenSpaceAllowsAndFallsBackInTheSameOrder()
    {
        var settings=new BarSettings{Items=[BarModule.Cpu,BarModule.Gpu,BarModule.Ram,BarModule.Network,BarModule.Battery]};
        var wide=BarLayout.Items(settings,640,true);
        var narrow=BarLayout.Items(settings,420,true);
        var textOnly=BarLayout.Items(settings with { Modules=new(){[BarModule.Cpu]=new(){Style=BarItemStyle.TwoLine}} },640,true);

        CollectionAssert.AreEqual(settings.Items,narrow.Select(item=>item.Module).ToArray());
        for(var i=0;i<3;i++)
        {
            Assert.AreEqual(84,wide[i].Width,.001);
            Assert.AreEqual(54,narrow[i].Width,.001);
        }
        Assert.IsTrue(narrow[^1].X+narrow[^1].Width<=420);
        Assert.AreEqual(54,textOnly[0].Width,.001);
        Assert.AreEqual(84,textOnly[1].Width,.001);
    }
    [TestMethod]
    public void BottomOverlayFitsWorkAreaAtHighDpiAndKeepsPopupAboveIt()
    {
        var display=new BarDisplay("second",false,new(-1920,-100,0,980),new(-1880,-100,0,932));
        var settings=new BarSettings{Edge=BarEdge.Bottom,Items=[BarModule.Cpu,BarModule.Gpu,BarModule.Ram,BarModule.Network,BarModule.Battery]};
        foreach(var alignment in Enum.GetValues<BarAlignment>())
        {
            var strip=BarLayout.BottomOverlay(display,settings with{Alignment=alignment},1.5);
            Assert.AreEqual(51,strip.Height);Assert.AreEqual(display.Work.Bottom-6,strip.Bottom);
            Assert.IsTrue(strip.Left>=display.Work.Left&&strip.Right<=display.Work.Right&&strip.Width<display.Work.Width);
            var items=BarLayout.Items(settings,strip.Width/1.5,true);
            CollectionAssert.AreEqual(settings.Items,items.Select(x=>x.Module).ToArray());
            Assert.AreEqual(84,items[0].Width,.001);Assert.IsTrue(items[^1].X+items[^1].Width<=strip.Width/1.5);
            var popup=BarLayout.Popup(strip,display.Work,480,540,BarEdge.Bottom);Assert.IsTrue(popup.Bottom<strip.Top);
        }
        Assert.AreEqual(24,BarLayout.HeightDip(BarEdge.Top),.001);
    }
    [TestMethod]
    public void PopupClampsToNegativeCoordinateMonitorAndBottomEdge()
    {
        var work=new BarRect(-1920,0,0,1000);var popup=BarLayout.Popup(new(-1910,970,-1800,1000),work,480,800,BarEdge.Bottom);
        Assert.IsTrue(popup.Left>=work.Left&&popup.Right<=work.Right&&popup.Top>=work.Top&&popup.Bottom<=work.Bottom);
        Assert.AreEqual(-1920,popup.Left);Assert.AreEqual(164,popup.Top);
    }
    [TestMethod]
    public void ReservationIsReleasedOnceAcrossHideFailureAndShutdown()
    {
        var added=0;var removed=0;using var lease=new BarReservation(()=>removed++);
        Assert.IsTrue(lease.Acquire(()=>{added++;return true;}));lease.Acquire(()=>{added++;return true;});Assert.AreEqual(1,added);
        lease.Dispose();lease.Dispose();Assert.AreEqual(1,removed);
        Assert.IsFalse(lease.Acquire(()=>false));Assert.IsFalse(lease.Active);Assert.AreEqual(1,removed);
        Assert.IsTrue(lease.Acquire(()=>true));try{throw new IOException("Placement failed");}catch(IOException){lease.Dispose();}
        Assert.IsFalse(lease.Active);Assert.AreEqual(2,removed);
    }
    [TestMethod]
    public void CorruptSettingsArePreservedAndInvalidLayoutsRejected()
    {
        var dir=Path.Combine(Path.GetTempPath(),"Tidlet-settings-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var file=Path.Combine(dir,"bar.json");File.WriteAllText(file,"{broken");var settings=BarSettingsFile.Load(file,out var notice);
            Assert.IsNotNull(notice);Assert.AreEqual("{broken",File.ReadAllText(file));
            BarSettingsFile.Save(file,settings with{Mode=BarMode.Reserve,Monitors=MonitorSelection.All});var restored=BarSettingsFile.Load(file,out notice);
            Assert.IsNull(notice);Assert.AreEqual(BarMode.Reserve,restored.Mode);
            Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{Opacity=double.NaN}.Validate());
            Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{Items=[BarModule.Cpu,BarModule.Cpu]}.Validate());
        }
        finally{File.Delete(Path.Combine(dir,"bar.json"));File.Delete(Path.Combine(dir,"bar.json.tmp"));Directory.Delete(dir);}
    }
}

