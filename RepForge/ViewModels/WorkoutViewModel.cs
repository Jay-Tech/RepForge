using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

public partial class WorkoutViewModel : ViewModelBase
{
    private RepForgeDb? _db;

    public ObservableCollection<WorkoutTemplate> Templates { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private WorkoutTemplate? _selectedTemplate;

    [ObservableProperty]
    private ActiveSessionViewModel? _activeSession;

    public WorkoutViewModel()
    {
        if (!Design.IsDesignMode)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db = await RepForgeDb.GetAsync();

        if (await _db.GetActiveSessionAsync() is { } openSession)
            ActiveSession = await ActiveSessionViewModel.CreateAsync(_db, openSession, EndSession);

        await RefreshTemplatesAsync();
    }

    /// <summary>Reloads the plan list; called when the tab becomes visible so new plans show up.</summary>
    public async Task RefreshTemplatesAsync()
    {
        if (_db is null)
            return;

        var previousId = SelectedTemplate?.Id;
        Templates.Clear();
        foreach (var template in await _db.GetTemplatesAsync())
            Templates.Add(template);
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == previousId);
    }

    private bool CanStart => SelectedTemplate is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (_db is null || SelectedTemplate is not { } template)
            return;

        var session = new WorkoutSession { TemplateId = template.Id };
        await _db.SaveSessionAsync(session);
        ActiveSession = await ActiveSessionViewModel.CreateAsync(_db, session, EndSession);
    }

    private void EndSession() => ActiveSession = null;
}
