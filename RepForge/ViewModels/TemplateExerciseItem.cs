using CommunityToolkit.Mvvm.ComponentModel;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>
/// One row in the template editor: a TemplateExercise joined with its exercise.
/// Edits to the targets write through to the row and are persisted immediately.
/// Strength rows edit sets/reps/weight/rest; cardio rows edit distance/time.
/// </summary>
public partial class TemplateExerciseItem : ObservableObject
{
    private readonly Func<TemplateExercise, Task> _save;

    public TemplateExercise Row { get; }

    public string ExerciseName { get; }

    public bool IsCardio { get; }

    public bool IsStrength => !IsCardio;

    [ObservableProperty]
    private decimal? _sets;

    [ObservableProperty]
    private decimal? _reps;

    [ObservableProperty]
    private decimal? _weight;

    [ObservableProperty]
    private decimal? _restSeconds;

    [ObservableProperty]
    private decimal? _distance;

    [ObservableProperty]
    private decimal? _minutes;

    public TemplateExerciseItem(TemplateExercise row, Exercise? exercise, Func<TemplateExercise, Task> save)
    {
        Row = row;
        ExerciseName = exercise?.Name ?? "(deleted exercise)";
        IsCardio = exercise?.Type == ExerciseType.Cardio;
        _save = save;

        // Backing fields, not properties: initialization must not trigger a save.
        _sets = row.TargetSets;
        _reps = row.TargetReps;
        _weight = row.TargetWeight is null ? null : (decimal)row.TargetWeight;
        _restSeconds = row.RestSeconds;
        _distance = row.TargetDistance is null ? null : (decimal)row.TargetDistance;
        _minutes = row.TargetMinutes is null ? null : (decimal)row.TargetMinutes;
    }

    partial void OnSetsChanged(decimal? value)
    {
        if (value is null) return;
        Row.TargetSets = (int)value;
        _ = _save(Row);
    }

    partial void OnRepsChanged(decimal? value)
    {
        if (value is null) return;
        Row.TargetReps = (int)value;
        _ = _save(Row);
    }

    partial void OnWeightChanged(decimal? value)
    {
        Row.TargetWeight = value is null ? null : (double)value;
        _ = _save(Row);
    }

    partial void OnRestSecondsChanged(decimal? value)
    {
        if (value is null) return;
        Row.RestSeconds = (int)value;
        _ = _save(Row);
    }

    partial void OnDistanceChanged(decimal? value)
    {
        Row.TargetDistance = value is null ? null : (double)value;
        _ = _save(Row);
    }

    partial void OnMinutesChanged(decimal? value)
    {
        Row.TargetMinutes = value is null ? null : (double)value;
        _ = _save(Row);
    }
}
