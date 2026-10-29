using Islet.Core.Settings;

namespace Islet.Core.Profiles;

public sealed record ProfileResolution(
    string ActiveProfileName,
    IslandStyle ResolvedStyle,
    bool SuppressAlerts,
    string Reason);

public sealed class ProfileManager(IReadOnlyList<ProfileRule> profiles)
{
    private string? _manualOverride;
    private string? _pendingCandidate;
    private DateTime _candidateStartTime;
    private string _currentProfile = "Work";
    private const double DebounceSeconds = 2.0;

    public void SetManualOverride(string? profileName)
    {
        _manualOverride = profileName;
    }

    public ProfileResolution Evaluate(
        bool isFullscreenAppActive,
        string? foregroundProcessName,
        DateTime now)
    {
        // 1. Manual override takes highest priority
        if (!string.IsNullOrWhiteSpace(_manualOverride))
        {
            var matched = profiles.FirstOrDefault(p => string.Equals(p.Name, _manualOverride, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
                return new(matched.Name, matched.Style, matched.SuppressAlerts, "Set manually by user.");
        }

        // 2. Rule evaluation
        string targetProfile = "Work";
        string reason = "Default productivity profile.";

        if (isFullscreenAppActive)
        {
            targetProfile = "Gaming";
            reason = "Fullscreen application active.";
        }
        else if (!string.IsNullOrWhiteSpace(foregroundProcessName))
        {
            var rule = profiles.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.TriggerProcessName)
                && string.Equals(p.TriggerProcessName, foregroundProcessName, StringComparison.OrdinalIgnoreCase));

            if (rule is not null)
            {
                targetProfile = rule.Name;
                reason = $"Triggered by process '{foregroundProcessName}'.";
            }
        }

        // 3. Debounce
        if (!string.Equals(targetProfile, _currentProfile, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(targetProfile, _pendingCandidate, StringComparison.OrdinalIgnoreCase))
            {
                _pendingCandidate = targetProfile;
                _candidateStartTime = now;
            }
            else if ((now - _candidateStartTime).TotalSeconds >= DebounceSeconds)
            {
                _currentProfile = targetProfile;
                _pendingCandidate = null;
            }
        }
        else
        {
            _pendingCandidate = null;
        }

        var active = profiles.FirstOrDefault(p => string.Equals(p.Name, _currentProfile, StringComparison.OrdinalIgnoreCase))
            ?? new ProfileRule { Name = "Work", Style = IslandStyle.Island, SuppressAlerts = false };

        return new(active.Name, active.Style, active.SuppressAlerts, reason);
    }
}
