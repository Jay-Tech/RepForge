using SQLite;

namespace RepForge.Models;

/// <summary>A designed workout program: a named, ordered list of exercises with targets.</summary>
[Table("WorkoutTemplate")]
public class WorkoutTemplate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
