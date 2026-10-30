using Islet.Core.Settings;

namespace Islet.Core.Island;

public readonly record struct IslandSize(double Width, double Height)
{
    public static IslandSize For(IslandStyle style, IslandState state) => (style, state) switch
    {
        (IslandStyle.Island, IslandState.Collapsed) => new(220, 36),
        (IslandStyle.Notch, IslandState.Collapsed) => new(240, 42),
        (IslandStyle.Bar, IslandState.Collapsed) => new(480, 28),
        (IslandStyle.Notch, IslandState.Hover) => new(380, 110),
        (_, IslandState.Hover) => new(360, 100),
        (IslandStyle.Bar, IslandState.Expanded) => new(860, 560),
        (IslandStyle.Notch, IslandState.Expanded) => new(860, 560),
        _ => new(860, 560)
    };
}

public sealed class IslandTransition(IslandSize initial)
{
    private IslandSize _from = initial, _to = initial;
    private double _started, _duration;
    public bool Active { get; private set; }

    public IslandSize At(double now)
    {
        if (!Active) return _to;
        var t = Math.Clamp((now - _started) / _duration, 0, 1);
        if (t >= 1) { Active = false; return _to; }
        var eased = 1 - Math.Pow(1 - t, 3);
        return new(_from.Width + (_to.Width - _from.Width) * eased,
            _from.Height + (_to.Height - _from.Height) * eased);
    }

    public void Begin(IslandSize target, double now, bool reducedMotion = false)
    {
        _from = At(now); _to = target; _started = now; _duration = reducedMotion ? 0 : 250;
        Active = _duration > 0 && _from != _to;
    }

    public void Finish() { _from = _to; Active = false; }
}

public static class FullscreenGeometry
{
    public static bool CoversMonitor(PixelRect foreground, PixelRect monitor, bool hasCaption,
        bool minimized, bool isDesktop)
    {
        if (minimized || isDesktop || hasCaption || foreground.Width <= 0 || foreground.Height <= 0) return false;
        return foreground.X <= monitor.X && foreground.Y <= monitor.Y
            && (long)foreground.X + foreground.Width >= (long)monitor.X + monitor.Width
            && (long)foreground.Y + foreground.Height >= (long)monitor.Y + monitor.Height;
    }
}
