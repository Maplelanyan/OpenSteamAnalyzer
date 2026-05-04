namespace OpenSteamAnalyzer.Models;

public sealed class CachedSteamLibrary
{
    public required SteamProfile Profile { get; init; }

    public required IReadOnlyList<SteamGame> Games { get; init; }

    public required DateTimeOffset CachedAt { get; init; }
}
