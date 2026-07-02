using SQLite;

namespace RepForge.Models;

/// <summary>One logged set: what was actually lifted during a session.</summary>
[Table("SetEntry")]
public class SetEntry
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed]
    public Guid SessionId { get; set; }

    public Guid ExerciseId { get; set; }

    /// <summary>1-based set number within the exercise for this session.</summary>
    public int SetNumber { get; set; }

    public int Reps { get; set; }

    public double Weight { get; set; }

    public bool IsWarmup { get; set; }

    public DateTime LoggedUtc { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedUtc { get; set; }
}
