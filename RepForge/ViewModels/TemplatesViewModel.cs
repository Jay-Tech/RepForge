using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

public partial class TemplatesViewModel : ViewModelBase
{
    private RepForgeDb? _db;

    public ObservableCollection<WorkoutTemplate> Templates { get; } = [];

    [ObservableProperty]
    private string _newTemplateName = string.Empty;

    [ObservableProperty]
    private TemplateEditorViewModel? _activeEditor;

    [ObservableProperty]
    private WorkoutTemplate? _selectedTemplate;

    public TemplatesViewModel()
    {
        if (!Design.IsDesignMode)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _db = await RepForgeDb.GetAsync();
        Templates.Clear();
        foreach (var template in await _db.GetTemplatesAsync())
            Templates.Add(template);
    }

    /// <summary>Tapping a template in the list opens it for editing.</summary>
    partial void OnSelectedTemplateChanged(WorkoutTemplate? value)
    {
        if (value is null)
            return;
        Open(value);
        SelectedTemplate = null;
    }

    [RelayCommand]
    private async Task AddTemplateAsync()
    {
        var name = NewTemplateName.Trim();
        if (name.Length == 0 || _db is null)
            return;

        var template = new WorkoutTemplate { Name = name };
        await _db.SaveTemplateAsync(template);
        Templates.Add(template);
        NewTemplateName = string.Empty;
        Open(template);
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync(WorkoutTemplate template)
    {
        if (_db is null)
            return;

        await _db.DeleteTemplateAsync(template);
        Templates.Remove(template);
    }

    private void Open(WorkoutTemplate template)
    {
        if (_db is null)
            return;

        ActiveEditor = new TemplateEditorViewModel(_db, template, () =>
        {
            ActiveEditor = null;
            _ = LoadAsync();
        });
    }
}
