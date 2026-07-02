using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

public partial class ActiveSessionViewModel : ViewModelBase
{
    private readonly RepForgeDb _db;
    private readonly WorkoutSession _session;
    private readonly Action _close;

    public string Title { get; }

    public string StartedText { get; }

    public ObservableCollection<SessionExerciseViewModel> Exercises { get; } = [];

    private ActiveSessionViewModel(RepForgeDb db, WorkoutSession session, string title, Action close)
    {
        _db = db;
        _session = session;
        _close = close;
        Title = title;
        StartedText = $"Started {session.StartedUtc.ToLocalTime():t}";
    }

    public static async Task<ActiveSessionViewModel> CreateAsync(
        RepForgeDb db, WorkoutSession session, Action close)
    {
        var exercisesById = (await db.GetExercisesAsync()).ToDictionary(e => e.Id);
        var title = "Workout";
        var items = new List<SessionExerciseViewModel>();

        if (session.TemplateId is { } templateId)
        {
            if (await db.GetTemplateAsync(templateId) is { } template)
                title = template.Name;

            foreach (var row in await db.GetTemplateExercisesAsync(templateId))
            {
                var name = exercisesById.TryGetValue(row.ExerciseId, out var ex)
                    ? ex.Name : "(deleted exercise)";
                items.Add(new SessionExerciseViewModel(
                    db, session.Id, row.ExerciseId, name,
                    row.TargetSets, row.TargetReps, row.TargetWeight));
            }
        }

        // Re-attach sets already logged (when resuming an interrupted session).
        foreach (var set in await db.GetSetsForSessionAsync(session.Id))
        {
            var item = items.FirstOrDefault(x => x.ExerciseId == set.ExerciseId);
            if (item is null)
            {
                var name = exercisesById.TryGetValue(set.ExerciseId, out var ex)
                    ? ex.Name : "(deleted exercise)";
                item = new SessionExerciseViewModel(db, session.Id, set.ExerciseId, name, 0, 0, null);
                items.Add(item);
            }
            item.AddExisting(set);
        }

        foreach (var item in items)
        {
            var previous = await db.GetPreviousSetsAsync(item.ExerciseId, session.Id);
            if (previous.Count > 0)
            {
                item.LastTime = "Last time:  " + string.Join(", ", previous.Select(s =>
                    s.Weight > 0 ? $"{s.Weight:0.##}×{s.Reps}" : $"{s.Reps} reps"));
            }
        }

        var vm = new ActiveSessionViewModel(db, session, title, close);
        foreach (var item in items)
            vm.Exercises.Add(item);
        return vm;
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        _session.CompletedUtc = DateTime.UtcNow;
        await _db.SaveSessionAsync(_session);
        _close();
    }

    [RelayCommand]
    private async Task DiscardAsync()
    {
        await _db.DeleteSessionAsync(_session);
        _close();
    }
}
