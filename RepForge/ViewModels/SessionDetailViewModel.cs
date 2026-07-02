using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;

namespace RepForge.ViewModels;

/// <summary>Read-only exercise group within a past session.</summary>
public class HistoryExerciseGroup(string name, IReadOnlyList<string> sets)
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Sets { get; } = sets;
}

public partial class SessionDetailViewModel : ViewModelBase
{
    private readonly Action _close;

    public string Title { get; }

    public string DateText { get; }

    public ObservableCollection<HistoryExerciseGroup> Groups { get; } = [];

    private SessionDetailViewModel(HistorySessionItem item, Action close)
    {
        _close = close;
        Title = item.Title;
        DateText = $"{item.DateText}  •  {item.Summary}";
    }

    public static async Task<SessionDetailViewModel> CreateAsync(
        RepForgeDb db, HistorySessionItem item, Action close)
    {
        var vm = new SessionDetailViewModel(item, close);
        var exercisesById = (await db.GetExercisesAsync()).ToDictionary(e => e.Id);

        var groups = (await db.GetSetsForSessionAsync(item.Session.Id))
            .GroupBy(s => s.ExerciseId)
            .OrderBy(g => g.Min(s => s.LoggedUtc));

        foreach (var group in groups)
        {
            var name = exercisesById.TryGetValue(group.Key, out var ex)
                ? ex.Name : "(deleted exercise)";
            var lines = group
                .OrderBy(s => s.SetNumber)
                .Select(s => $"Set {s.SetNumber}:   {Models.SetFormat.Line(s)}")
                .ToList();
            vm.Groups.Add(new HistoryExerciseGroup(name, lines));
        }

        return vm;
    }

    [RelayCommand]
    private void Close() => _close();
}
