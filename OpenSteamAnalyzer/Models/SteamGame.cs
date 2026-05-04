namespace OpenSteamAnalyzer.Models;

public sealed class SteamGame
{
    public int AppId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int PlaytimeMinutes { get; init; }

    public int RecentPlaytimeMinutes { get; init; }

    public string IconUrl { get; init; } = string.Empty;

    public DateTimeOffset? LastPlayedAt { get; init; }

    public double PlaytimeHours => Math.Round(PlaytimeMinutes / 60d, 1);

    public double RecentPlaytimeHours => Math.Round(RecentPlaytimeMinutes / 60d, 1);

    public string LastPlayedText => LastPlayedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-";
}
