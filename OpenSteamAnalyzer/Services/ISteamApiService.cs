using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public interface ISteamApiService
{
    Task<SteamProfile> GetProfileAsync(string steamId64, CancellationToken cancellationToken);

    Task<IReadOnlyList<SteamGame>> GetOwnedGamesAsync(string steamId64, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, int>> GetRecentPlaytimeByAppIdAsync(string steamId64, CancellationToken cancellationToken);
}
