using CommunityToolkit.Mvvm.ComponentModel;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>A set that has been logged in the active session.</summary>
public class LoggedSetViewModel(SetEntry row) : ObservableObject
{
    public SetEntry Row { get; } = row;

    public string Display => $"Set {Row.SetNumber}:   {SetFormat.Line(Row)}";

    public void Refresh() => OnPropertyChanged(nameof(Display));
}
