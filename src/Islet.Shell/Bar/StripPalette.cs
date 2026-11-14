using System.Globalization;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

internal sealed class StripPalette
{
    private readonly Dictionary<StripColor, Brush> _brushes = [];
    private readonly Dictionary<StripColor, Pen> _pens = [];

    public StripPalette(IReadOnlyDictionary<StripColor, string> colors)
    {
        foreach (var (role, hex) in colors)
        {
            var brush = PopupPalette.Solid(int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            _brushes.Add(role, brush);
            _pens.Add(role, PopupPalette.Stroke(brush, role switch { StripColor.Border => 1, StripColor.Track => 2.5, _ => 2 }));
        }
    }

    public Brush? Get(StripColor role) => _brushes.GetValueOrDefault(role);
    public Pen? Pen(StripColor role) => _pens.GetValueOrDefault(role);
}
