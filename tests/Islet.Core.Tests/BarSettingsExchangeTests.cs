using Islet.Core.Bar;
namespace Islet.Core.Tests;
[TestClass]
public sealed class BarSettingsExchangeTests
{
    [TestMethod]
    public void FormerToolSettingsAreIgnoredWithoutLosingMonitorOrder()
    {
        var restored=BarSettingsFile.Import("""
            {"Version":1,"Alignment":"Right","Items":["Gpu","Ram"],"ItemOrder":["Gpu","Cpu","Ram"],
             "Tools":{"Modules":{"Screenshot":{"Enabled":true},"Git":{"Enabled":true},"Clipboard":{"Enabled":true}}}}
            """);
        CollectionAssert.AreEqual(new[]{BarModule.Gpu,BarModule.Ram},restored.Items);
        Assert.AreEqual(BarAlignment.Right,restored.Alignment);
        CollectionAssert.AreEqual(new[]{BarModule.Gpu,BarModule.Cpu,BarModule.Ram},restored.WithItemVisibility(BarModule.Cpu,true).Items);
        Assert.IsFalse(BarSettingsFile.Export(restored).Contains("\"Tools\"",StringComparison.Ordinal));
    }
    [TestMethod]
    public void ModulePreferencesAndAppearanceSurviveExchange()
    {
        var original=new BarSettings{Theme=BarTheme.Light,Accent="#128AEF",UseBits=true,Fahrenheit=true,
            Items=[BarModule.Ram,BarModule.Network,BarModule.Cpu],Modules=new(){[BarModule.Ram]=new(){PopupEnabled=false,UpdateSeconds=5,Style=BarItemStyle.TwoLine}}};
        var restored=BarSettingsFile.Import(BarSettingsFile.Export(original));
        CollectionAssert.AreEqual(original.Items,restored.Items);
        Assert.AreEqual(original.Theme,restored.Theme);Assert.AreEqual(original.Accent,restored.Accent);
        Assert.IsTrue(restored.UseBits&&restored.Fahrenheit);
        Assert.AreEqual(original.Preference(BarModule.Ram),restored.Preference(BarModule.Ram));
        Assert.AreEqual(1,restored.Preference(BarModule.Cpu).UpdateSeconds);
    }
    [TestMethod]
    public void ImportRejectsFutureMalformedOversizedAndInvalidPreferences()
    {
        Assert.ThrowsExactly<InvalidDataException>(()=>BarSettingsFile.Import("{\"Version\":2}"));
        Assert.ThrowsExactly<System.Text.Json.JsonException>(()=>BarSettingsFile.Import("{broken"));
        Assert.ThrowsExactly<InvalidDataException>(()=>BarSettingsFile.Import(new string(' ',262145)));
        Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{Accent="blue"}.Validate());
        Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{Modules=new(){[BarModule.Cpu]=new(){UpdateSeconds=0}}}.Validate());
        Assert.ThrowsExactly<InvalidDataException>(()=>new BarSettings{PingTarget="host/path"}.Validate());
    }
}
