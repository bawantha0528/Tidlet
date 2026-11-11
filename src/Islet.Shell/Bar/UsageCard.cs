using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

internal static class UsageCard
{
    private static readonly Typeface Face = new("Segoe UI Variable, Segoe UI");
    private static readonly UsagePeriod[] Periods = Enum.GetValues<UsagePeriod>();

    internal static Rect Option(Rect card, UsagePeriod period)
    {
        var width = (card.Width - 24) / 3;
        return new(card.Left + 12 + width * (int)period, card.Top + 35, width - 3, 25);
    }

    internal static void Draw(DrawingContext drawing, Rect card, DashboardPalette palette, double scale, UsageSnapshot usage)
    {
        drawing.DrawRoundedRectangle(palette.Card, palette.CardBorder, card, 15, 15);
        Text("Data usage", card.Left + 12, card.Top + 10, 12, palette.Text);
        foreach (var period in Periods)
        {
            var rect = Option(card, period);
            var selected = usage.Period == period;
            if (selected) DashboardPalette.DrawSelection(drawing, rect);
            else drawing.DrawRoundedRectangle(palette.Metrics.Surface, palette.CardBorder, rect, 5, 5);
            Center(period.ToString(), rect.Top + rect.Height / 2, 10, selected ? Brushes.White : palette.Text, rect.Left + rect.Width / 2);
        }
        Center(usage.HasSamples ? Size(usage.Bytes.Total) : "No data yet", card.Top + 87, 20, palette.Text, card.Left + card.Width / 2);
        var label = usage.Period switch { UsagePeriod.Day => "Today", UsagePeriod.Week => "This week · Mon–Sun", _ => "This month" };
        Center(label, card.Top + 112, 10, palette.Muted, card.Left + card.Width / 2);
        if (usage.HasSamples)
        {
            Row("Downloaded", usage.Bytes.Download, 128);
            Row("Uploaded", usage.Bytes.Upload, 148);
        }
        Center("Recorded while monitoring", card.Top + 175, 9, palette.Muted, card.Left + card.Width / 2);

        void Row(string name, ulong value, double y)
        {
            Text(name, card.Left + 12, card.Top + y, 10, palette.Muted);
            var number = MonitorText.Format(Size(value), Face, 10, palette.Text, scale);
            drawing.DrawText(number, MonitorText.PixelAligned(card.Right - 12 - number.Width, card.Top + y, scale));
        }
        void Text(string value, double x, double y, double size, Brush brush) =>
            drawing.DrawText(MonitorText.Format(value, Face, size, brush, scale), MonitorText.PixelAligned(x, y, scale));
        void Center(string value, double y, double size, Brush brush, double x)
        {
            var text = MonitorText.Format(value, Face, size, brush, scale);
            drawing.DrawText(text, MonitorText.Centered(text, new Point(x, y), scale));
        }
    }

    private static string Size(ulong bytes) => bytes switch
    {
        >= 1UL << 40 => $"{bytes / (double)(1UL << 40):0.00} TiB",
        >= 1UL << 30 => $"{bytes / (double)(1UL << 30):0.00} GiB",
        >= 1UL << 20 => $"{bytes / (double)(1UL << 20):0.00} MiB",
        >= 1UL << 10 => $"{bytes / 1024.0:0.00} KiB",
        _ => $"{bytes} B"
    };
}
