using System.Security.Cryptography;
using System.Text;
using RepForge.Models;
using RepForge.Sync;
using SQLite;

namespace RepForge.Data;

/// <summary>
/// Application database. The file lives in the platform's local app-data folder
/// (app-private storage on Android), so the same code works on desktop and phone.
/// All rows carry Guid keys and a ModifiedUtc stamp so two devices can merge.
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
        // Overridable for tests so a second dataset can live next to the real one.
        if (Environment.GetEnvironmentVariable("REPFORGE_DB") is { Length: > 0 } overridePath)
            return overridePath;

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
        await MigrateFromV1IfNeededAsync();

        await _conn.CreateTableAsync<Exercise>();
        await _conn.CreateTableAsync<WorkoutTemplate>();
        await _conn.CreateTableAsync<TemplateExercise>();
        await _conn.CreateTableAsync<WorkoutSession>();
        await _conn.CreateTableAsync<SetEntry>();
        await _conn.CreateTableAsync<BodyMetric>();
        await _conn.CreateTableAsync<Tombstone>();
        await _conn.CreateTableAsync<Setting>();

        await EnsureSeedExercisesAsync();
    }

    /// <summary>
    /// Inserts any seed exercises not yet present (new installs get all of them;
    /// existing databases pick up seeds added in later versions). Seeds the user
    /// deliberately deleted stay deleted — their tombstones block re-insertion.
    /// </summary>
    private async Task EnsureSeedExercisesAsync()
    {
        var existing = (await _conn.Table<Exercise>().ToListAsync()).Select(e => e.Id).ToHashSet();
        var deleted = (await _conn.Table<Tombstone>().ToListAsync()).Select(t => t.Id).ToHashSet();

        var missing = SeedExercises()
            .Where(s => !existing.Contains(s.Id) && !deleted.Contains(s.Id))
            .ToList();
        if (missing.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var seed in missing)
            seed.ModifiedUtc = now;
        await _conn.InsertAllAsync(missing);
    }

    /// <summary>
    /// Same name → same Guid on every device, so seed exercises (and same-named
    /// migrated ones) merge into a single row instead of duplicating on first sync.
    /// </summary>
    public static Guid DeterministicExerciseId(string name)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(
            "repforge-exercise:" + name.Trim().ToLowerInvariant()));
        return new Guid(bytes);
    }

    // --- Exercises ---

    public Task<List<Exercise>> GetExercisesAsync() =>
        _conn.Table<Exercise>().OrderBy(e => e.Name).ToListAsync();

    public Task<int> SaveExerciseAsync(Exercise exercise)
    {
        exercise.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(exercise);
    }

    public async Task DeleteExerciseAsync(Exercise exercise)
    {
        await _conn.DeleteAsync(exercise);
        await AddTombstoneAsync(exercise.Id, "Exercise");
    }

    // --- Templates ---

    public Task<List<WorkoutTemplate>> GetTemplatesAsync() =>
        _conn.Table<WorkoutTemplate>().OrderBy(t => t.Name).ToListAsync();

    public Task<WorkoutTemplate?> GetTemplateAsync(Guid id) =>
        _conn.FindAsync<WorkoutTemplate>(id)!;

    public Task<int> SaveTemplateAsync(WorkoutTemplate template)
    {
        template.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(template);
    }

    public async Task DeleteTemplateAsync(WorkoutTemplate template)
    {
        foreach (var row in await GetTemplateExercisesAsync(template.Id))
        {
            await _conn.DeleteAsync(row);
            await AddTombstoneAsync(row.Id, "TemplateExercise");
        }
        await _conn.DeleteAsync(template);
        await AddTombstoneAsync(template.Id, "WorkoutTemplate");
    }

    public Task<List<TemplateExercise>> GetTemplateExercisesAsync(Guid templateId) =>
        _conn.Table<TemplateExercise>()
            .Where(te => te.TemplateId == templateId)
            .OrderBy(te => te.SortOrder)
            .ToListAsync();

    public Task<int> SaveTemplateExerciseAsync(TemplateExercise item)
    {
        item.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(item);
    }

    public async Task DeleteTemplateExerciseAsync(TemplateExercise item)
    {
        await _conn.DeleteAsync(item);
        await AddTombstoneAsync(item.Id, "TemplateExercise");
    }

    // --- Sessions & sets ---

    public Task<List<WorkoutSession>> GetSessionsAsync() =>
        _conn.Table<WorkoutSession>().OrderByDescending(s => s.StartedUtc).ToListAsync();

    /// <summary>Most recent unfinished session, if any — used to resume after an app restart.</summary>
    public Task<WorkoutSession?> GetActiveSessionAsync() =>
        _conn.Table<WorkoutSession>()
            .Where(s => s.CompletedUtc == null)
            .OrderByDescending(s => s.StartedUtc)
            .FirstOrDefaultAsync()!;

    public Task<int> SaveSessionAsync(WorkoutSession session)
    {
        session.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(session);
    }

    public async Task DeleteSessionAsync(WorkoutSession session)
    {
        foreach (var set in await GetSetsForSessionAsync(session.Id))
        {
            await _conn.DeleteAsync(set);
            await AddTombstoneAsync(set.Id, "SetEntry");
        }
        await _conn.DeleteAsync(session);
        await AddTombstoneAsync(session.Id, "WorkoutSession");
    }

    public Task<List<WorkoutSession>> GetCompletedSessionsAsync() =>
        _conn.Table<WorkoutSession>()
            .Where(s => s.CompletedUtc != null)
            .OrderByDescending(s => s.StartedUtc)
            .ToListAsync();

    public Task<List<SetEntry>> GetAllSetsAsync() =>
        _conn.Table<SetEntry>().ToListAsync();

    public Task<List<SetEntry>> GetSetsForSessionAsync(Guid sessionId) =>
        _conn.Table<SetEntry>()
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.LoggedUtc)
            .ToListAsync();

    public Task<int> SaveSetAsync(SetEntry set)
    {
        set.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(set);
    }

    public async Task DeleteSetAsync(SetEntry set)
    {
        await _conn.DeleteAsync(set);
        await AddTombstoneAsync(set.Id, "SetEntry");
    }

    /// <summary>Sets for this exercise from the most recent other session — the "last time" hint.</summary>
    public async Task<List<SetEntry>> GetPreviousSetsAsync(Guid exerciseId, Guid excludeSessionId)
    {
        var last = await _conn.Table<SetEntry>()
            .Where(s => s.ExerciseId == exerciseId && s.SessionId != excludeSessionId)
            .OrderByDescending(s => s.LoggedUtc)
            .FirstOrDefaultAsync();
        if (last is null)
            return [];

        return await _conn.Table<SetEntry>()
            .Where(s => s.SessionId == last.SessionId && s.ExerciseId == exerciseId)
            .OrderBy(s => s.SetNumber)
            .ToListAsync();
    }

    // --- Body metrics ---

    public Task<List<BodyMetric>> GetBodyMetricsAsync() =>
        _conn.Table<BodyMetric>().OrderByDescending(b => b.MeasuredUtc).ToListAsync();

    public Task<int> SaveBodyMetricAsync(BodyMetric metric)
    {
        metric.ModifiedUtc = DateTime.UtcNow;
        return _conn.InsertOrReplaceAsync(metric);
    }

    public async Task DeleteBodyMetricAsync(BodyMetric metric)
    {
        await _conn.DeleteAsync(metric);
        await AddTombstoneAsync(metric.Id, "BodyMetric");
    }

    // --- Settings (local only, never synced) ---

    public async Task<string?> GetSettingAsync(string key) =>
        (await _conn.FindAsync<Setting>(key))?.Value;

    public Task SetSettingAsync(string key, string value) =>
        _conn.InsertOrReplaceAsync(new Setting { Key = key, Value = value });

    // --- Sync support ---

    private Task AddTombstoneAsync(Guid id, string table) =>
        _conn.InsertOrReplaceAsync(new Tombstone { Id = id, TableName = table });

    public async Task<SyncData> GetSyncDataAsync() => new()
    {
        Exercises = await _conn.Table<Exercise>().ToListAsync(),
        Templates = await _conn.Table<WorkoutTemplate>().ToListAsync(),
        TemplateExercises = await _conn.Table<TemplateExercise>().ToListAsync(),
        Sessions = await _conn.Table<WorkoutSession>().ToListAsync(),
        Sets = await _conn.Table<SetEntry>().ToListAsync(),
        BodyMetrics = await _conn.Table<BodyMetric>().ToListAsync(),
        Tombstones = await _conn.Table<Tombstone>().ToListAsync(),
    };

    /// <summary>Replaces all synced tables with the merged dataset, atomically.</summary>
    public Task ApplySyncDataAsync(SyncData data) =>
        _conn.RunInTransactionAsync(tran =>
        {
            tran.DeleteAll<Exercise>();
            tran.DeleteAll<WorkoutTemplate>();
            tran.DeleteAll<TemplateExercise>();
            tran.DeleteAll<WorkoutSession>();
            tran.DeleteAll<SetEntry>();
            tran.DeleteAll<BodyMetric>();
            tran.DeleteAll<Tombstone>();
            tran.InsertAll(data.Exercises);
            tran.InsertAll(data.Templates);
            tran.InsertAll(data.TemplateExercises);
            tran.InsertAll(data.Sessions);
            tran.InsertAll(data.Sets);
            tran.InsertAll(data.BodyMetrics);
            tran.InsertAll(data.Tombstones);
        });

    // --- v1 (int-key) schema migration ---

    private class OldRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string MuscleGroup { get; set; } = "";
        public string? Equipment { get; set; }
        public string? Notes { get; set; }
        public string? Description { get; set; }
        public long CreatedUtc { get; set; }
        public int TemplateId { get; set; }
        public int ExerciseId { get; set; }
        public int SortOrder { get; set; }
        public int TargetSets { get; set; }
        public int TargetReps { get; set; }
        public double? TargetWeight { get; set; }
        public int RestSeconds { get; set; }
        public long StartedUtc { get; set; }
        public long? CompletedUtc { get; set; }
        public int SessionId { get; set; }
        public int SetNumber { get; set; }
        public int Reps { get; set; }
        public double Weight { get; set; }
        public bool IsWarmup { get; set; }
        public long LoggedUtc { get; set; }
    }

    private async Task MigrateFromV1IfNeededAsync()
    {
        var hasExerciseTable = await _conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Exercise'") > 0;
        if (!hasExerciseTable)
            return;

        var idType = await _conn.ExecuteScalarAsync<string>(
            "SELECT type FROM pragma_table_info('Exercise') WHERE name='Id'");
        if (!string.Equals(idType, "INTEGER", StringComparison.OrdinalIgnoreCase))
            return; // already v2

        var now = DateTime.UtcNow;

        var oldExercises = await _conn.QueryAsync<OldRow>("SELECT * FROM Exercise");
        var oldTemplates = await _conn.QueryAsync<OldRow>("SELECT * FROM WorkoutTemplate");
        var oldTemplateExercises = await _conn.QueryAsync<OldRow>("SELECT * FROM TemplateExercise");
        var oldSessions = await _conn.QueryAsync<OldRow>("SELECT * FROM WorkoutSession");
        var oldSets = await _conn.QueryAsync<OldRow>("SELECT * FROM SetEntry");

        // Name-based ids let identical exercises on two independently created
        // databases merge on first sync; duplicates within one db stay distinct.
        var exerciseIds = new Dictionary<int, Guid>();
        var usedIds = new HashSet<Guid>();
        var exercises = new List<Exercise>();
        foreach (var old in oldExercises)
        {
            var id = DeterministicExerciseId(old.Name);
            if (!usedIds.Add(id))
            {
                id = Guid.NewGuid();
                usedIds.Add(id);
            }
            exerciseIds[old.Id] = id;
            exercises.Add(new Exercise
            {
                Id = id, Name = old.Name, MuscleGroup = old.MuscleGroup,
                Equipment = old.Equipment, Notes = old.Notes, ModifiedUtc = now,
            });
        }

        var templateIds = oldTemplates.ToDictionary(t => t.Id, _ => Guid.NewGuid());
        var sessionIds = oldSessions.ToDictionary(s => s.Id, _ => Guid.NewGuid());

        var templates = oldTemplates.Select(old => new WorkoutTemplate
        {
            Id = templateIds[old.Id], Name = old.Name, Description = old.Description,
            CreatedUtc = new DateTime(old.CreatedUtc, DateTimeKind.Utc), ModifiedUtc = now,
        }).ToList();

        var templateExercises = oldTemplateExercises
            .Where(old => templateIds.ContainsKey(old.TemplateId) && exerciseIds.ContainsKey(old.ExerciseId))
            .Select(old => new TemplateExercise
            {
                Id = Guid.NewGuid(),
                TemplateId = templateIds[old.TemplateId],
                ExerciseId = exerciseIds[old.ExerciseId],
                SortOrder = old.SortOrder, TargetSets = old.TargetSets, TargetReps = old.TargetReps,
                TargetWeight = old.TargetWeight, RestSeconds = old.RestSeconds, ModifiedUtc = now,
            }).ToList();

        var sessions = oldSessions.Select(old => new WorkoutSession
        {
            Id = sessionIds[old.Id],
            TemplateId = templateIds.TryGetValue(old.TemplateId, out var tid) ? tid : null,
            StartedUtc = new DateTime(old.StartedUtc, DateTimeKind.Utc),
            CompletedUtc = old.CompletedUtc is { } c ? new DateTime(c, DateTimeKind.Utc) : null,
            Notes = old.Notes, ModifiedUtc = now,
        }).ToList();

        var sets = oldSets
            .Where(old => sessionIds.ContainsKey(old.SessionId) && exerciseIds.ContainsKey(old.ExerciseId))
            .Select(old => new SetEntry
            {
                Id = Guid.NewGuid(),
                SessionId = sessionIds[old.SessionId],
                ExerciseId = exerciseIds[old.ExerciseId],
                SetNumber = old.SetNumber, Reps = old.Reps, Weight = old.Weight,
                IsWarmup = old.IsWarmup,
                LoggedUtc = new DateTime(old.LoggedUtc, DateTimeKind.Utc), ModifiedUtc = now,
            }).ToList();

        await _conn.ExecuteAsync("DROP TABLE Exercise");
        await _conn.ExecuteAsync("DROP TABLE WorkoutTemplate");
        await _conn.ExecuteAsync("DROP TABLE TemplateExercise");
        await _conn.ExecuteAsync("DROP TABLE WorkoutSession");
        await _conn.ExecuteAsync("DROP TABLE SetEntry");

        await _conn.CreateTableAsync<Exercise>();
        await _conn.CreateTableAsync<WorkoutTemplate>();
        await _conn.CreateTableAsync<TemplateExercise>();
        await _conn.CreateTableAsync<WorkoutSession>();
        await _conn.CreateTableAsync<SetEntry>();

        await _conn.InsertAllAsync(exercises);
        await _conn.InsertAllAsync(templates);
        await _conn.InsertAllAsync(templateExercises);
        await _conn.InsertAllAsync(sessions);
        await _conn.InsertAllAsync(sets);
    }

    private static List<Exercise> SeedExercises()
    {
        List<(string Name, string Group, string Equipment)> cardio =
        [
            ("Treadmill Run", "Cardio", "Machine"),
            ("Outdoor Run", "Cardio", "Bodyweight"),
            ("Incline Walk", "Cardio", "Machine"),
            ("Cycling", "Cardio", "Machine"),
            ("Rowing Machine", "Cardio", "Machine"),
            ("Elliptical", "Cardio", "Machine"),
            ("Stair Climber", "Cardio", "Machine"),
            ("Swimming", "Cardio", "Bodyweight"),
            ("Jump Rope", "Cardio", "Bodyweight"),
        ];

        List<(string Name, string Group, string Equipment)> seeds =
        [
            ("Bench Press", "Chest", "Barbell"),
            ("Incline Dumbbell Press", "Chest", "Dumbbells"),
            ("Push-Up", "Chest", "Bodyweight"),
            ("Cable Fly", "Chest", "Cable"),
            ("Deadlift", "Back", "Barbell"),
            ("Pull-Up", "Back", "Bodyweight"),
            ("Bent-Over Row", "Back", "Barbell"),
            ("Lat Pulldown", "Back", "Cable"),
            ("Seated Cable Row", "Back", "Cable"),
            ("Squat", "Legs", "Barbell"),
            ("Leg Press", "Legs", "Machine"),
            ("Romanian Deadlift", "Legs", "Barbell"),
            ("Walking Lunge", "Legs", "Dumbbells"),
            ("Leg Curl", "Legs", "Machine"),
            ("Calf Raise", "Legs", "Machine"),
            ("Overhead Press", "Shoulders", "Barbell"),
            ("Lateral Raise", "Shoulders", "Dumbbells"),
            ("Face Pull", "Shoulders", "Cable"),
            ("Barbell Curl", "Arms", "Barbell"),
            ("Hammer Curl", "Arms", "Dumbbells"),
            ("Triceps Pushdown", "Arms", "Cable"),
            ("Skull Crusher", "Arms", "Barbell"),
            ("Plank", "Core", "Bodyweight"),
            ("Hanging Leg Raise", "Core", "Bodyweight"),
            ("Cable Crunch", "Core", "Cable"),
        ];

        return seeds.Select(s => new Exercise
        {
            Id = DeterministicExerciseId(s.Name),
            Name = s.Name,
            MuscleGroup = s.Group,
            Equipment = s.Equipment,
        }).Concat(cardio.Select(s => new Exercise
        {
            Id = DeterministicExerciseId(s.Name),
            Name = s.Name,
            MuscleGroup = s.Group,
            Equipment = s.Equipment,
            Type = ExerciseType.Cardio,
        })).ToList();
    }
}
