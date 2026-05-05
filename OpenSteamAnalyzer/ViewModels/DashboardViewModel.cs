using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Data;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.Repositories;
using OpenSteamAnalyzer.Services;
using Prism.Mvvm;

namespace OpenSteamAnalyzer.ViewModels;

public sealed class DashboardViewModel : BindableBase
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    private readonly ISteamIdResolverService _steamIdResolver;
    private readonly ISteamApiService _steamApiService;
    private readonly ISteamCacheRepository _cacheRepository;
    private readonly IAnalyzerService _analyzerService;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly AppSettings _settings;

    private string _accountInput = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedFilterOption = "全部游戏";
    private string _selectedSortOption = "游玩时长";
    private AccountHistoryItem? _selectedHistoryAccount;
    private string _statusText = "等待输入";
    private string _friendStatusText = "分析账号后显示好友";
    private bool _isBusy;
    private bool _isLoadingFriends;
    private SteamProfile? _profile;
    private LibraryAnalysis _analysis = new();
    private ISeries[] _topGamesSeries = Array.Empty<ISeries>();
    private Axis[] _topGamesXAxes = { new() };
    private Axis[] _topGamesYAxes = { new() };
    private ISeries[] _distributionSeries = Array.Empty<ISeries>();
    private ISeries[] _recentGamesSeries = Array.Empty<ISeries>();
    private Axis[] _recentGamesXAxes = { new() };
    private Axis[] _recentGamesYAxes = { new() };
    private ISeries[] _activityTrendSeries = Array.Empty<ISeries>();
    private Axis[] _activityTrendXAxes = { new() };
    private Axis[] _activityTrendYAxes = { new() };
    private ISeries[] _paretoSeries = Array.Empty<ISeries>();
    private Axis[] _paretoXAxes = { new() };
    private Axis[] _paretoYAxes = { new() };

    public DashboardViewModel(
        ISteamIdResolverService steamIdResolver,
        ISteamApiService steamApiService,
        ISteamCacheRepository cacheRepository,
        IAnalyzerService analyzerService,
        IAppSettingsRepository settingsRepository,
        AppSettings settings)
    {
        _steamIdResolver = steamIdResolver;
        _steamApiService = steamApiService;
        _cacheRepository = cacheRepository;
        _analyzerService = analyzerService;
        _settingsRepository = settingsRepository;
        _settings = settings;
        _accountInput = settings.SteamInput;
        RefreshAccountHistory();

        if (!string.IsNullOrWhiteSpace(settingsRepository.LastLoadError))
        {
            _statusText = settingsRepository.LastLoadError;
        }
        else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_API_KEY")))
        {
            _statusText = "已读取环境变量 STEAM_API_KEY";
        }

        GamesView = CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = FilterGame;

        AnalyzeCommand = new AsyncDelegateCommand(() => LoadAsync(forceRefresh: false), CanAnalyze);
        RefreshCommand = new AsyncDelegateCommand(() => LoadAsync(forceRefresh: true), CanAnalyze);

    }

    public AsyncDelegateCommand AnalyzeCommand { get; }

    public AsyncDelegateCommand RefreshCommand { get; }

    public ObservableCollection<SteamGame> Games { get; } = new();

    public ObservableCollection<SteamFriend> Friends { get; } = new();

    public ObservableCollection<TrendInsight> TrendInsights { get; } = new();

    public ObservableCollection<AccountHistoryItem> AccountHistory { get; } = new();

    public ICollectionView GamesView { get; }

    public IReadOnlyList<string> FilterOptions { get; } = new[] { "全部游戏", "已玩", "未玩" };

    public IReadOnlyList<string> SortOptions { get; } = new[] { "游玩时长", "名称", "最近游玩" };

    public string AccountInput
    {
        get => _accountInput;
        set
        {
            if (SetProperty(ref _accountInput, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AccountHistoryItem? SelectedHistoryAccount
    {
        get => _selectedHistoryAccount;
        set
        {
            if (SetProperty(ref _selectedHistoryAccount, value)
                && value is not null
                && !string.Equals(AccountInput, value.DisplayText, StringComparison.Ordinal))
            {
                AccountInput = value.DisplayText;
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                GamesView.Refresh();
            }
        }
    }

    public string SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            if (SetProperty(ref _selectedFilterOption, value))
            {
                GamesView.Refresh();
            }
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplySort();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoadingFriends
    {
        get => _isLoadingFriends;
        private set => SetProperty(ref _isLoadingFriends, value);
    }

    public string FriendStatusText
    {
        get => _friendStatusText;
        private set => SetProperty(ref _friendStatusText, value);
    }

    public SteamProfile? Profile
    {
        get => _profile;
        private set => SetProperty(ref _profile, value);
    }

    public int TotalGames => _analysis.TotalGames;

    public double TotalHours => _analysis.TotalHours;

    public double AverageHours => _analysis.AverageHours;

    public int UnplayedGames => _analysis.UnplayedGames;

    public double RecentTwoWeeksHours => _analysis.RecentTwoWeeksHours;

    public ISeries[] TopGamesSeries
    {
        get => _topGamesSeries;
        private set => SetProperty(ref _topGamesSeries, value);
    }

    public Axis[] TopGamesXAxes
    {
        get => _topGamesXAxes;
        private set => SetProperty(ref _topGamesXAxes, value);
    }

    public Axis[] TopGamesYAxes
    {
        get => _topGamesYAxes;
        private set => SetProperty(ref _topGamesYAxes, value);
    }

    public ISeries[] DistributionSeries
    {
        get => _distributionSeries;
        private set => SetProperty(ref _distributionSeries, value);
    }

    public ISeries[] RecentGamesSeries
    {
        get => _recentGamesSeries;
        private set => SetProperty(ref _recentGamesSeries, value);
    }

    public Axis[] RecentGamesXAxes
    {
        get => _recentGamesXAxes;
        private set => SetProperty(ref _recentGamesXAxes, value);
    }

    public Axis[] RecentGamesYAxes
    {
        get => _recentGamesYAxes;
        private set => SetProperty(ref _recentGamesYAxes, value);
    }

    public ISeries[] ActivityTrendSeries
    {
        get => _activityTrendSeries;
        private set => SetProperty(ref _activityTrendSeries, value);
    }

    public Axis[] ActivityTrendXAxes
    {
        get => _activityTrendXAxes;
        private set => SetProperty(ref _activityTrendXAxes, value);
    }

    public Axis[] ActivityTrendYAxes
    {
        get => _activityTrendYAxes;
        private set => SetProperty(ref _activityTrendYAxes, value);
    }

    public ISeries[] ParetoSeries
    {
        get => _paretoSeries;
        private set => SetProperty(ref _paretoSeries, value);
    }

    public Axis[] ParetoXAxes
    {
        get => _paretoXAxes;
        private set => SetProperty(ref _paretoXAxes, value);
    }

    public Axis[] ParetoYAxes
    {
        get => _paretoYAxes;
        private set => SetProperty(ref _paretoYAxes, value);
    }

    private bool CanAnalyze()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(AccountInput);
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        IsBusy = true;
        StatusText = "正在解析账号";

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var cancellationToken = cancellationTokenSource.Token;
            var steamId64 = await _steamIdResolver.ResolveSteamId64Async(AccountInput, cancellationToken);

            if (!forceRefresh)
            {
                var cachedLibrary = await _cacheRepository.GetLibraryAsync(steamId64, cancellationToken);
                if (cachedLibrary is not null && DateTimeOffset.UtcNow - cachedLibrary.CachedAt <= CacheLifetime)
                {
                    ApplyLibrary(cachedLibrary.Profile, cachedLibrary.Games);
                    await LoadFriendsAsync(steamId64, cancellationToken);
                    StatusText = TrySaveSteamInput()
                        ? $"已使用缓存：{cachedLibrary.CachedAt.ToLocalTime():yyyy-MM-dd HH:mm}{BuildDecorationStatus(cachedLibrary.Profile)}"
                        : $"已使用缓存，但保存 SteamID 失败：{cachedLibrary.CachedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
                    return;
                }
            }

            StatusText = "正在拉取 Steam 数据";
            var profile = await _steamApiService.GetProfileAsync(steamId64, cancellationToken);
            var games = await _steamApiService.GetOwnedGamesAsync(steamId64, cancellationToken);
            var recentPlaytime = await _steamApiService.GetRecentPlaytimeByAppIdAsync(steamId64, cancellationToken);
            var mergedGames = games
                .Select(game => recentPlaytime.TryGetValue(game.AppId, out var recentMinutes)
                    ? new SteamGame
                    {
                        AppId = game.AppId,
                        Name = game.Name,
                        PlaytimeMinutes = game.PlaytimeMinutes,
                        RecentPlaytimeMinutes = recentMinutes,
                        IconUrl = game.IconUrl,
                        LastPlayedAt = game.LastPlayedAt
                    }
                    : game)
                .ToList();

            await _cacheRepository.SaveLibraryAsync(profile, mergedGames, cancellationToken);
            ApplyLibrary(profile, mergedGames);
            await LoadFriendsAsync(steamId64, cancellationToken);
            StatusText = TrySaveSteamInput()
                ? $"分析完成：{DateTimeOffset.Now:HH:mm}{BuildDecorationStatus(profile)}"
                : $"分析完成，但保存 SteamID 失败：{DateTimeOffset.Now:HH:mm}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "请求超时，请稍后重试。";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AnalyzeFriendAsync(SteamFriend friend)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(friend.SteamId64))
        {
            return;
        }

        AccountInput = friend.SteamId64;
        await LoadAsync(forceRefresh: false);
    }

    private void ApplyLibrary(SteamProfile profile, IReadOnlyList<SteamGame> games)
    {
        Profile = profile;
        _analysis = _analyzerService.Analyze(games);
        Friends.Clear();
        FriendStatusText = "正在加载好友";

        Games.Clear();
        foreach (var game in games)
        {
            Games.Add(game);
        }

        ApplySort();
        UpdateCharts();
        RaiseAnalysisPropertiesChanged();
    }

    private async Task LoadFriendsAsync(string steamId64, CancellationToken cancellationToken)
    {
        IsLoadingFriends = true;
        Friends.Clear();
        FriendStatusText = "正在加载好友";

        try
        {
            var friends = await _steamApiService.GetFriendsAsync(steamId64, cancellationToken);
            foreach (var friend in friends)
            {
                Friends.Add(friend);
            }

            FriendStatusText = Friends.Count == 0
                ? "暂无公开好友"
                : $"共 {Friends.Count} 位好友";
        }
        catch (Exception ex) when (ex is SteamApiException or HttpRequestException or InvalidOperationException or JsonException)
        {
            FriendStatusText = $"无法读取好友列表：{ex.Message}";
        }
        finally
        {
            IsLoadingFriends = false;
        }
    }

    private bool FilterGame(object item)
    {
        if (item is not SteamGame game)
        {
            return false;
        }

        var matchesSearch = string.IsNullOrWhiteSpace(SearchText)
            || game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        var matchesFilter = SelectedFilterOption switch
        {
            "已玩" => game.PlaytimeMinutes > 0,
            "未玩" => game.PlaytimeMinutes == 0,
            _ => true
        };

        return matchesSearch && matchesFilter;
    }

    private void ApplySort()
    {
        GamesView.SortDescriptions.Clear();
        switch (SelectedSortOption)
        {
            case "名称":
                GamesView.SortDescriptions.Add(new SortDescription(nameof(SteamGame.Name), ListSortDirection.Ascending));
                break;
            case "最近游玩":
                GamesView.SortDescriptions.Add(new SortDescription(nameof(SteamGame.RecentPlaytimeMinutes), ListSortDirection.Descending));
                GamesView.SortDescriptions.Add(new SortDescription(nameof(SteamGame.PlaytimeMinutes), ListSortDirection.Descending));
                break;
            default:
                GamesView.SortDescriptions.Add(new SortDescription(nameof(SteamGame.PlaytimeMinutes), ListSortDirection.Descending));
                break;
        }

        GamesView.Refresh();
    }

    private void UpdateCharts()
    {
        TopGamesSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "h",
                Values = _analysis.TopGames.Select(game => game.PlaytimeHours).ToArray()
            }
        };

        TopGamesXAxes = new[]
        {
            new Axis
            {
                Labels = _analysis.TopGames.Select(game => Shorten(game.Name)).ToArray(),
                LabelsRotation = -25,
                TextSize = 11
            }
        };

        TopGamesYAxes = new[]
        {
            new Axis
            {
                Name = "h",
                MinLimit = 0
            }
        };

        DistributionSeries = _analysis.TimeBuckets
            .Where(bucket => bucket.Count > 0)
            .Select(bucket => new PieSeries<double>
            {
                Name = bucket.Label,
                Values = new[] { (double)bucket.Count }
            })
            .Cast<ISeries>()
            .ToArray();

        RecentGamesSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "h",
                Values = _analysis.RecentTopGames.Select(game => game.RecentPlaytimeHours).ToArray()
            }
        };

        RecentGamesXAxes = new[]
        {
            new Axis
            {
                Labels = _analysis.RecentTopGames.Select(game => Shorten(game.Name)).ToArray(),
                LabelsRotation = -25,
                TextSize = 11
            }
        };

        RecentGamesYAxes = new[]
        {
            new Axis
            {
                Name = "h",
                MinLimit = 0
            }
        };

        ActivityTrendSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Games",
                Values = _analysis.ActivityTrend.Select(point => (double)point.GameCount).ToArray()
            },
            new LineSeries<double>
            {
                Name = "Hours",
                Values = _analysis.ActivityTrend.Select(point => point.TotalHours).ToArray()
            }
        };

        ActivityTrendXAxes = new[]
        {
            new Axis
            {
                Labels = _analysis.ActivityTrend.Select(point => point.Label).ToArray(),
                LabelsRotation = -25,
                TextSize = 11
            }
        };

        ActivityTrendYAxes = new[]
        {
            new Axis
            {
                MinLimit = 0
            }
        };

        var playedGames = Games
            .Where(game => game.PlaytimeMinutes > 0)
            .OrderByDescending(game => game.PlaytimeMinutes)
            .Take(12)
            .ToList();
        var totalPlayedMinutes = Games.Sum(game => game.PlaytimeMinutes);
        var runningTotal = 0;
        var cumulativeValues = playedGames
            .Select(game =>
            {
                runningTotal += game.PlaytimeMinutes;
                return totalPlayedMinutes == 0 ? 0 : Math.Round(runningTotal * 100d / totalPlayedMinutes, 1);
            })
            .ToArray();

        ParetoSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "%",
                Values = cumulativeValues
            }
        };

        ParetoXAxes = new[]
        {
            new Axis
            {
                Labels = playedGames.Select(game => Shorten(game.Name)).ToArray(),
                LabelsRotation = -25,
                TextSize = 11
            }
        };

        ParetoYAxes = new[]
        {
            new Axis
            {
                Name = "%",
                MinLimit = 0,
                MaxLimit = 100
            }
        };

        TrendInsights.Clear();
        foreach (var insight in _analysis.TrendInsights)
        {
            TrendInsights.Add(insight);
        }
    }

    private void RaiseAnalysisPropertiesChanged()
    {
        RaisePropertyChanged(nameof(TotalGames));
        RaisePropertyChanged(nameof(TotalHours));
        RaisePropertyChanged(nameof(AverageHours));
        RaisePropertyChanged(nameof(UnplayedGames));
        RaisePropertyChanged(nameof(RecentTwoWeeksHours));
    }

    private static string Shorten(string value)
    {
        return value.Length <= 16 ? value : value[..15] + "...";
    }

    private bool TrySaveSteamInput()
    {
        try
        {
            _settings.SteamInput = AccountInput.Trim();
            AddAccountHistory(_settings.SteamInput);
            _settingsRepository.Save(_settings);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshAccountHistory()
    {
        AccountHistory.Clear();
        foreach (var account in _settings.SteamInputHistory
                     .Select(input => input.Trim())
                     .Where(input => !string.IsNullOrWhiteSpace(input))
                     .GroupBy(AccountHistoryItem.BuildDisplayText, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            AccountHistory.Add(new AccountHistoryItem(account));
        }
    }

    private void AddAccountHistory(string account)
    {
        account = account.Trim();
        if (string.IsNullOrWhiteSpace(account))
        {
            return;
        }

        _settings.SteamInputHistory = new[] { account }
            .Concat(_settings.SteamInputHistory)
            .Select(input => input.Trim())
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .GroupBy(AccountHistoryItem.BuildDisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(20)
            .ToList();

        RefreshAccountHistory();
        SelectedHistoryAccount = AccountHistory.FirstOrDefault(item =>
            string.Equals(item.Value, account, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDecorationStatus(SteamProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.AvatarFrameVideoUrl))
        {
            return $"；头像框：动态{BuildBackgroundStatus(profile)}";
        }

        if (!string.IsNullOrWhiteSpace(profile.AvatarFrameUrl))
        {
            return $"；头像框：静态{BuildBackgroundStatus(profile)}";
        }

        return $"；头像框：无{BuildBackgroundStatus(profile)}";
    }

    private static string BuildBackgroundStatus(SteamProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ProfileBackgroundVideoUrl))
        {
            return "；背景：动态";
        }

        if (!string.IsNullOrWhiteSpace(profile.ProfileBackgroundUrl))
        {
            return "；背景：静态";
        }

        return "；背景：无";
    }
}
