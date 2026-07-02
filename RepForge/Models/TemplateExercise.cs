using SQLite;

namespace RepForge.Models;

/// <summary>One exercise slot within a workout template, with its targets.</summary>
[Table("TemplateExercise")]
public class TemplateExercise
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed]
    public Guid TemplateId { get; set; }

    public Guid ExerciseId { get; set; }

    /// <summary>Position of this exercise within the template.</summary>
    public int SortOrder { get; set; }

    public int TargetSets { get; set; } = 3;

    public int TargetReps { get; set; } = 10;

    public double? TargetWeight { get; set; }

    public int RestSeconds { get; set; } = 90;

    // Cardio targets (used when the exercise's Type is Cardio)
    public double? TargetDistance { get; set; }

    public double? TargetMinutes { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
