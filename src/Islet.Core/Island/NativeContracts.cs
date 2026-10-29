using Islet.Core.Settings;

namespace Islet.Core.Island;

public enum IslandState { Collapsed, Hover, Expanded }
public enum IslandTimer { Metric = 1, Exit = 2, Pointer = 3, Animation = 4, Reconcile = 5, Exercise = 6 }
public enum IslandInputKind { Timer, LeftClick, ContextMenu, Toggle, Close, DisplayChanged, DpiChanged, PowerChanged, Command }
public readonly record struct IslandInput(IslandInputKind Kind, int Value = 0);
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
public sealed record IslandDisplay(string Id, PixelRect Bounds, PixelRect WorkArea, uint Dpi);
public sealed record IslandFrame(IslandStyle Style, IslandState State, PixelRect Bounds, double Scale,
    string Label, string Explanation, IReadOnlyList<double?> History, bool HighContrast = false);
public sealed record NativeIslandStatus(bool Visible, bool Layered, bool NoActivate, bool ToolWindow,
    bool ClickThrough, bool ForegroundIsIsland, uint Dpi, PixelRect Bounds, int ActiveTimers, int RenderCount);

public interface IIslandDesktop : IDisposable
{
    event Action<IslandInput>? Input;
    void Create();
    IslandDisplay ReadDisplay();
    void Render(IslandFrame frame);
    void Show(bool visible);
    void SetTimer(IslandTimer timer, int? milliseconds);
    NativeIslandStatus Observe();
    void Close();
    int Run();
}
