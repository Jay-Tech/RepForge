using System.Net.Sockets;
using RepForge.Data;

namespace RepForge.Sync;

/// <summary>Initiates a sync with the desktop (used by the phone).</summary>
public static class SyncClient
{
    public static async Task<string> SyncWithAsync(RepForgeDb db, string host)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var client = new TcpClient();
        await client.ConnectAsync(host.Trim(), SyncServer.Port, cts.Token);

        var stream = client.GetStream();
        var local = await db.GetSyncDataAsync();
        await SyncTransport.WriteFrameAsync(stream, local.Serialize(), cts.Token);

        var merged = SyncData.Deserialize(await SyncTransport.ReadFrameAsync(stream, cts.Token));
        await db.ApplySyncDataAsync(merged);

        SyncEvents.RaiseSyncCompleted();
        return merged.Summary();
    }
}
