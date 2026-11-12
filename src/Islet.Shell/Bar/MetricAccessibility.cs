using Islet.Core.Bar;
using Islet.Core.Popups;

namespace Islet.Shell.Bar;

internal static class MetricAccessibility
{
    public static string Describe(BarModule module, PopupSample? sample, SampleRing? history, bool previous)
    {
        if (sample is null) return MetricLoading.For(module).Title;

        var readings = new List<string>();
        if (previous) readings.Add(MetricLoading.Previous);
        void Number(string label, double? value, string unit)
        {
            if (value is double number && double.IsFinite(number))
                readings.Add($"{label}: {number:0.##} {unit}");
        }

        switch (module)
        {
            case BarModule.Cpu:
                Number("CPU usage", sample.Cpu, "percent");
                Number("System", sample.System, "percent");
                Number("User", sample.User, "percent");
                Number("Idle", sample.Idle, "percent");
                Number("Frequency", sample.FrequencyGhz, "gigahertz");
                Number("Last CPU temperature", sample.LastCpuPackageCelsius, "degrees Celsius");
                if (sample.CpuTemperatureSampledAt is {} sampledAt)
                    readings.Add($"CPU temperature checked at {sampledAt.ToLocalTime():t}");
                for (int i = 0; i < sample.Cores.Length; i++)
                    Number($"Core {i + 1}", sample.Cores[i], "percent");
                break;
            case BarModule.Ram when sample.Memory is { Valid: true } memory:
                Number("Memory used", memory.UsedPercent, "percent");
                Number("Memory available", memory.Available / 1073741824.0, "GiB");
                Number("Total memory", memory.Total / 1073741824.0, "GiB");
                readings.Add($"Memory pressure: {memory.PressureName}");
                break;
            case BarModule.Network when sample.Network is {} network:
                Number("Download", network.Download, "bytes per second");
                Number("Upload", network.Upload, "bytes per second");
                Number("Latency", network.Latency, "milliseconds");
                Number("Jitter", network.Jitter, "milliseconds");
                foreach (var adapter in network.Adapters) readings.Add($"Adapter: {adapter.Name}");
                break;
            case BarModule.Gpu when sample.Gpu is {} gpu:
                foreach (var adapter in gpu.Adapters)
                {
                    Number(adapter.Name + " usage", adapter.Usage, "percent");
                    Number(adapter.Name + " temperature", adapter.TemperatureCelsius, "degrees Celsius");
                }
                break;
            case BarModule.Disk when sample.Disk is {} disk:
                foreach (var volume in disk.Volumes)
                    Number(volume.Name + " used", volume.UsedPercent, "percent");
                foreach (var drive in disk.Disks)
                {
                    Number(drive.Name + " read", drive.Read, "bytes per second");
                    Number(drive.Name + " write", drive.Write, "bytes per second");
                }
                break;
            case BarModule.Battery when sample.Battery is {} battery:
                Number("Charge", battery.Reading.Percent, "percent");
                Number("Battery health", battery.Reading.HealthPercent, "percent");
                Number("Battery temperature", battery.TemperatureCelsius, "degrees Celsius");
                if (battery.Reading.ChargingStatus != Islet.Core.Metrics.BatteryChargingStatus.Unknown)
                    readings.Add($"Battery state: {battery.Reading.ChargingStatus}");
                break;
            case BarModule.Sensors when sample.Sensors is {} sensors:
                foreach (var sensor in sensors.Items)
                    Number($"{sensor.Hardware} {sensor.Name}", sensor.Value, sensor.Unit);
                break;
        }

        if (history is { Count: > 0 } && module is BarModule.Cpu or BarModule.Ram)
        {
            double low = 100, high = 0;
            for (int i = 0; i < history.Count; i++)
            {
                double value = history.At(i).Value;
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
            readings.Add($"Usage history chart: {history.Count} readings, {low:0.#} to {high:0.#} percent");
        }

        return readings.Count == 0 ? sample.Explanation : string.Join(". ", readings);
    }
}
