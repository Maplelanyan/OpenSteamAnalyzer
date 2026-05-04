namespace OpenSteamAnalyzer.Models;

public sealed class GameTimeBucket
{
    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }
}
