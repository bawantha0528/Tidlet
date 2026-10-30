namespace Islet.Core.Activities;

public enum FocusState { Ready, Running, Paused, Complete }
public sealed record FocusSnapshot(FocusState State, TimeSpan Remaining, TimeSpan Duration)
{
    public string Display => $"{(int)Math.Ceiling(Remaining.TotalSeconds) / 60:00}:{(int)Math.Ceiling(Remaining.TotalSeconds) % 60:00}";
}

/// <summary>Monotonic deadline, evaluated on demand. No thread, timer or platform dependency.</summary>
public sealed class FocusTimer
{
    private readonly TimeSpan _duration;
    private double _remaining, _deadline;
    private FocusState _state;
    public FocusTimer(TimeSpan? duration = null)
    {
        _duration = duration ?? TimeSpan.FromMinutes(25);
        if (_duration <= TimeSpan.Zero || _duration > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(duration));
        _remaining = _duration.TotalMilliseconds;
    }
    public FocusSnapshot Read(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        var remaining = _state == FocusState.Running ? Math.Clamp(_deadline - milliseconds, 0, _duration.TotalMilliseconds) : _remaining;
        if (_state == FocusState.Running && remaining == 0) { _state = FocusState.Complete; _remaining = 0; }
        return new(_state, TimeSpan.FromMilliseconds(remaining), _duration);
    }
    public void Toggle(double milliseconds)
    {
        var snapshot = Read(milliseconds);
        if (snapshot.State == FocusState.Running) { _remaining = snapshot.Remaining.TotalMilliseconds; _state = FocusState.Paused; }
        else { if (_state == FocusState.Complete) _remaining = _duration.TotalMilliseconds; _deadline = milliseconds + _remaining; _state = FocusState.Running; }
    }
    public void Reset() { _state = FocusState.Ready; _remaining = _duration.TotalMilliseconds; }
}
