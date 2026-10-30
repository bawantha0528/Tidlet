using System.Runtime.Versioning;
using Islet.Core.Activities;
using Windows.Media.Control;

namespace Islet.Windows.Media;

[SupportedOSPlatform("windows10.0.17763.0")]
internal sealed class WindowsMediaSessionBackend : IMediaSessionBackend
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _token;
    public async Task<MediaReading> ReadAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = _manager.GetCurrentSession();
        _session = null; _token = null;
        if (session is null) return new(MediaState.NoSession, "Not playing", "Play something in a media app");
        var properties = await session.TryGetMediaPropertiesAsync();
        if (!ReferenceEquals(session, _manager.GetCurrentSession())) return MediaReading.Waiting;
        var playback = session.GetPlaybackInfo();
        var controls = playback.Controls;
        var timeline = session.GetTimelineProperties();
        var duration = timeline.EndTime - timeline.StartTime;
        TimeSpan? position = duration > TimeSpan.Zero && duration <= TimeSpan.FromDays(7)
            ? TimeSpan.FromTicks(Math.Clamp((timeline.Position - timeline.StartTime).Ticks, 0, duration.Ticks)) : null;
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        _session = session; _token = Guid.NewGuid().ToString("N");
        return new(MediaState.Available, Clean(properties.Title, "Untitled media"), Clean(properties.Artist, "Windows media player"), _token,
            playing,
            controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled), controls.IsPreviousEnabled, controls.IsNextEnabled, position, position.HasValue ? duration : null);
    }
    public async Task<MediaCommandResult> ExecuteAsync(MediaCommand command, string sessionToken)
    {
        var session = _session;
        if (session is null || sessionToken != _token || !ReferenceEquals(session, _manager?.GetCurrentSession()))
            return new(false, "Player changed. Try again.");
        var playback = session.GetPlaybackInfo();
        var controls = playback.Controls;
        var success = command switch
        {
            MediaCommand.Previous when controls.IsPreviousEnabled => await session.TrySkipPreviousAsync(),
            MediaCommand.Next when controls.IsNextEnabled => await session.TrySkipNextAsync(),
            MediaCommand.TogglePlayback when controls.IsPlayPauseToggleEnabled => await session.TryTogglePlayPauseAsync(),
            MediaCommand.TogglePlayback when playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls.IsPauseEnabled => await session.TryPauseAsync(),
            MediaCommand.TogglePlayback when playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls.IsPlayEnabled => await session.TryPlayAsync(),
            _ => false
        };
        return new(success, success ? "Control sent to player" : "This player did not accept the control");
    }
    private static string Clean(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return new string(value.Where(c => !char.IsControl(c)).Take(160).ToArray());
    }
}
