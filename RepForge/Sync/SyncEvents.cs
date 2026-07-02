namespace RepForge.Sync;

/// <summary>Lets view models refresh after a sync changed the database underneath them.</summary>
public static class SyncEvents
{
    public static event Action? SyncCompleted;

    public static event Action<string>? Activity;

    public static void RaiseSyncCompleted() => SyncCompleted?.Invoke();

    public static void RaiseActivity(string message) => Activity?.Invoke(message);
}
