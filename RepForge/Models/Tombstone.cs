using SQLite;

namespace RepForge.Models;

/// <summary>
/// Records a deletion so it can propagate during sync — without this,
/// a row deleted on one device would be resurrected by the other.
/// </summary>
[Table("Tombstone")]
public class Tombstone
{
    /// <summary>Id of the deleted row.</summary>
    [PrimaryKey]
    public Guid Id { get; set; }

    public string TableName { get; set; } = string.Empty;

    public DateTime DeletedUtc { get; set; } = DateTime.UtcNow;
}
