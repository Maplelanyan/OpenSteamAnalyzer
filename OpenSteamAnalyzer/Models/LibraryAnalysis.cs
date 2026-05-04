namespace OpenSteamAnalyzer.Models;

public sealed class LibraryAnalysis
{
    public int TotalGames { get; init; }

    public double TotalHours { get; init; }

    public double AverageHours { get; init; }

    public int UnplayedGames { get; init; }

    public double RecentTwoWeeksHours { get; init; }

    public IReadOnlyList<SteamGame> TopGames { get; init; } = Array.Empty<SteamGame>();

    public IReadOnlyList<GameTimeBucket> TimeBuckets { get; init; } = Array.Empty<GameTimeBucket>();
}
