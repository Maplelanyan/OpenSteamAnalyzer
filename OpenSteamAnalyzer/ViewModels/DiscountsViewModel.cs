using System.Collections.ObjectModel;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.Services;
using Prism.Mvvm;

namespace OpenSteamAnalyzer.ViewModels;

public sealed class DiscountsViewModel : BindableBase
{
    private readonly IStoreDealsService _storeDealsService;
    private readonly IImageCacheService _imageCacheService;
    private bool _isBusy;
    private string _statusText = "点击刷新获取 Steam 商店打折游戏";

    public DiscountsViewModel(IStoreDealsService storeDealsService, IImageCacheService imageCacheService)
    {
        _storeDealsService = storeDealsService;
        _imageCacheService = imageCacheService;
        RefreshCommand = new AsyncDelegateCommand(RefreshAsync, () => !IsBusy);
    }

    public ObservableCollection<StoreDiscountGame> Games { get; } = new();

    public AsyncDelegateCommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

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
            var games = await _storeDealsService.GetCurrentDiscountsAsync(100, cancellationTokenSource.Token);
            var gamesWithCachedImages = await CacheImagesAsync(games, cancellationTokenSource.Token);

            Games.Clear();
            foreach (var game in gamesWithCachedImages)
            {
                Games.Add(game);
            }

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
