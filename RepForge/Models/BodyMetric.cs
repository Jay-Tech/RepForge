using SQLite;

namespace RepForge.Models;

/// <summary>One body weigh-in. Height is captured per entry (prefilled from the
/// previous one) so BMI history stays correct and the rows sync with no extra state.</summary>
[Table("BodyMetric")]
public class BodyMetric
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime MeasuredUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Body weight in the user's chosen unit (lb or kg).</summary>
    public double Weight { get; set; }

    /// <summary>Height in the user's chosen unit (in or cm); 0 when not provided.</summary>
    public double Height { get; set; }

    public DateTime ModifiedUtc { get; set; }
}
