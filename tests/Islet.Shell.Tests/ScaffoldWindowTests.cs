using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using Islet.Shell;

namespace Islet.Shell.Tests;

[TestClass]
public sealed class ScaffoldWindowTests
{
    [TestMethod]
    public void ScaffoldDoesNotPretendToBeTheTransparentIsland()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ScaffoldWindow();
                Assert.IsFalse(window.AllowsTransparency);
                Assert.IsFalse(window.Topmost);
                Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
                Assert.IsInstanceOfType<StackPanel>(window.Content);
                Assert.IsTrue(window.Title.Contains("M0 scaffold", StringComparison.Ordinal));
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "STA window construction timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
