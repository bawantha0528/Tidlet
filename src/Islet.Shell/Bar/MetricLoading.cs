using Islet.Core.Bar;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

internal static class MetricLoading
{
    internal static (string Title, string Detail) For(BarModule module) => module switch
    {
        BarModule.Cpu => ("Measuring CPU usage", "Windows needs two counter samples to calculate CPU usage."),
        BarModule.Ram => ("Reading memory usage", "Tidlet is checking physical memory and available details."),
        BarModule.Gpu => ("Measuring GPU usage", "Tidlet is checking graphics adapters and their Windows counters."),
        BarModule.Network => ("Measuring network speeds", "Upload and download rates appear after the next traffic sample."),
        BarModule.Disk => ("Measuring disk activity", "Tidlet is checking drive capacity and measuring transfer rates."),
        BarModule.Battery => ("Checking battery status", "Tidlet is looking for a battery and reading its charge level."),
        BarModule.Sensors => ("Checking sensor availability", "Tidlet is checking whether this device exposes supported sensors."),
        _ => ("Reading Windows metrics", "Tidlet is waiting for the first sample.")
    };

    internal const string Previous = "Previous reading · refreshing";
    internal const string CollectingHistory = "Collecting samples for the chart";

    internal static bool HasPartialDetails(BarModule module, PopupSample sample) => module switch
    {
        BarModule.Disk => sample.Disk?.Volumes.Any(static volume => volume.Valid) == true,
        BarModule.Gpu => sample.Gpu?.Adapters.Length > 0,
        BarModule.Network => sample.Network?.Adapters.Length > 0,
        _ => false
    };
}
