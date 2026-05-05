namespace OpenSteamAnalyzer.Models;

public sealed class SteamProfile
{
    public string SteamId64 { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AvatarUrl { get; init; } = string.Empty;

    public string AvatarFrameUrl { get; init; } = string.Empty;

    public string AvatarFrameVideoUrl { get; init; } = string.Empty;

    public string AnimatedAvatarUrl { get; init; } = string.Empty;

    public string AnimatedAvatarVideoUrl { get; init; } = string.Empty;

    public string ProfileBackgroundUrl { get; init; } = string.Empty;

    public string ProfileBackgroundVideoUrl { get; init; } = string.Empty;

    public string MiniProfileBackgroundUrl { get; init; } = string.Empty;

    public string MiniProfileBackgroundVideoUrl { get; init; } = string.Empty;

    public string ProfileUrl { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public string StateText { get; init; } = string.Empty;

    public int? Level { get; init; }

    public string LevelText => SteamLevelStyle.GetLevelText(Level);

    public string LevelColor => SteamLevelStyle.GetLevelColor(Level);

    public string LevelBadgeBackground => SteamLevelStyle.GetLevelBadgeBackground(Level);

    public string DisplayAvatarUrl => string.IsNullOrWhiteSpace(AnimatedAvatarUrl)
        ? AvatarUrl
        : AnimatedAvatarUrl;

    public bool HasAnimatedAvatarLayer => !string.IsNullOrWhiteSpace(AvatarFrameVideoUrl)
        || !string.IsNullOrWhiteSpace(AnimatedAvatarVideoUrl)
        || !string.IsNullOrWhiteSpace(AvatarFrameUrl)
        || !string.IsNullOrWhiteSpace(AnimatedAvatarUrl);

    public bool HasProfileBackground => !string.IsNullOrWhiteSpace(ProfileBackgroundVideoUrl)
        || !string.IsNullOrWhiteSpace(ProfileBackgroundUrl);

    public bool HasMiniProfileBackground => !string.IsNullOrWhiteSpace(MiniProfileBackgroundVideoUrl)
        || !string.IsNullOrWhiteSpace(MiniProfileBackgroundUrl);
}
