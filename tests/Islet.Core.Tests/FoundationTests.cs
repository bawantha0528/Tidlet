using System.Text.Json;
using Islet.Core.Capabilities;
using Islet.Core.Settings;

namespace Islet.Core.Tests;

[TestClass]
public sealed class FoundationTests
{
    [TestMethod]
    public void SupportedHasNoUnavailableReason()
    {
        var result = CapabilityProbeResult.Supported;
        Assert.AreEqual(CapabilityAvailability.Supported, result.Availability);
        Assert.IsNull(result.ReasonCode);
        Assert.IsNull(result.Explanation);
    }

    [TestMethod]
    [DataRow("", "No battery")]
    [DataRow("no_battery", " ")]
    [DataRow(null, "No battery")]
    [DataRow("no_battery", null)]
    public void UnsupportedRequiresAnActionableReason(string? code, string? reason)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityProbeResult.Unsupported(code!, reason!));
    }

    [TestMethod]
    public void MissingHardwareAndHelperAreDifferentCapabilities()
    {
        var absent = CapabilityProbeResult.Unsupported("no_battery", "This PC has no battery.");
        var helper = CapabilityProbeResult.NeedsHelper("temperature_helper", "CPU temperature needs an optional helper.");
        Assert.AreEqual(CapabilityAvailability.Unsupported, absent.Availability);
        Assert.AreEqual(CapabilityAvailability.NeedsHelper, helper.Availability);
        Assert.AreNotEqual(absent, helper);
    }

    [TestMethod]
    public void DefaultSettingsRoundTripWithoutLosingEnumsOrSchema()
    {
        var original = new SettingsDocument();
        var restored = JsonSerializer.Deserialize<SettingsDocument>(JsonSerializer.Serialize(original));
        Assert.AreEqual(original, restored);
        Assert.IsTrue(restored!.HasSupportedSchema);
    }

    [TestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(2, 0, 0)]
    [DataRow(1, 999, 0)]
    [DataRow(1, 0, 999)]
    public void UnknownSchemaOrEnumIsNotAccepted(int schema, int style, int theme)
    {
        Assert.IsFalse(new SettingsDocument
        {
            SchemaVersion = schema, Style = (IslandStyle)style, Theme = (ThemeMode)theme
        }.HasSupportedSchema);
    }

    [TestMethod]
    public void CoreDoesNotReferenceWindowsOrWpfAssemblies()
    {
        var references = typeof(ProductInfo).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        foreach (var name in references)
        {
            Assert.IsFalse(name.StartsWith("Islet.Windows", StringComparison.Ordinal)
                || name.StartsWith("Islet.Shell", StringComparison.Ordinal)
                || name is "PresentationFramework" or "PresentationCore" or "WindowsBase"
                || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal), name);
        }
        Assert.IsFalse(typeof(ProductInfo).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Any(method => method.IsDefined(typeof(System.Runtime.InteropServices.DllImportAttribute), false)));
    }
}
