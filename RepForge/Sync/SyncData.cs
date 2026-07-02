using System.Text.Json;
using System.Text.Json.Serialization;
using RepForge.Models;

namespace RepForge.Sync;

/// <summary>Full dataset exchanged between devices during a sync.</summary>
public class SyncData
{
    public List<Exercise> Exercises { get; set; } = [];

    public List<WorkoutTemplate> Templates { get; set; } = [];

    public List<TemplateExercise> TemplateExercises { get; set; } = [];

    public List<WorkoutSession> Sessions { get; set; } = [];

    public List<SetEntry> Sets { get; set; } = [];

    public List<Tombstone> Tombstones { get; set; } = [];

    public string Summary() =>
        $"{Exercises.Count} exercises, {Templates.Count} plans, {Sessions.Count} sessions, {Sets.Count} sets";

    public byte[] Serialize() =>
        JsonSerializer.SerializeToUtf8Bytes(this, SyncJsonContext.Default.SyncData);

    public static SyncData Deserialize(byte[] payload) =>
        JsonSerializer.Deserialize(payload, SyncJsonContext.Default.SyncData)
        ?? throw new InvalidDataException("Empty sync payload.");
}

/// <summary>Source-generated serializer so sync survives trimming on Android release builds.</summary>
[JsonSerializable(typeof(SyncData))]
internal partial class SyncJsonContext : JsonSerializerContext;
