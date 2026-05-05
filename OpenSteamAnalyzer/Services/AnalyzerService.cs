using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public sealed class AnalyzerService : IAnalyzerService
{
    public LibraryAnalysis Analyze(IReadOnlyList<SteamGame> games)
    {
        var totalMinutes = games.Sum(game => game.PlaytimeMinutes);
        var totalGames = games.Count;
        var recentTopGames = games
            .Where(game => game.RecentPlaytimeMinutes > 0)
            .OrderByDescending(game => game.RecentPlaytimeMinutes)
            .ThenBy(game => game.Name)
            .Take(8)
            .ToList();
        var activityTrend = BuildActivityTrend(games);

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
            RecentTopGames = recentTopGames,
            TimeBuckets = BuildBuckets(games),
            ActivityTrend = activityTrend,
            TrendInsights = BuildTrendInsights(games, recentTopGames, activityTrend)
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

    private static IReadOnlyList<ActivityTrendPoint> BuildActivityTrend(IReadOnlyList<SteamGame> games)
    {
        var now = DateTimeOffset.Now;
        var monthStarts = Enumerable.Range(0, 12)
            .Select(offset => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(offset - 11))
            .ToList();

        return monthStarts
            .Select(monthStart =>
            {
                var monthEnd = monthStart.AddMonths(1);
                var monthGames = games
                    .Where(game => game.LastPlayedAt is not null)
                    .Where(game =>
                    {
                        var playedAt = game.LastPlayedAt!.Value.ToLocalTime();
                        return playedAt >= monthStart && playedAt < monthEnd;
                    })
                    .ToList();

                return new ActivityTrendPoint
                {
                    Label = monthStart.ToString("MM/yyyy"),
                    GameCount = monthGames.Count,
                    TotalHours = Math.Round(monthGames.Sum(game => game.PlaytimeMinutes) / 60d, 1)
                };
            })
            .ToList();
    }

    private static IReadOnlyList<TrendInsight> BuildTrendInsights(
        IReadOnlyList<SteamGame> games,
        IReadOnlyList<SteamGame> recentTopGames,
        IReadOnlyList<ActivityTrendPoint> activityTrend)
    {
        var playedGames = games.Where(game => game.PlaytimeMinutes > 0).ToList();
        var totalMinutes = playedGames.Sum(game => game.PlaytimeMinutes);
        var topThreeMinutes = playedGames
            .OrderByDescending(game => game.PlaytimeMinutes)
            .Take(3)
            .Sum(game => game.PlaytimeMinutes);
        var activeGames = games.Count(game => game.RecentPlaytimeMinutes > 0);
        var activeRate = games.Count == 0 ? 0 : Math.Round(activeGames * 100d / games.Count, 1);
        var focusRate = totalMinutes == 0 ? 0 : Math.Round(topThreeMinutes * 100d / totalMinutes, 1);
        var bestMonth = activityTrend
            .OrderByDescending(point => point.GameCount)
            .ThenByDescending(point => point.TotalHours)
            .FirstOrDefault();
        var recentFavorite = recentTopGames.FirstOrDefault();

        return new[]
        {
            new TrendInsight
            {
                Title = "最近活跃率",
                Value = $"{activeRate:0.#}%",
                Description = $"{activeGames} 款游戏在最近两周有游玩记录"
            },
            new TrendInsight
            {
                Title = "时长集中度",
                Value = $"{focusRate:0.#}%",
                Description = "总游玩时长中 Top 3 游戏的占比"
            },
            new TrendInsight
            {
                Title = "最近主力",
                Value = recentFavorite?.Name ?? "-",
                Description = recentFavorite is null
                    ? "最近两周暂无游玩记录"
                    : $"最近两周 {recentFavorite.RecentPlaytimeHours:0.#} 小时"
            },
            new TrendInsight
            {
                Title = "活跃峰值",
                Value = bestMonth?.Label ?? "-",
                Description = bestMonth is null
                    ? "暂无最后游玩时间记录"
                    : $"{bestMonth.GameCount} 款游戏最后游玩集中在该月"
            }
        };
    }
}
