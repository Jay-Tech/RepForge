using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Sync;

namespace RepForge.ViewModels;

public partial class SyncViewModel : ViewModelBase
{
    private const string HostSettingKey = "sync.desktopHost";

    private RepForgeDb? _db;

    public bool IsPhone { get; } = OperatingSystem.IsAndroid();

    public bool IsDesktop => !IsPhone;

    public string ListeningInfo { get; } = OperatingSystem.IsAndroid()
        ? string.Empty
        : $"This computer:   {SyncServer.GetLocalAddresses()}   (port {SyncServer.Port})";

    [ObservableProperty]
    private string _desktopAddress = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public SyncViewModel()
    {
        if (Design.IsDesignMode)
            return;

        SyncEvents.Activity += message =>
            Dispatcher.UIThread.Post(() => Status = message);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db = await RepForgeDb.GetAsync();
        DesktopAddress = await _db.GetSettingAsync(HostSettingKey) ?? string.Empty;
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (_db is null)
            return;

        var host = DesktopAddress.Trim();
        if (host.Length == 0)
        {
            Status = "Enter the desktop's address first (shown on its Sync tab).";
            return;
        }

        Status = "Syncing…";
        try
        {
            var summary = await SyncClient.SyncWithAsync(_db, host);
            await _db.SetSettingAsync(HostSettingKey, host);
            Status = $"Done at {DateTime.Now:h:mm tt}  —  {summary}";
        }
        catch (Exception ex)
        {
            Status = $"Sync failed: {ex.Message}";
        }
    }
}
