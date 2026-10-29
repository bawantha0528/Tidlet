using Microsoft.VisualStudio.TestTools.UnitTesting;
using Islet.Core.Alerts;
using Islet.Core.Island;
using Islet.Core.Metrics;
using Islet.Core.Profiles;
using Islet.Core.Settings;

namespace Islet.Core.Tests;

[TestClass]
public sealed class M3SettingsAndBehaviorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TidletTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch { }
        }
    }

    [TestMethod]
    public void SettingsStorage_RoundTrip_PreservesValuesAndCreatesBackup()
    {
        var store = new FileSettingsStore(_tempDir);
        var initial = new SettingsDocument
        {
            Style = IslandStyle.Notch,
            Edge = IslandEdge.Bottom,
            Opacity = 0.85,
            TransitionDurationMs = 280,
            CollapsedMetric = "ram"
        };

        store.Save(initial);
        var loaded = store.Load();
        Assert.AreEqual(initial.Style, loaded.Style);
        Assert.AreEqual(initial.Edge, loaded.Edge);
        Assert.AreEqual(0.85, loaded.Opacity, 0.001);
        Assert.AreEqual(280, loaded.TransitionDurationMs);
        Assert.AreEqual("ram", loaded.CollapsedMetric);

        // Second save creates backup
        var updated = loaded with { Opacity = 0.90 };
        store.Save(updated);

        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "settings.backup.json")));
        var loaded2 = store.Load();
        Assert.AreEqual(0.90, loaded2.Opacity, 0.001);
    }

    [TestMethod]
    public void SettingsStorage_CorruptJson_RecoversFromBackupAndPreservesCorrupt()
    {
        var store = new FileSettingsStore(_tempDir);
        var good = new SettingsDocument { Style = IslandStyle.Bar };
        store.Save(good);
        store.Save(good with { Opacity = 0.75 });
        store.Save(good with { Opacity = 0.80 }); // Creates backup containing 0.75

        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{ corrupt json ... [truncated]");

        var recovered = store.Load();
        Assert.IsNotNull(store.LastWarning);
        StringAssert.Contains(store.LastWarning, "Corrupt settings detected");
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "settings.corrupt.json")));
        Assert.AreEqual(0.75, recovered.Opacity, 0.001);
    }

    [TestMethod]
    public void SettingsStorage_FutureSchema_DoesNotOverwriteAndWarns()
    {
        var store = new FileSettingsStore(_tempDir);
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, @"{ ""SchemaVersion"": 99, ""Style"": 0 }");

        var loaded = store.Load();
        Assert.IsNotNull(store.LastWarning);
        StringAssert.Contains(store.LastWarning, "schema version 99 is newer");
        Assert.AreEqual(1, loaded.SchemaVersion);
    }

    [TestMethod]
    public void SettingsEnvelope_ImportExport_RoundTripsAndValidatesLimits()
    {
        var original = new SettingsDocument { Style = IslandStyle.Notch, Opacity = 0.8 };
        var json = SettingsEnvelope.Export(original);
        StringAssert.Contains(json, "Tidlet Monitor");

        var (success, imported, error) = SettingsEnvelope.TryImport(json);
        Assert.IsTrue(success, error);
        Assert.IsNotNull(imported);
        Assert.AreEqual(IslandStyle.Notch, imported.Style);

        // Rejects oversize
        var oversize = new string('x', 600 * 1024);
        var (overSuccess, _, overError) = SettingsEnvelope.TryImport(oversize);
        Assert.IsFalse(overSuccess);
        StringAssert.Contains(overError, "512 KB");
    }

    [TestMethod]
    public void AlertEngine_BatteryLow_AppliesThresholdHysteresisAndCooldown()
    {
        var alerts = new AlertSettings { Enabled = true, BatteryLowThresholdPercent = 20.0, CooldownSeconds = 30 };
        var engine = new AlertEngine(alerts);
        var t0 = DateTime.UtcNow;

        // 1. First low battery reading triggers warning
        var bat18 = new BatteryReading(BatteryReadingState.Available, 18.0, BatteryChargingStatus.Discharging, null, null, null, "18%");
        var notices1 = engine.Evaluate(bat18, null, false, false, t0);
        Assert.AreEqual(1, notices1.Count);
        Assert.AreEqual("Battery Low", notices1[0].Title);

        // 2. Cooldown prevents immediate re-trigger
        var notices2 = engine.Evaluate(bat18, null, false, false, t0.AddSeconds(5));
        Assert.AreEqual(0, notices2.Count);

        // 3. DND suppresses alerts
        var notices3 = engine.Evaluate(bat18, null, isDoNotDisturb: true, isSuppressed: false, t0.AddSeconds(60));
        Assert.AreEqual(0, notices3.Count);
    }

    [TestMethod]
    public void ProfileManager_ManualOverrideAndFullscreenRules_ResolveCorrectly()
    {
        var rules = new List<ProfileRule>
        {
            new() { Name = "Work", Style = IslandStyle.Island, SuppressAlerts = false },
            new() { Name = "Gaming", Style = IslandStyle.Bar, SuppressAlerts = true },
            new() { Name = "Meeting", Style = IslandStyle.Island, SuppressAlerts = true }
        };

        var manager = new ProfileManager(rules);
        var t0 = DateTime.UtcNow;

        // Default resolves to Work
        var res1 = manager.Evaluate(false, null, t0);
        Assert.AreEqual("Work", res1.ActiveProfileName);
        Assert.AreEqual(IslandStyle.Island, res1.ResolvedStyle);
        Assert.IsFalse(res1.SuppressAlerts);

        // Fullscreen switches to Gaming after debounce
        manager.Evaluate(true, null, t0);
        var res2 = manager.Evaluate(true, null, t0.AddSeconds(3));
        Assert.AreEqual("Gaming", res2.ActiveProfileName);
        Assert.AreEqual(IslandStyle.Bar, res2.ResolvedStyle);
        Assert.IsTrue(res2.SuppressAlerts);

        // Manual override takes precedence over fullscreen
        manager.SetManualOverride("Meeting");
        var res3 = manager.Evaluate(true, null, t0.AddSeconds(4));
        Assert.AreEqual("Meeting", res3.ActiveProfileName);
        Assert.IsTrue(res3.SuppressAlerts);
    }

    [TestMethod]
    public void IslandGeometry_AllEdges_PlacesCorrectlyWithinDisplayBounds()
    {
        var display = new IslandDisplay("display1", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 96);

        var top = IslandGeometry.Place(display, 220, 36, IslandStyle.Island, IslandEdge.Top);
        Assert.AreEqual(8, top.Y);
        Assert.AreEqual((1920 - 220) / 2, top.X);

        var bottom = IslandGeometry.Place(display, 220, 36, IslandStyle.Island, IslandEdge.Bottom);
        Assert.AreEqual(1080 - 36 - 8, bottom.Y);
        Assert.AreEqual((1920 - 220) / 2, bottom.X);

        var left = IslandGeometry.Place(display, 220, 36, IslandStyle.Island, IslandEdge.Left);
        Assert.AreEqual(8, left.X);
        Assert.AreEqual((1080 - 36) / 2, left.Y);

        var right = IslandGeometry.Place(display, 220, 36, IslandStyle.Island, IslandEdge.Right);
        Assert.AreEqual(1920 - 220 - 8, right.X);
        Assert.AreEqual((1080 - 36) / 2, right.Y);
    }
}
