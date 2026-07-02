namespace RepForge.Models;

/// <summary>Shared display formatting for logged sets (session cards, history, last-time hints).</summary>
public static class SetFormat
{
    public static bool IsCardioEntry(SetEntry s) =>
        s.Distance > 0 || s.DurationSec > 0 || s.Laps > 0;

    public static string Time(int seconds) =>
        seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds / 60}:{seconds % 60:00}";

    /// <summary>"10 reps × 185" / "2.5 · 22:30 · 10 laps"</summary>
    public static string Line(SetEntry s)
    {
        if (!IsCardioEntry(s))
            return s.Weight > 0 ? $"{s.Reps} reps  ×  {s.Weight:0.##}" : $"{s.Reps} reps";

        var parts = new List<string>();
        if (s.Distance > 0) parts.Add($"{s.Distance:0.##}");
        if (s.DurationSec > 0) parts.Add(Time(s.DurationSec));
        if (s.Laps > 0) parts.Add($"{s.Laps} laps");
        return string.Join("  ·  ", parts);
    }

    /// <summary>Compact form for the "last time" hint: "185×10" / "2.5 · 22:30"</summary>
    public static string Short(SetEntry s)
    {
        if (!IsCardioEntry(s))
            return s.Weight > 0 ? $"{s.Weight:0.##}×{s.Reps}" : $"{s.Reps} reps";
        return Line(s);
    }
}
