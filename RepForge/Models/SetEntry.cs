using SQLite;

namespace RepForge.Models;

/// <summary>One logged set: what was actually lifted during a session.</summary>
[Table("SetEntry")]
public class SetEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SessionId { get; set; }

    public int ExerciseId { get; set; }

    /// <summary>1-based set number within the exercise for this session.</summary>
    public int SetNumber { get; set; }

    public int Reps { get; set; }

    public double Weight { get; set; }

    public bool IsWarmup { get; set; }

    public DateTime LoggedUtc { get; set; } = DateTime.UtcNow;
}
