namespace RepForge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public WorkoutViewModel Workout { get; } = new();

    public TemplatesViewModel Templates { get; } = new();

    public ExerciseLibraryViewModel ExerciseLibrary { get; } = new();

    public HistoryViewModel History { get; } = new();
}
