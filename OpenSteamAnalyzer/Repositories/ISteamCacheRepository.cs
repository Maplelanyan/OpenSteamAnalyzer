using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Repositories;

public interface ISteamCacheRepository
{
    Task<CachedSteamLibrary?> GetLibraryAsync(string steamId64, CancellationToken cancellationToken);

    Task SaveLibraryAsync(SteamProfile profile, IReadOnlyList<SteamGame> games, CancellationToken cancellationToken);
}
