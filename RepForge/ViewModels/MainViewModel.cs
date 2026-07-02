namespace RepForge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public TemplatesViewModel Templates { get; } = new();

    public ExerciseLibraryViewModel ExerciseLibrary { get; } = new();
}
