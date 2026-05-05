namespace OpenSteamAnalyzer.Models;

public sealed class SteamFriend
{
    public string SteamId64 { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AvatarUrl { get; init; } = string.Empty;

    public string ProfileUrl { get; init; } = string.Empty;

    public string StateText { get; init; } = string.Empty;

    public int? Level { get; init; }

    public DateTimeOffset? FriendSince { get; init; }

    public string LevelText => SteamLevelStyle.GetLevelText(Level);

    public string LevelColor => SteamLevelStyle.GetLevelColor(Level);

    public string LevelBadgeBackground => SteamLevelStyle.GetLevelBadgeBackground(Level);

    public string FriendSinceText => FriendSince?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-";

    public string SummaryText => $"{StateText} · {FriendSinceText}";
}
