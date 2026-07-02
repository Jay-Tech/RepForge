using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>One row in the history list.</summary>
public class HistorySessionItem(WorkoutSession session, string title, string summary)
{
    public WorkoutSession Session { get; } = session;

    public string Title { get; } = title;

    public string DateText { get; } =
        session.StartedUtc.ToLocalTime().ToString("ddd, MMM d  •  h:mm tt");

    public string Summary { get; } = summary;
}

public partial class HistoryViewModel : ViewModelBase
{
    private RepForgeDb? _db;

    public ObservableCollection<HistorySessionItem> Sessions { get; } = [];

    [ObservableProperty]
    private HistorySessionItem? _selectedSession;

    [ObservableProperty]
    private SessionDetailViewModel? _activeDetail;

    public HistoryViewModel()
    {
        if (!Design.IsDesignMode)
            _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        _db ??= await RepForgeDb.GetAsync();

        var templates = (await _db.GetTemplatesAsync()).ToDictionary(t => t.Id);
        var setsBySession = (await _db.GetAllSetsAsync())
            .GroupBy(s => s.SessionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Sessions.Clear();
        foreach (var session in await _db.GetCompletedSessionsAsync())
        {
            var title = session.TemplateId is { } tid && templates.TryGetValue(tid, out var t)
                ? t.Name : "Workout";
            var sets = setsBySession.GetValueOrDefault(session.Id) ?? [];
            var exerciseCount = sets.Select(s => s.ExerciseId).Distinct().Count();

            var parts = new List<string>();
            if (session.CompletedUtc is { } done)
            {
                var minutes = Math.Max(1, (int)(done - session.StartedUtc).TotalMinutes);
                parts.Add(minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min");
            }
            parts.Add(sets.Count == 1 ? "1 set" : $"{sets.Count} sets");
            parts.Add(exerciseCount == 1 ? "1 exercise" : $"{exerciseCount} exercises");

            Sessions.Add(new HistorySessionItem(session, title, string.Join("  •  ", parts)));
        }
    }

    partial void OnSelectedSessionChanged(HistorySessionItem? value)
    {
        if (value is null)
            return;
        _ = OpenAsync(value);
        SelectedSession = null;
    }

    private async Task OpenAsync(HistorySessionItem item)
    {
        if (_db is null)
            return;
        ActiveDetail = await SessionDetailViewModel.CreateAsync(
            _db, item, () => ActiveDetail = null);
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(HistorySessionItem item)
    {
        if (_db is null)
            return;
        await _db.DeleteSessionAsync(item.Session);
        Sessions.Remove(item);
    }
}
