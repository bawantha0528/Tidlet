namespace Islet.Core.Display;

public sealed record IslandProbeObservation(
    bool Visible, bool RequiredStylesPresent, bool Layered, bool ForegroundIsIsland,
    uint Dpi, int X, int Y, int Width, int Height);

public interface IIslandProbePlatform
{
    void Configure(nint window, double widthDip, double heightDip, double gapDip);
    IslandProbeObservation Observe(nint window);
}
