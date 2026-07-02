using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

public partial class ExerciseLibraryViewModel : ViewModelBase
{
    private RepForgeDb? _db;

    public ObservableCollection<Exercise> Exercises { get; } = [];

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newMuscleGroup = string.Empty;

    [ObservableProperty]
    private bool _newIsCardio;

    [ObservableProperty]
    private Exercise? _selectedExercise;

    [ObservableProperty]
    private ExerciseProgressViewModel? _activeProgress;

    [ObservableProperty]
    private bool _isLoading;

    public ExerciseLibraryViewModel()
    {
        if (!Design.IsDesignMode)
            _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            _db = await RepForgeDb.GetAsync();
            Exercises.Clear();
            foreach (var exercise in await _db.GetExercisesAsync())
                Exercises.Add(exercise);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Tapping an exercise opens its progression chart.</summary>
    partial void OnSelectedExerciseChanged(Exercise? value)
    {
        if (value is null || _db is null)
            return;
        _ = OpenProgressAsync(value);
        SelectedExercise = null;
    }

    private async Task OpenProgressAsync(Exercise exercise)
    {
        if (_db is null)
            return;
        ActiveProgress = await ExerciseProgressViewModel.CreateAsync(
            _db, exercise, () => ActiveProgress = null);
    }

    [RelayCommand]
    private async Task AddExerciseAsync()
    {
        var name = NewName.Trim();
        if (name.Length == 0 || _db is null)
            return;

        var exercise = new Exercise
        {
            Name = name,
            MuscleGroup = NewIsCardio && NewMuscleGroup.Trim().Length == 0
                ? "Cardio" : NewMuscleGroup.Trim(),
            Type = NewIsCardio ? ExerciseType.Cardio : ExerciseType.Strength,
        };
        await _db.SaveExerciseAsync(exercise);

        var index = 0;
        while (index < Exercises.Count &&
               string.Compare(Exercises[index].Name, exercise.Name, StringComparison.OrdinalIgnoreCase) < 0)
            index++;
        Exercises.Insert(index, exercise);

        NewName = string.Empty;
        NewMuscleGroup = string.Empty;
        NewIsCardio = false;
    }

    [RelayCommand]
    private async Task DeleteExerciseAsync(Exercise exercise)
    {
        if (_db is null)
            return;

        await _db.DeleteExerciseAsync(exercise);
        Exercises.Remove(exercise);
    }
}
