using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly SteamApiOptions _steamApiOptions;
    private readonly AppSettings _settings;

    private string _accountInput = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedFilterOption = "全部游戏";
    private string _selectedSortOption = "游玩时长";
    private string _statusText = "等待输入";
    private bool _isBusy;
    private SteamProfile? _profile;
    private LibraryAnalysis _analysis = new();
    private ISeries[] _topGamesSeries = Array.Empty<ISeries>();
    private Axis[] _topGamesXAxes = { new() };
    private Axis[] _topGamesYAxes = { new() };
    private ISeries[] _distributionSeries = Array.Empty<ISeries>();

    public DashboardViewModel(
        ISteamIdResolverService steamIdResolver,
        ISteamApiService steamApiService,
        ISteamCacheRepository cacheRepository,
        IAnalyzerService analyzerService,
        IAppSettingsRepository settingsRepository,
        SteamApiOptions steamApiOptions,
        AppSettings settings)
    {
        _steamIdResolver = steamIdResolver;
        _steamApiService = steamApiService;
        _cacheRepository = cacheRepository;
        _analyzerService = analyzerService;
        _settingsRepository = settingsRepository;
        _steamApiOptions = steamApiOptions;
        _settings = settings;
        _accountInput = settings.SteamInput;

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

        HasSavedApiKey = !string.IsNullOrWhiteSpace(settings.SteamApiKey)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_API_KEY"));
    }

    public AsyncDelegateCommand AnalyzeCommand { get; }

    public AsyncDelegateCommand RefreshCommand { get; }

    public ObservableCollection<SteamGame> Games { get; } = new();

    public ICollectionView GamesView { get; }

    public IReadOnlyList<string> FilterOptions { get; } = new[] { "全部游戏", "已玩", "未玩" };

    public IReadOnlyList<string> SortOptions { get; } = new[] { "游玩时长", "名称", "最近游玩" };

    public bool HasSavedApiKey { get; private set; }

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

    private bool CanAnalyze()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(AccountInput);
    }

    public bool SaveApiKey(string apiKey)
    {
        apiKey = apiKey.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText = "请输入 Steam API Key 后再保存。";
            return false;
        }

        try
        {
            var updatedSettings = new AppSettings
            {
                SteamInput = AccountInput.Trim(),
                SteamApiKey = apiKey
            };
            _settingsRepository.Save(updatedSettings);
            _settings.SteamInput = updatedSettings.SteamInput;
            _settings.SteamApiKey = updatedSettings.SteamApiKey;
        }
        catch (Exception ex)
        {
            StatusText = $"保存 Steam API Key 失败：{ex.Message}";
            return false;
        }

        var environmentApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY");
        _steamApiOptions.ApiKey = string.IsNullOrWhiteSpace(environmentApiKey)
            ? apiKey
            : environmentApiKey;

        StatusText = string.IsNullOrWhiteSpace(environmentApiKey)
            ? "Steam API Key 已保存"
            : "已保存本地 Key；当前仍优先使用环境变量 STEAM_API_KEY";
        HasSavedApiKey = !string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(environmentApiKey);
        RaisePropertyChanged(nameof(HasSavedApiKey));
        return true;
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

    private void ApplyLibrary(SteamProfile profile, IReadOnlyList<SteamGame> games)
    {
        Profile = profile;
        _analysis = _analyzerService.Analyze(games);

        Games.Clear();
        foreach (var game in games)
        {
            Games.Add(game);
        }

        ApplySort();
        UpdateCharts();
        RaiseAnalysisPropertiesChanged();
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
            _settingsRepository.Save(_settings);
            return true;
        }
        catch
        {
            return false;
        }
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
