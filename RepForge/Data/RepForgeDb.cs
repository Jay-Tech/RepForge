using RepForge.Models;
using SQLite;

namespace RepForge.Data;

/// <summary>
/// Application database. The file lives in the platform's local app-data folder
/// (app-private storage on Android), so the same code works on desktop and phone.
/// </summary>
public sealed class RepForgeDb
{
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static RepForgeDb? _instance;

    private readonly SQLiteAsyncConnection _conn;

    private RepForgeDb(string databasePath)
    {
        _conn = new SQLiteAsyncConnection(databasePath, Flags);
    }

    public static string GetDefaultDbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RepForge");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "repforge.db3");
    }

    public static async Task<RepForgeDb> GetAsync()
    {
        if (_instance is not null)
            return _instance;

        await InitLock.WaitAsync();
        try
        {
            if (_instance is null)
            {
                var db = new RepForgeDb(GetDefaultDbPath());
                await db.InitializeAsync();
                _instance = db;
            }
            return _instance;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private async Task InitializeAsync()
    {
        await _conn.CreateTableAsync<Exercise>();
        await _conn.CreateTableAsync<WorkoutTemplate>();
        await _conn.CreateTableAsync<TemplateExercise>();
        await _conn.CreateTableAsync<WorkoutSession>();
        await _conn.CreateTableAsync<SetEntry>();

        if (await _conn.Table<Exercise>().CountAsync() == 0)
            await _conn.InsertAllAsync(SeedExercises());
    }

    // --- Exercises ---

    public Task<List<Exercise>> GetExercisesAsync() =>
        _conn.Table<Exercise>().OrderBy(e => e.Name).ToListAsync();

    public Task<int> SaveExerciseAsync(Exercise exercise) =>
        exercise.Id == 0 ? _conn.InsertAsync(exercise) : _conn.UpdateAsync(exercise);

    public Task<int> DeleteExerciseAsync(Exercise exercise) =>
        _conn.DeleteAsync(exercise);

    // --- Templates ---

    public Task<List<WorkoutTemplate>> GetTemplatesAsync() =>
        _conn.Table<WorkoutTemplate>().OrderBy(t => t.Name).ToListAsync();

    public Task<int> SaveTemplateAsync(WorkoutTemplate template) =>
        template.Id == 0 ? _conn.InsertAsync(template) : _conn.UpdateAsync(template);

    public async Task DeleteTemplateAsync(WorkoutTemplate template)
    {
        await _conn.Table<TemplateExercise>().Where(te => te.TemplateId == template.Id).DeleteAsync();
        await _conn.DeleteAsync(template);
    }

    public Task<List<TemplateExercise>> GetTemplateExercisesAsync(int templateId) =>
        _conn.Table<TemplateExercise>()
            .Where(te => te.TemplateId == templateId)
            .OrderBy(te => te.SortOrder)
            .ToListAsync();

    public Task<int> SaveTemplateExerciseAsync(TemplateExercise item) =>
        item.Id == 0 ? _conn.InsertAsync(item) : _conn.UpdateAsync(item);

    public Task<int> DeleteTemplateExerciseAsync(TemplateExercise item) =>
        _conn.DeleteAsync(item);

    // --- Sessions & sets ---

    public Task<List<WorkoutSession>> GetSessionsAsync() =>
        _conn.Table<WorkoutSession>().OrderByDescending(s => s.StartedUtc).ToListAsync();

    public Task<int> SaveSessionAsync(WorkoutSession session) =>
        session.Id == 0 ? _conn.InsertAsync(session) : _conn.UpdateAsync(session);

    public Task<List<SetEntry>> GetSetsForSessionAsync(int sessionId) =>
        _conn.Table<SetEntry>()
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.LoggedUtc)
            .ToListAsync();

    public Task<int> SaveSetAsync(SetEntry set) =>
        set.Id == 0 ? _conn.InsertAsync(set) : _conn.UpdateAsync(set);

    public Task<int> DeleteSetAsync(SetEntry set) =>
        _conn.DeleteAsync(set);

    private static List<Exercise> SeedExercises() =>
    [
        new() { Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" },
        new() { Name = "Incline Dumbbell Press", MuscleGroup = "Chest", Equipment = "Dumbbells" },
        new() { Name = "Push-Up", MuscleGroup = "Chest", Equipment = "Bodyweight" },
        new() { Name = "Cable Fly", MuscleGroup = "Chest", Equipment = "Cable" },
        new() { Name = "Deadlift", MuscleGroup = "Back", Equipment = "Barbell" },
        new() { Name = "Pull-Up", MuscleGroup = "Back", Equipment = "Bodyweight" },
        new() { Name = "Bent-Over Row", MuscleGroup = "Back", Equipment = "Barbell" },
        new() { Name = "Lat Pulldown", MuscleGroup = "Back", Equipment = "Cable" },
        new() { Name = "Seated Cable Row", MuscleGroup = "Back", Equipment = "Cable" },
        new() { Name = "Squat", MuscleGroup = "Legs", Equipment = "Barbell" },
        new() { Name = "Leg Press", MuscleGroup = "Legs", Equipment = "Machine" },
        new() { Name = "Romanian Deadlift", MuscleGroup = "Legs", Equipment = "Barbell" },
        new() { Name = "Walking Lunge", MuscleGroup = "Legs", Equipment = "Dumbbells" },
        new() { Name = "Leg Curl", MuscleGroup = "Legs", Equipment = "Machine" },
        new() { Name = "Calf Raise", MuscleGroup = "Legs", Equipment = "Machine" },
        new() { Name = "Overhead Press", MuscleGroup = "Shoulders", Equipment = "Barbell" },
        new() { Name = "Lateral Raise", MuscleGroup = "Shoulders", Equipment = "Dumbbells" },
        new() { Name = "Face Pull", MuscleGroup = "Shoulders", Equipment = "Cable" },
        new() { Name = "Barbell Curl", MuscleGroup = "Arms", Equipment = "Barbell" },
        new() { Name = "Hammer Curl", MuscleGroup = "Arms", Equipment = "Dumbbells" },
        new() { Name = "Triceps Pushdown", MuscleGroup = "Arms", Equipment = "Cable" },
        new() { Name = "Skull Crusher", MuscleGroup = "Arms", Equipment = "Barbell" },
        new() { Name = "Plank", MuscleGroup = "Core", Equipment = "Bodyweight" },
        new() { Name = "Hanging Leg Raise", MuscleGroup = "Core", Equipment = "Bodyweight" },
        new() { Name = "Cable Crunch", MuscleGroup = "Core", Equipment = "Cable" },
    ];
}
