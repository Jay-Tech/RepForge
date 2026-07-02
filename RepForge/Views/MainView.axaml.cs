using Avalonia.Controls;
using RepForge.ViewModels;

namespace RepForge.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged bubbles up from ListBoxes inside the tabs; only react to the TabControl.
        if (e.Source is TabControl && DataContext is MainViewModel vm)
            _ = vm.Workout.RefreshTemplatesAsync();
    }
}
