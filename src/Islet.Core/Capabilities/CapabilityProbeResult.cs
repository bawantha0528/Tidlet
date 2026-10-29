namespace Islet.Core.Capabilities;

public enum CapabilityAvailability
{
    Supported,
    Unsupported,
    NeedsHelper
}

/// <summary>Startup support only; sample validity is a separate future contract.</summary>
public sealed record CapabilityProbeResult
{
    private CapabilityProbeResult(CapabilityAvailability availability, string? reasonCode, string? explanation)
    {
        Availability = availability;
        ReasonCode = reasonCode;
        Explanation = explanation;
    }

    public CapabilityAvailability Availability { get; }
    public string? ReasonCode { get; }
    public string? Explanation { get; }

    public static CapabilityProbeResult Supported { get; } =
        new(CapabilityAvailability.Supported, null, null);

    public static CapabilityProbeResult Unsupported(string reasonCode, string explanation) =>
        WithReason(CapabilityAvailability.Unsupported, reasonCode, explanation);

    public static CapabilityProbeResult NeedsHelper(string reasonCode, string explanation) =>
        WithReason(CapabilityAvailability.NeedsHelper, reasonCode, explanation);

    private static CapabilityProbeResult WithReason(
        CapabilityAvailability availability, string reasonCode, string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        return new(availability, reasonCode.Trim(), explanation.Trim());
    }
}
