namespace Islet.Windows.Tests;

[TestClass]
public sealed class PlatformBoundaryTests
{
    [TestMethod]
    public void PlatformAssemblyHasNoWpfOrShellDependency()
    {
        var references = typeof(WindowsPlatform).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        CollectionAssert.DoesNotContain(references, "Islet.Shell");
        CollectionAssert.DoesNotContain(references, "PresentationFramework");
        Assert.IsTrue(WindowsPlatform.IsAvailable);
    }
}
