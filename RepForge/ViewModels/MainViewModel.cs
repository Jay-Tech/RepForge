using Avalonia.Controls;
using Avalonia.Threading;
using RepForge.Sync;

namespace RepForge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public WorkoutViewModel Workout { get; } = new();

    public TemplatesViewModel Templates { get; } = new();

    public ExerciseLibraryViewModel ExerciseLibrary { get; } = new();

    public HistoryViewModel History { get; } = new();

    public BodyViewModel Body { get; } = new();

    public SyncViewModel Sync { get; } = new();

    public MainViewModel()
    {
        if (Design.IsDesignMode)
            return;

        // A sync rewrites the database underneath the open tabs; reload them all.
        SyncEvents.SyncCompleted += () => Dispatcher.UIThread.Post(() =>
        {
            _ = Workout.RefreshTemplatesAsync();
            _ = Templates.RefreshAsync();
            _ = ExerciseLibrary.RefreshAsync();
            _ = History.RefreshAsync();
            _ = Body.RefreshAsync();
        });
    }
}
