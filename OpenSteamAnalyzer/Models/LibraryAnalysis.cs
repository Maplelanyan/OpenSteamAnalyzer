namespace OpenSteamAnalyzer.Models;

public sealed class LibraryAnalysis
{
    public int TotalGames { get; init; }

    public double TotalHours { get; init; }

    public double AverageHours { get; init; }

    public int UnplayedGames { get; init; }

    public double RecentTwoWeeksHours { get; init; }

    public IReadOnlyList<SteamGame> TopGames { get; init; } = Array.Empty<SteamGame>();

    public IReadOnlyList<SteamGame> RecentTopGames { get; init; } = Array.Empty<SteamGame>();

    public IReadOnlyList<GameTimeBucket> TimeBuckets { get; init; } = Array.Empty<GameTimeBucket>();

    public IReadOnlyList<ActivityTrendPoint> ActivityTrend { get; init; } = Array.Empty<ActivityTrendPoint>();

    public IReadOnlyList<TrendInsight> TrendInsights { get; init; } = Array.Empty<TrendInsight>();
}
