using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RepForge.Data;

namespace RepForge.Sync;

/// <summary>
/// Listens for sync requests from the phone (desktop only). Protocol: the client
/// sends its full dataset, the server merges it with its own, applies the result
/// locally, and sends the merged dataset back.
/// </summary>
public sealed class SyncServer(RepForgeDb db)
{
    public const int Port = 5730;

    private readonly TcpListener _listener = new(IPAddress.Any, Port);

    public void Start()
    {
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var peer = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using (client)
            {
                var stream = client.GetStream();
                var remote = SyncData.Deserialize(await SyncTransport.ReadFrameAsync(stream, cts.Token));
                var local = await db.GetSyncDataAsync();
                var merged = SyncMerger.Merge(local, remote);
                await db.ApplySyncDataAsync(merged);
                await SyncTransport.WriteFrameAsync(stream, merged.Serialize(), cts.Token);

                SyncEvents.RaiseActivity(
                    $"{DateTime.Now:h:mm tt} — synced with {peer}  ({merged.Summary()})");
                SyncEvents.RaiseSyncCompleted();
            }
        }
        catch (Exception ex)
        {
            SyncEvents.RaiseActivity($"{DateTime.Now:h:mm tt} — sync with {peer} failed: {ex.Message}");
        }
    }

    /// <summary>IPv4 addresses of this machine, for showing the user what to type on the phone.</summary>
    public static string GetLocalAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Distinct()
            .ToList();
        return addresses.Count > 0 ? string.Join("   or   ", addresses) : "(no network)";
    }
}
