namespace OpenSteamAnalyzer.Models;

public sealed class ActivityTrendPoint
{
    public string Label { get; init; } = string.Empty;

    public int GameCount { get; init; }

    public double TotalHours { get; init; }
}
