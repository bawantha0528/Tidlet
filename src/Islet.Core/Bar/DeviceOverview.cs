namespace Islet.Core.Bar;
public sealed record DeviceFact(string Label,string Value);
public sealed record DeviceOverview(string Model,string OperatingSystem,DeviceFact[] Facts,string[] Limitations);
