using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public sealed class AnalyzerService : IAnalyzerService
{
    public LibraryAnalysis Analyze(IReadOnlyList<SteamGame> games)
    {
        var totalMinutes = games.Sum(game => game.PlaytimeMinutes);
        var totalGames = games.Count;

        return new LibraryAnalysis
        {
            TotalGames = totalGames,
            TotalHours = Math.Round(totalMinutes / 60d, 1),
            AverageHours = totalGames == 0 ? 0 : Math.Round(totalMinutes / 60d / totalGames, 1),
            UnplayedGames = games.Count(game => game.PlaytimeMinutes == 0),
            RecentTwoWeeksHours = Math.Round(games.Sum(game => game.RecentPlaytimeMinutes) / 60d, 1),
            TopGames = games
                .OrderByDescending(game => game.PlaytimeMinutes)
                .ThenBy(game => game.Name)
                .Take(10)
                .ToList(),
            TimeBuckets = BuildBuckets(games)
        };
    }

    private static IReadOnlyList<GameTimeBucket> BuildBuckets(IReadOnlyList<SteamGame> games)
    {
        return new[]
        {
            new GameTimeBucket { Label = "未玩", Count = games.Count(game => game.PlaytimeMinutes == 0) },
            new GameTimeBucket { Label = "0-2h", Count = games.Count(game => game.PlaytimeMinutes is > 0 and <= 120) },
            new GameTimeBucket { Label = "2-10h", Count = games.Count(game => game.PlaytimeMinutes is > 120 and <= 600) },
            new GameTimeBucket { Label = "10-50h", Count = games.Count(game => game.PlaytimeMinutes is > 600 and <= 3000) },
            new GameTimeBucket { Label = "50h+", Count = games.Count(game => game.PlaytimeMinutes > 3000) }
        };
    }
}
