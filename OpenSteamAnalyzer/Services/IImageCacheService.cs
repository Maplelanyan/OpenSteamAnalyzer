namespace OpenSteamAnalyzer.Services;

public interface IImageCacheService
{
    Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken);
}
