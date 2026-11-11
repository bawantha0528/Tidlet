using System.Security;
using Islet.Core.Bar;
using Microsoft.Win32;

namespace Islet.Shell.Bar;

internal static class WindowsAppTheme
{
    internal static BarTheme Resolve(BarTheme preference)
    {
        if (preference != BarTheme.System) return preference;
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1) is 0 ? BarTheme.Dark : BarTheme.Light;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return BarTheme.Light;
        }
    }
}
