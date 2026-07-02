using System.Buffers.Binary;

namespace RepForge.Sync;

/// <summary>Length-prefixed frames over a plain TCP stream (4-byte little-endian length + UTF-8 JSON).</summary>
internal static class SyncTransport
{
    private const int MaxPayloadBytes = 64 * 1024 * 1024;

    public static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaxPayloadBytes)
            throw new InvalidDataException($"Bad sync frame length: {length}");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);
        return payload;
    }
}
