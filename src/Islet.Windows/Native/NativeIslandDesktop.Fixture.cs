using Islet.Core.Island;
using Islet.Core.Settings;
using static Islet.Windows.Native.NativeMethods;

namespace Islet.Windows.Native;

public sealed partial class NativeIslandDesktop
{
    public bool FixtureForegroundConfirmed { get; private set; }

    /// <summary>Owned, naturally exiting borderless test fixture. Never used by normal startup.</summary>
    public int RunFullscreenFixture(int seconds)
    {
        Create();
        var display = ReadDisplay();
        SetClickThrough(false);
        Render(new(IslandStyle.Bar, IslandState.Expanded, display.Bounds, display.Dpi / 96.0,
            "Fullscreen test",
            "This local test closes by itself. Press Esc to close it sooner.", Array.Empty<double?>()));
        Show(true);
        SetForegroundWindow(_window);
        FixtureForegroundConfirmed = GetForegroundWindow() == _window;
        if (!FixtureForegroundConfirmed)
        {
            Failure = "Windows did not allow the owned test fixture into the foreground.";
            Close();
            return 1;
        }
        Input += OnFixtureInput;
        SetTimer(IslandTimer.Exit, checked(seconds * 1000));
        SetTimer(IslandTimer.Pointer, 100);
        try { return Run(); }
        finally { Input -= OnFixtureInput; }
    }

    private void OnFixtureInput(IslandInput input)
    {
        if (input.Kind == IslandInputKind.Close
            || input is { Kind: IslandInputKind.Timer, Value: (int)IslandTimer.Exit }
            || (input is { Kind: IslandInputKind.Timer, Value: (int)IslandTimer.Pointer } && GetAsyncKeyState(0x1B) < 0))
            Close();
    }
}
