using CommunityToolkit.Mvvm.Input;
using RepForge.Controls;
using RepForge.Data;
using RepForge.Models;

namespace RepForge.ViewModels;

/// <summary>Progression chart for one exercise: the best set of each session over time.</summary>
public partial class ExerciseProgressViewModel : ViewModelBase
{
    private readonly Action _close;

    public string Title { get; }

    public string Subtitle { get; private set; } = string.Empty;

    public string BestText { get; private set; } = string.Empty;

    public IReadOnlyList<ChartPoint> Points { get; private set; } = [];

    private ExerciseProgressViewModel(Exercise exercise, Action close)
    {
        Title = exercise.Name;
        _close = close;
    }

    public static async Task<ExerciseProgressViewModel> CreateAsync(
        RepForgeDb db, Exercise exercise, Action close)
    {
        var vm = new ExerciseProgressViewModel(exercise, close);
        var sets = await db.GetSetsForExerciseAsync(exercise.Id);

        // One point per session: the session's best value for the chosen metric.
        var sessions = sets
            .GroupBy(s => s.SessionId)
            .Select(g => (Date: g.Min(s => s.LoggedUtc), Sets: g.ToList()))
            .OrderBy(x => x.Date)
            .ToList();

        Func<List<SetEntry>, double> best;
        string metric, unitSuffix = "";

        if (exercise.Type == ExerciseType.Cardio)
        {
            if (sessions.Any(x => x.Sets.Any(s => s.Distance > 0)))
                (best, metric) = (g => g.Max(s => s.Distance), "Best distance per session");
            else if (sessions.Any(x => x.Sets.Any(s => s.DurationSec > 0)))
                (best, metric, unitSuffix) = (g => g.Max(s => s.DurationSec) / 60.0, "Longest time per session", " min");
            else
                (best, metric) = (g => g.Max(s => (double)s.Laps), "Most laps per session");
        }
        else if (sessions.Any(x => x.Sets.Any(s => s.Weight > 0)))
        {
            (best, metric) = (g => g.Max(s => s.Weight), "Top set weight per session");
        }
        else
        {
            (best, metric) = (g => g.Max(s => (double)s.Reps), "Most reps per session");
        }

        vm.Points = sessions
            .Select(x => new ChartPoint(x.Date, best(x.Sets)))
            .Where(p => p.Value > 0)
            .ToList();
        vm.Subtitle = metric;
        vm.BestText = vm.Points.Count > 0
            ? $"Best:  {vm.Points.Max(p => p.Value):0.##}{unitSuffix}   ·   {vm.Points.Count} session{(vm.Points.Count == 1 ? "" : "s")}"
            : "No logged sessions yet — progress shows up here once you train this.";
        return vm;
    }

    [RelayCommand]
    private void Close() => _close();
}
