using CommunityToolkit.Mvvm.ComponentModel;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>
/// One row in the template editor: a TemplateExercise joined with its exercise name.
/// Edits to the targets write through to the row and are persisted immediately.
/// </summary>
public partial class TemplateExerciseItem : ObservableObject
{
    private readonly Func<TemplateExercise, Task> _save;

    public TemplateExercise Row { get; }

    public string ExerciseName { get; }

    [ObservableProperty]
    private decimal? _sets;

    [ObservableProperty]
    private decimal? _reps;

    [ObservableProperty]
    private decimal? _weight;

    [ObservableProperty]
    private decimal? _restSeconds;

    public TemplateExerciseItem(TemplateExercise row, string exerciseName, Func<TemplateExercise, Task> save)
    {
        Row = row;
        ExerciseName = exerciseName;
        _save = save;

        // Backing fields, not properties: initialization must not trigger a save.
        _sets = row.TargetSets;
        _reps = row.TargetReps;
        _weight = row.TargetWeight is null ? null : (decimal)row.TargetWeight;
        _restSeconds = row.RestSeconds;
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
}
