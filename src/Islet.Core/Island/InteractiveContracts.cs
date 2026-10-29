using Islet.Core.Settings;

namespace Islet.Core.Island;

public sealed record RecoveryStatus(bool TrayAvailable, bool HotkeyAvailable, bool ReducedMotion,
    bool HighContrast, IReadOnlyList<string> Warnings);
public readonly record struct PointerSnapshot(bool Available, int X, int Y, bool PrimaryDown, bool EscapeDown);
public sealed record DesktopSuppression(bool Fullscreen, bool PowerInactive, string? Reason)
{
    public bool Suppressed => Fullscreen || PowerInactive;
}
public sealed record IslandMenuItem(int Id, string Label, bool Checked = false, bool Enabled = true);

public interface IInteractiveIslandDesktop : IIslandDesktop
{
    RecoveryStatus Recovery { get; }
    void EnableRecovery();
    IReadOnlyList<IslandDisplay> ReadDisplays();
    void SelectDisplay(string id);
    PointerSnapshot ReadPointer();
    DesktopSuppression ReadSuppression();
    void SetClickThrough(bool enabled);
    void ObserveForeground(bool enabled);
    int OpenMenu(IReadOnlyList<IslandMenuItem> items);
}

public sealed record NativeIslandOptions
{
    public bool Minimal { get; init; }
    public int? ExitAfterSeconds { get; init; }
    public IslandStyle Style { get; init; } = IslandStyle.Island;
    public IslandState State { get; init; } = IslandState.Collapsed;
    public bool Hidden { get; init; }
    public bool Exercise { get; init; }
}
