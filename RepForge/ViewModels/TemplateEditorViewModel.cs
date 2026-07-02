using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

public partial class TemplateEditorViewModel : ViewModelBase
{
    private readonly RepForgeDb _db;
    private readonly WorkoutTemplate _template;
    private readonly Action _close;

    public ObservableCollection<TemplateExerciseItem> Items { get; } = [];

    public ObservableCollection<Exercise> Library { get; } = [];

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private Exercise? _selectedExercise;

    public TemplateEditorViewModel(RepForgeDb db, WorkoutTemplate template, Action close)
    {
        _db = db;
        _template = template;
        _close = close;
        _name = template.Name;

        _ = LoadAsync();
    }

    partial void OnNameChanged(string value)
    {
        _template.Name = value.Trim();
        _ = _db.SaveTemplateAsync(_template);
    }

    private async Task LoadAsync()
    {
        var exercises = await _db.GetExercisesAsync();
        Library.Clear();
        foreach (var exercise in exercises)
            Library.Add(exercise);

        var byId = exercises.ToDictionary(e => e.Id);
        Items.Clear();
        foreach (var row in await _db.GetTemplateExercisesAsync(_template.Id))
        {
            var name = byId.TryGetValue(row.ExerciseId, out var ex) ? ex.Name : "(deleted exercise)";
            Items.Add(new TemplateExerciseItem(row, name, _db.SaveTemplateExerciseAsync));
        }
    }

    [RelayCommand]
    private async Task AddExerciseAsync()
    {
        if (SelectedExercise is not { } exercise)
            return;

        var row = new TemplateExercise
        {
            TemplateId = _template.Id,
            ExerciseId = exercise.Id,
            SortOrder = Items.Count,
        };
        await _db.SaveTemplateExerciseAsync(row);
        Items.Add(new TemplateExerciseItem(row, exercise.Name, _db.SaveTemplateExerciseAsync));
        SelectedExercise = null;
    }

    [RelayCommand]
    private async Task RemoveItemAsync(TemplateExerciseItem item)
    {
        await _db.DeleteTemplateExerciseAsync(item.Row);
        Items.Remove(item);
        await RenumberAsync();
    }

    [RelayCommand]
    private Task MoveUpAsync(TemplateExerciseItem item) => MoveAsync(item, -1);

    [RelayCommand]
    private Task MoveDownAsync(TemplateExerciseItem item) => MoveAsync(item, +1);

    private async Task MoveAsync(TemplateExerciseItem item, int delta)
    {
        var index = Items.IndexOf(item);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Items.Count)
            return;

        Items.Move(index, target);
        await RenumberAsync();
    }

    private async Task RenumberAsync()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Row.SortOrder == i)
                continue;
            Items[i].Row.SortOrder = i;
            await _db.SaveTemplateExerciseAsync(Items[i].Row);
        }
    }

    [RelayCommand]
    private void Close() => _close();
}
