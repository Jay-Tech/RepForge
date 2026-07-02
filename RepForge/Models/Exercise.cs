using SQLite;

namespace RepForge.Models;

[Table("Exercise")]
public class Exercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public string? Equipment { get; set; }

    public string? Notes { get; set; }
}
