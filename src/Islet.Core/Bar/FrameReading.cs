using System.Globalization;

namespace Islet.Core.Bar;

public readonly record struct FrameReading(double? Milliseconds, string? Status, bool Unavailable = false);

public sealed class PresentMonFrames
{
    private readonly Dictionary<string, (double Total, int Count)> _chains = new();
    private int _chainIndex = -1;
    private int _durationIndex = -1;

    public void Add(string line)
    {
        var fields = line.Split(',');
        if (fields[0] == "Application")
        {
            _chainIndex = Array.IndexOf(fields, "SwapChainAddress");
            _durationIndex = Array.IndexOf(fields, "DisplayedTime");
            return;
        }

        if (_chainIndex < 0 || _durationIndex < 0 || fields.Length <= Math.Max(_chainIndex, _durationIndex)
            || !double.TryParse(fields[_durationIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
            || !double.IsFinite(duration) || duration is < 0.1 or > 10000)
            return;

        var chain = fields[_chainIndex];
        if (!_chains.TryGetValue(chain, out var current) && _chains.Count >= 32)
            return;

        _chains[chain] = (current.Total + duration, current.Count + 1);
    }

    public double? TakeInterval()
    {
        var best = (Total: 0d, Count: 0);
        foreach (var sample in _chains.Values)
            if (sample.Count > best.Count)
                best = sample;

        _chains.Clear();
        return best.Count == 0 ? null : best.Total / best.Count;
    }
}
