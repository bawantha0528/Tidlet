using Islet.Core.Activities;
using Islet.Core.Capabilities;
using Islet.Core.Island;
using Islet.Core.Presentation;
using Islet.Core.Settings;

namespace Islet.Shell.Island;

public sealed partial class NativeIslandHost
{
    private readonly IMediaProvider? _media;
    private readonly FocusTimer _focus = new();
    private MediaReading _mediaReading = MediaReading.Waiting;
    private Task<MediaReading>? _mediaRead;
    private Task<MediaCommandResult>? _mediaCommand;
    private double _lastMediaRead = double.NegativeInfinity;
    private string? _mediaFeedback;
    private bool _fullDashboard;
    private int _mediaEpoch, _readEpoch;
    public CapabilityProbeResult? MediaCapability { get; private set; }
    private void InvalidateMedia() { (_media as IMediaVisibilityAware)?.SetVisible(false); _mediaEpoch++; _mediaReading=MediaReading.Waiting; _mediaFeedback=null; _lastMediaRead=double.NegativeInfinity; }
    public int MediaReads { get; private set; }
    public int MediaCommands { get; private set; }
    public MediaReading LastCompletedMediaStatus { get; private set; } = MediaReading.Waiting;
    public MediaReading MediaStatus => _mediaReading;
    public FocusSnapshot FocusStatus => _focus.Read(_now());
    public bool CompactNotch => Style == IslandStyle.Notch && !_fullDashboard && _page == MonitorPage.Overview && !_options.ExerciseDashboard;
    private IslandSize DesiredSize() => State == IslandState.Expanded && CompactNotch ? new(620,320) : IslandSize.For(Style,State);
    private bool MediaVisible => CompactNotch && State == IslandState.Expanded && !_suppressed;
    private ControlCenter? CreateControlCenter() => CompactNotch ? new(_mediaReading, FocusStatus,
        _mediaRead is { IsCompleted: false } || _mediaCommand is { IsCompleted: false }, _mediaFeedback) : null;
    private void PollMedia()
    {
        if (!MediaVisible) return;
        if (_mediaCommand is { IsCompleted: true })
        {
            try { _mediaFeedback = _mediaCommand.GetAwaiter().GetResult().Message; }
            catch { _mediaFeedback = "The player did not accept that control."; }
            _mediaCommand = null; _lastMediaRead = double.NegativeInfinity;
        }
        if (_mediaRead is { IsCompleted: true })
        {
            try { var result = _mediaRead.GetAwaiter().GetResult(); if (_readEpoch == _mediaEpoch) _mediaReading = LastCompletedMediaStatus = result; }
            catch { _mediaReading = new(MediaState.Unavailable, "Media unavailable", "Windows could not read this player."); }
            _mediaRead = null;
        }
        if (_media is null) { _mediaReading = new(MediaState.Unavailable,"Media unavailable","Media controls are not connected in this build."); return; }
        if (MediaCapability?.Availability == CapabilityAvailability.Unsupported) { _mediaReading = new(MediaState.Unavailable,"Media unavailable",MediaCapability.Explanation!); return; }
        if (_mediaRead is not null || _mediaCommand is not null || _now() - _lastMediaRead < 2000) return;
        _lastMediaRead = _now(); _readEpoch = _mediaEpoch; MediaReads++;
        _mediaRead = Task.Run(_media.ReadAsync);
    }
    private bool ExecuteControlCenterCommand(int command)
    {
        if (command is ControlCenterLayout.FullMonitor or ControlCenterLayout.ReturnToNotch)
        {
            InvalidateMedia();
            _fullDashboard = command == ControlCenterLayout.FullMonitor; _page = MonitorPage.Overview;
            BeginTransition(); return true;
        }
        if (command == ControlCenterLayout.FocusToggle || command == ControlCenterLayout.FocusReset)
        {
            if (!MediaVisible) return true;
            if (command == ControlCenterLayout.FocusToggle) _focus.Toggle(_now()); else _focus.Reset();
            Draw(); return true;
        }
        if (command is >= 500 and <= 502)
        {
            var action = (MediaCommand)(command - 500);
            if (MediaVisible && _media is not null && _mediaRead is null && _mediaCommand is null && _mediaReading.Allows(action))
            {
                var token = _mediaReading.SessionToken!; MediaCommands++;
                _mediaFeedback = "Sending control…";
                _mediaCommand = Task.Run(() => _media.ExecuteAsync(action, token));
                Draw();
            }
            return true;
        }
        return false;
    }
}
