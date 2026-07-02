using SQLite;

namespace RepForge.Models;

/// <summary>One exercise slot within a workout template, with its targets.</summary>
[Table("TemplateExercise")]
public class TemplateExercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TemplateId { get; set; }

    public int ExerciseId { get; set; }

    /// <summary>Position of this exercise within the template.</summary>
    public int SortOrder { get; set; }

    public int TargetSets { get; set; } = 3;

    public int TargetReps { get; set; } = 10;

    public double? TargetWeight { get; set; }

    public int RestSeconds { get; set; } = 90;
}
