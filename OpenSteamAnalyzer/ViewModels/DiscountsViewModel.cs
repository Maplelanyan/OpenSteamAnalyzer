using System.Collections.ObjectModel;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.Services;
using Prism.Mvvm;

namespace OpenSteamAnalyzer.ViewModels;

public sealed class DiscountsViewModel : BindableBase
{
    private const int PageSize = 20;
    private const int MaxDiscountGames = 500;

    private readonly IStoreDealsService _storeDealsService;
    private readonly IImageCacheService _imageCacheService;
    private bool _isBusy;
    private int _currentPage;
    private int _totalPages;
    private string _statusText = "点击刷新获取 Steam 商店打折游戏";

    public DiscountsViewModel(IStoreDealsService storeDealsService, IImageCacheService imageCacheService)
    {
        _storeDealsService = storeDealsService;
        _imageCacheService = imageCacheService;
        RefreshCommand = new AsyncDelegateCommand(RefreshAsync, () => !IsBusy);
        PreviousPageCommand = new AsyncDelegateCommand(() => ChangePageAsync(-1), CanGoToPreviousPage);
        NextPageCommand = new AsyncDelegateCommand(() => ChangePageAsync(1), CanGoToNextPage);
    }

    public ObservableCollection<StoreDiscountGame> Games { get; } = new();

    public ObservableCollection<StoreDiscountGame> PagedGames { get; } = new();

    public AsyncDelegateCommand RefreshCommand { get; }

    public AsyncDelegateCommand PreviousPageCommand { get; }

    public AsyncDelegateCommand NextPageCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                RaisePageCommandStateChanged();
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                RaisePropertyChanged(nameof(PageInfo));
                RaisePageCommandStateChanged();
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                RaisePropertyChanged(nameof(PageInfo));
                RaisePageCommandStateChanged();
            }
        }
    }

    public string PageInfo => TotalPages == 0
        ? "0 / 0"
        : $"{CurrentPage} / {TotalPages}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "正在获取 Steam 商店折扣";

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var games = await _storeDealsService.GetCurrentDiscountsAsync(MaxDiscountGames, cancellationTokenSource.Token);

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }

            await RefreshPagedGamesAsync(resetPage: true, cancellationTokenSource.Token);

            StatusText = Games.Count == 0
                ? "暂无可显示的打折游戏"
                : $"已获取 {Games.Count} 款打折游戏";
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

    private async Task ChangePageAsync(int offset)
    {
        var targetPage = CurrentPage + offset;
        if (targetPage < 1 || targetPage > TotalPages)
        {
            return;
        }

        CurrentPage = targetPage;
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await RefreshPagedGamesAsync(resetPage: false, cancellationTokenSource.Token);
    }

    private async Task RefreshPagedGamesAsync(bool resetPage, CancellationToken cancellationToken)
    {
        TotalPages = Games.Count == 0
            ? 0
            : (int)Math.Ceiling(Games.Count / (double)PageSize);

        if (resetPage)
        {
            CurrentPage = TotalPages == 0 ? 0 : 1;
        }
        else if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }
        else if (CurrentPage == 0 && TotalPages > 0)
        {
            CurrentPage = 1;
        }

        PagedGames.Clear();
        if (CurrentPage == 0)
        {
            RaisePageCommandStateChanged();
            return;
        }

        var pageGames = Games
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
        var gamesWithCachedImages = await CacheImagesAsync(pageGames, cancellationToken);
        foreach (var game in gamesWithCachedImages)
        {
            PagedGames.Add(game);
        }

        RaisePageCommandStateChanged();
    }

    private bool CanGoToPreviousPage()
    {
        return !IsBusy && CurrentPage > 1;
    }

    private bool CanGoToNextPage()
    {
        return !IsBusy && CurrentPage > 0 && CurrentPage < TotalPages;
    }

    private void RaisePageCommandStateChanged()
    {
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private async Task<IReadOnlyList<StoreDiscountGame>> CacheImagesAsync(
        IReadOnlyList<StoreDiscountGame> games,
        CancellationToken cancellationToken)
    {
        using var throttler = new SemaphoreSlim(8);
        var tasks = games.Select(async game =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                var cachedImagePath = await _imageCacheService.GetCachedImagePathAsync(game.ImageUrl, cancellationToken);
                return WithCachedImage(game, cachedImagePath);
            }
            catch
            {
                return game;
            }
            finally
            {
                throttler.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private static StoreDiscountGame WithCachedImage(StoreDiscountGame game, string cachedImagePath)
    {
        return new StoreDiscountGame
        {
            AppId = game.AppId,
            Name = game.Name,
            ImageUrl = game.ImageUrl,
            CachedImagePath = cachedImagePath,
            StoreUrl = game.StoreUrl,
            DiscountPercent = game.DiscountPercent,
            OriginalPrice = game.OriginalPrice,
            FinalPrice = game.FinalPrice
        };
    }
}
