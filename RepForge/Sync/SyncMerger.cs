using RepForge.Models;

namespace RepForge.Sync;

/// <summary>
/// Merges two full datasets. Per row the newest ModifiedUtc wins;
/// a tombstone always beats the row it refers to.
/// </summary>
public static class SyncMerger
{
    public static SyncData Merge(SyncData a, SyncData b)
    {
        // Deletions from either side apply; keep the newest stamp per id.
        var tombstones = a.Tombstones.Concat(b.Tombstones)
            .GroupBy(t => t.Id)
            .Select(g => g.OrderByDescending(t => t.DeletedUtc).First())
            .ToList();
        var deadIds = tombstones.Select(t => t.Id).ToHashSet();

        var merged = new SyncData
        {
            Tombstones = tombstones,
            Exercises = MergeRows(a.Exercises, b.Exercises, e => e.Id, e => e.ModifiedUtc, deadIds),
            Templates = MergeRows(a.Templates, b.Templates, t => t.Id, t => t.ModifiedUtc, deadIds),
            TemplateExercises = MergeRows(a.TemplateExercises, b.TemplateExercises, te => te.Id, te => te.ModifiedUtc, deadIds),
            Sessions = MergeRows(a.Sessions, b.Sessions, s => s.Id, s => s.ModifiedUtc, deadIds),
            Sets = MergeRows(a.Sets, b.Sets, s => s.Id, s => s.ModifiedUtc, deadIds),
            BodyMetrics = MergeRows(a.BodyMetrics, b.BodyMetrics, m => m.Id, m => m.ModifiedUtc, deadIds),
        };

        // Referential cleanup: children whose parent was deleted on the other device.
        var templateIds = merged.Templates.Select(t => t.Id).ToHashSet();
        var sessionIds = merged.Sessions.Select(s => s.Id).ToHashSet();
        merged.TemplateExercises.RemoveAll(te => !templateIds.Contains(te.TemplateId));
        merged.Sets.RemoveAll(s => !sessionIds.Contains(s.SessionId));

        return merged;
    }

    private static List<T> MergeRows<T>(
        List<T> a, List<T> b,
        Func<T, Guid> id, Func<T, DateTime> modified,
        HashSet<Guid> deadIds)
        =>
        a.Concat(b)
            .Where(row => !deadIds.Contains(id(row)))
            .GroupBy(id)
            .Select(g => g.OrderByDescending(modified).First())
            .ToList();
}
