namespace Islet.Windows;

/// <summary>Composition boundary. Native APIs/providers are introduced in M1/M2.</summary>
public static class WindowsPlatform
{
    public static bool IsAvailable => OperatingSystem.IsWindows();
}
