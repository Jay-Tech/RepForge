using SQLite;

namespace RepForge.Models;

/// <summary>Local key-value settings (not synced between devices).</summary>
[Table("Setting")]
public class Setting
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
