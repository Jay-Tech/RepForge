using CommunityToolkit.Mvvm.ComponentModel;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>A set that has been logged in the active session.</summary>
public class LoggedSetViewModel(SetEntry row) : ObservableObject
{
    public SetEntry Row { get; } = row;

    public string Display => Row.Weight > 0
        ? $"Set {Row.SetNumber}:   {Row.Reps} reps  ×  {Row.Weight:0.##}"
        : $"Set {Row.SetNumber}:   {Row.Reps} reps";

    public void Refresh() => OnPropertyChanged(nameof(Display));
}
