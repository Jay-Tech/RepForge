using SQLite;

namespace RepForge.Models;

/// <summary>A single trip to the gym: one execution of a template (or an ad-hoc workout).</summary>
[Table("WorkoutSession")]
public class WorkoutSession
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Template this session was started from; null for ad-hoc workouts.</summary>
    public Guid? TemplateId { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
