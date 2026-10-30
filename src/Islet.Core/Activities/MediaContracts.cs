using Islet.Core.Capabilities;

namespace Islet.Core.Activities;

public enum MediaState { Waiting, NoSession, Available, Unavailable }
public enum MediaCommand { Previous, TogglePlayback, Next }
public sealed record MediaReading(MediaState State, string Title, string Subtitle, string? SessionToken = null,
    bool Playing = false, bool CanToggle = false, bool CanPrevious = false, bool CanNext = false,
    TimeSpan? Position = null, TimeSpan? Duration = null)
{
    public static MediaReading Waiting { get; } = new(MediaState.Waiting, "Looking for a player", "Windows media sessions");
    public bool Allows(MediaCommand command) => State == MediaState.Available && SessionToken is not null && command switch
    { MediaCommand.Previous => CanPrevious, MediaCommand.TogglePlayback => CanToggle, MediaCommand.Next => CanNext, _ => false };
}
public sealed record MediaCommandResult(bool Success, string Message);
public interface IMediaProvider
{
    CapabilityProbeResult Probe();
    Task<MediaReading> ReadAsync();
    Task<MediaCommandResult> ExecuteAsync(MediaCommand command, string sessionToken);
}

/// <summary>UI lifecycle demand; hiding must stop pending activity without blocking the UI thread.</summary>
public interface IMediaVisibilityAware
{
    void SetVisible(bool visible);
}
