using System.Buffers.Binary;
using System.Text.Json;
using Islet.Core.Activities;

namespace Islet.Windows.Media;

public sealed record MediaWorkerRequest(int Version = 1, MediaCommand? Command = null, string? SessionToken = null);
public sealed record MediaWorkerInfo(int ProcessId, int Reads, int Commands, string[] MediaAssemblies, string[] WpfAssemblies);
public sealed record MediaWorkerResponse(int Version, MediaReading? Reading, MediaCommandResult? Result, MediaWorkerInfo Info);

/// <summary>Length-prefixed UTF-8 JSON on inherited pipes. Never allocate an unchecked peer-supplied length.</summary>
public static class MediaWorkerProtocol
{
    public const int MaxPacketBytes = 8192;
    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> TypeInfo<T>() =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(MediaWorkerJson.Default.GetTypeInfo(typeof(T))
            ?? throw new NotSupportedException("Unregistered media packet type."));
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, TypeInfo<T>());
        if (bytes.Length is < 1 or > MaxPacketBytes) throw new InvalidDataException("Invalid media packet length.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
    {
        var header = new byte[4];
        var first = await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaxPacketBytes) throw new InvalidDataException("Invalid media packet length.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(payload, TypeInfo<T>()) ?? throw new InvalidDataException("Empty media packet.");
    }
}
