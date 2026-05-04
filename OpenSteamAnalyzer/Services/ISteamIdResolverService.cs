namespace OpenSteamAnalyzer.Services;

public interface ISteamIdResolverService
{
    Task<string> ResolveSteamId64Async(string input, CancellationToken cancellationToken);
}
