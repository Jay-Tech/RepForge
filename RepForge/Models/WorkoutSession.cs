using SQLite;

namespace RepForge.Models;

/// <summary>A single trip to the gym: one execution of a template (or an ad-hoc workout).</summary>
[Table("WorkoutSession")]
public class WorkoutSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Template this session was started from; null for ad-hoc workouts.</summary>
    public int? TemplateId { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedUtc { get; set; }

    public string? Notes { get; set; }
}
