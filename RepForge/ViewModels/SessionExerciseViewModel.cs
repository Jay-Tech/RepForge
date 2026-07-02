using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>One exercise within the active session: its targets, logged sets, and input row.</summary>
public partial class SessionExerciseViewModel : ObservableObject
{
    private readonly RepForgeDb _db;
    private readonly int _sessionId;

    public int ExerciseId { get; }

    public string ExerciseName { get; }

    /// <summary>e.g. "Target:  3 × 10 @ 185" — empty for exercises without a plan.</summary>
    public string TargetSummary { get; }

    public int TargetSets { get; }

    public ObservableCollection<LoggedSetViewModel> Sets { get; } = [];

    [ObservableProperty]
    private decimal? _reps;

    [ObservableProperty]
    private decimal? _weight;

    [ObservableProperty]
    private string _progress = string.Empty;

    public SessionExerciseViewModel(
        RepForgeDb db, int sessionId, int exerciseId, string exerciseName,
        int targetSets, int targetReps, double? targetWeight)
    {
        _db = db;
        _sessionId = sessionId;
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        TargetSets = targetSets;

        if (targetSets > 0)
        {
            TargetSummary = targetWeight is { } w
                ? $"Target:  {targetSets} × {targetReps} @ {w:0.##}"
                : $"Target:  {targetSets} × {targetReps}";
            _reps = targetReps;
            _weight = targetWeight is null ? null : (decimal)targetWeight;
        }
        else
        {
            TargetSummary = string.Empty;
        }

        UpdateProgress();
    }

    /// <summary>Attaches an already-persisted set when resuming a session.</summary>
    public void AddExisting(SetEntry row)
    {
        Sets.Add(new LoggedSetViewModel(row));
        Reps = row.Reps;
        Weight = row.Weight > 0 ? (decimal)row.Weight : null;
        UpdateProgress();
    }

    [RelayCommand]
    private async Task LogSetAsync()
    {
        if (Reps is not { } reps || reps <= 0)
            return;

        var row = new SetEntry
        {
            SessionId = _sessionId,
            ExerciseId = ExerciseId,
            SetNumber = Sets.Count + 1,
            Reps = (int)reps,
            Weight = Weight is { } w ? (double)w : 0,
        };
        await _db.SaveSetAsync(row);
        Sets.Add(new LoggedSetViewModel(row));
        UpdateProgress();
    }

    [RelayCommand]
    private async Task DeleteSetAsync(LoggedSetViewModel set)
    {
        await _db.DeleteSetAsync(set.Row);
        Sets.Remove(set);

        for (var i = 0; i < Sets.Count; i++)
        {
            if (Sets[i].Row.SetNumber == i + 1)
                continue;
            Sets[i].Row.SetNumber = i + 1;
            await _db.SaveSetAsync(Sets[i].Row);
            Sets[i].Refresh();
        }

        UpdateProgress();
    }

    private void UpdateProgress() =>
        Progress = TargetSets > 0 ? $"{Sets.Count} / {TargetSets} sets" : $"{Sets.Count} sets";
}
