using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>One exercise within the active session: its targets, logged entries, and input row.</summary>
public partial class SessionExerciseViewModel : ObservableObject
{
    private readonly RepForgeDb _db;
    private readonly Guid _sessionId;

    public Guid ExerciseId { get; }

    public string ExerciseName { get; }

    public bool IsCardio { get; }

    public bool IsStrength => !IsCardio;

    /// <summary>e.g. "Target:  3 × 10 @ 185" or "Target:  2.5 · 20 min" — empty without a plan.</summary>
    public string TargetSummary { get; }

    public int TargetSets { get; }

    public ObservableCollection<LoggedSetViewModel> Sets { get; } = [];

    // Strength inputs
    [ObservableProperty]
    private decimal? _reps;

    [ObservableProperty]
    private decimal? _weight;

    // Cardio inputs
    [ObservableProperty]
    private decimal? _distance;

    [ObservableProperty]
    private decimal? _minutes;

    [ObservableProperty]
    private int _laps;

    [ObservableProperty]
    private string _progress = string.Empty;

    /// <summary>What was lifted for this exercise last session, e.g. "Last time:  185×10, 185×9".</summary>
    [ObservableProperty]
    private string _lastTime = string.Empty;

    public SessionExerciseViewModel(RepForgeDb db, Guid sessionId, Exercise exercise, TemplateExercise? target)
    {
        _db = db;
        _sessionId = sessionId;
        ExerciseId = exercise.Id;
        ExerciseName = exercise.Name;
        IsCardio = exercise.Type == ExerciseType.Cardio;

        if (IsCardio)
        {
            TargetSets = 0;
            _distance = target?.TargetDistance is { } d ? (decimal)d : null;
            _minutes = target?.TargetMinutes is { } m ? (decimal)m : null;

            var parts = new List<string>();
            if (target?.TargetDistance is { } td) parts.Add($"{td:0.##}");
            if (target?.TargetMinutes is { } tm) parts.Add($"{tm:0.#} min");
            TargetSummary = parts.Count > 0 ? "Target:  " + string.Join("  ·  ", parts) : string.Empty;
        }
        else if (target is { TargetSets: > 0 })
        {
            TargetSets = target.TargetSets;
            _reps = target.TargetReps;
            _weight = target.TargetWeight is null ? null : (decimal)target.TargetWeight;
            TargetSummary = target.TargetWeight is { } w
                ? $"Target:  {target.TargetSets} × {target.TargetReps} @ {w:0.##}"
                : $"Target:  {target.TargetSets} × {target.TargetReps}";
        }
        else
        {
            TargetSets = 0;
            TargetSummary = string.Empty;
        }

        UpdateProgress();
    }

    /// <summary>Attaches an already-persisted entry when resuming a session.</summary>
    public void AddExisting(SetEntry row)
    {
        Sets.Add(new LoggedSetViewModel(row));
        if (IsCardio)
        {
            if (row.Distance > 0) Distance = (decimal)row.Distance;
            if (row.DurationSec > 0) Minutes = (decimal)(row.DurationSec / 60.0);
        }
        else
        {
            Reps = row.Reps;
            Weight = row.Weight > 0 ? (decimal)row.Weight : null;
        }
        UpdateProgress();
    }

    [RelayCommand]
    private void AddLap() => Laps++;

    [RelayCommand]
    private void RemoveLap() => Laps = Math.Max(0, Laps - 1);

    [RelayCommand]
    private async Task LogSetAsync()
    {
        SetEntry row;
        if (IsCardio)
        {
            var distance = Distance is { } d ? (double)d : 0;
            var seconds = Minutes is { } m ? (int)((double)m * 60) : 0;
            if (distance <= 0 && seconds <= 0 && Laps <= 0)
                return;

            row = new SetEntry
            {
                SessionId = _sessionId,
                ExerciseId = ExerciseId,
                SetNumber = Sets.Count + 1,
                Distance = distance,
                DurationSec = seconds,
                Laps = Laps,
            };
        }
        else
        {
            if (Reps is not { } reps || reps <= 0)
                return;

            row = new SetEntry
            {
                SessionId = _sessionId,
                ExerciseId = ExerciseId,
                SetNumber = Sets.Count + 1,
                Reps = (int)reps,
                Weight = Weight is { } w ? (double)w : 0,
            };
        }

        await _db.SaveSetAsync(row);
        Sets.Add(new LoggedSetViewModel(row));
        Laps = 0;
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

    private void UpdateProgress() => Progress = IsCardio || TargetSets == 0
        ? (Sets.Count == 1 ? "1 logged" : $"{Sets.Count} logged")
        : $"{Sets.Count} / {TargetSets} sets";
}
