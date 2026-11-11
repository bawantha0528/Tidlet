using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Islet.Windows.Bar;

public static class SystemDiagnostics
{
    public static async Task<string> CheckWifi(CancellationToken cancellation)
    {
        var wifi = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                adapter.OperationalStatus == OperationalStatus.Up)
            .Select(adapter => adapter.GetIPProperties()).ToArray();
        if (wifi.Length == 0) return "Windows reports no active Wi-Fi connection.";
        var connection = wifi.Select(adapter => new
        {
            Gateway = adapter.GatewayAddresses.Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any)),
            Address = adapter.UnicastAddresses.Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
        }).FirstOrDefault(adapter => adapter.Gateway is not null && adapter.Address is not null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        var router = CheckRouter(connection?.Gateway, timeout.Token);
        var internet = CheckInternet(connection?.Address, timeout.Token);
        var dns = CheckDns(timeout.Token);
        await Task.WhenAll(router, internet, dns);
        cancellation.ThrowIfCancellationRequested();
        return $"{router.Result}  ·  {internet.Result}  ·  {dns.Result}";
    }

    private static async Task<string> CheckRouter(IPAddress? gateway, CancellationToken cancellation)
    {
        if (gateway is null) return "Router: no IPv4 gateway reported";
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(gateway, TimeSpan.FromMilliseconds(1500), [], null, cancellation);
            return reply.Status == IPStatus.Success ? $"Router: {reply.RoundtripTime} ms" : "Router: no ping reply";
        }
        catch (Exception error) when (error is PingException or OperationCanceledException) { return "Router: no ping reply"; }
    }

    private static async Task<string> CheckInternet(IPAddress? address, CancellationToken cancellation)
    {
        if (address is null) return "Internet endpoint: no Wi-Fi IPv4 route";
        try
        {
            using var client = new TcpClient(new IPEndPoint(address, 0));
            await client.ConnectAsync(IPAddress.Parse("1.1.1.1"), 443, cancellation);
            return "Internet endpoint: reachable";
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException) { return "Internet endpoint: no response"; }
    }

    private static async Task<string> CheckDns(CancellationToken cancellation)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("example.com", cancellation);
            return addresses.Length > 0 ? "DNS: resolved" : "DNS: no address returned";
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException) { return "DNS: lookup failed"; }
    }

    public static string ReadStorageHealth(CancellationToken cancellation)
    {
        object? locator = null, service = null, rows = null;
        try
        {
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null || (locator = Activator.CreateInstance(type)) is null) return "Windows Storage health is unavailable.";
            service = Call(locator, "ConnectServer", ".", @"root\Microsoft\Windows\Storage", "", "", "", "", 128, Type.Missing);
            rows = Call(service, "ExecQuery", "SELECT FriendlyName, HealthStatus FROM MSFT_PhysicalDisk", "WQL", 16, Type.Missing);
            var statuses = new List<string>();
            var count = Math.Min(4, Convert.ToInt32(Get(rows, "Count")));
            for (var i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var row = Call(rows, "ItemIndex", i);
                try
                {
                    var name = Convert.ToString(WmiProperty(row, "FriendlyName"));
                    var status = Convert.ToString(WmiProperty(row, "HealthStatus")) switch
                    {
                        "0" => "Healthy", "1" => "Warning", "2" => "Unhealthy", _ => "Unknown"
                    };
                    statuses.Add($"{(string.IsNullOrWhiteSpace(name) ? "Physical drive" : name)}: {status}");
                }
                finally { Release(row); }
            }
            return statuses.Count == 0 ? "Windows did not report physical drive health." : string.Join("  ·  ", statuses);
        }
        catch (Exception error) when (error is COMException or TargetInvocationException or MissingMemberException or UnauthorizedAccessException)
        {
            return error.GetBaseException().HResult is unchecked((int)0x80041003) or unchecked((int)0x80070005)
                ? "Windows denied access to drive health." : "Windows Storage health is unavailable.";
        }
        finally { Release(rows); Release(service); Release(locator); }
    }

    public static string OpenStorageCleanup()
    {
        var uri = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? "ms-settings:storagerecommendations" : "ms-settings:storagesense";
        try
        {
            using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return "Opened Windows Storage for review.";
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { return "Windows could not open Storage settings."; }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    private static object Call(object target, string name, params object?[] arguments) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, arguments)!;

    private static object Get(object target, string name) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null)!;

    private static object WmiProperty(object row, string name)
    {
        var properties = Get(row, "Properties_");
        try
        {
            var property = Call(properties, "Item", name, 0);
            try { return Get(property, "Value"); }
            finally { Release(property); }
        }
        finally { Release(properties); }
    }
}
