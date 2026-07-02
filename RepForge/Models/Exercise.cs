using SQLite;

namespace RepForge.Models;

[Table("Exercise")]
public class Exercise
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string? Equipment { get; set; }

    public string? Notes { get; set; }

    public ExerciseType Type { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
