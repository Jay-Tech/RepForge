namespace RepForge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ExerciseLibraryViewModel ExerciseLibrary { get; } = new();
}
