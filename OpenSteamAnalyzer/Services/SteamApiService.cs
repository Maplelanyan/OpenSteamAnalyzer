using System.Net;
using System.Net.Http;
using System.Text.Json;
using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public sealed class SteamApiService : ISteamApiService
{
    private readonly HttpClient _httpClient;
    private readonly SteamApiOptions _options;

    public SteamApiService(HttpClient httpClient, SteamApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<SteamProfile> GetProfileAsync(string steamId64, CancellationToken cancellationToken)
    {
        _options.EnsureApiKey();

        var summaryUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(_options.ApiKey)}&steamids={steamId64}";
        using var summaryDocument = await GetJsonAsync(summaryUrl, "获取账号信息", cancellationToken);

        if (!summaryDocument.RootElement.TryGetProperty("response", out var response)
            || !response.TryGetProperty("players", out var players)
            || players.GetArrayLength() == 0)
        {
            throw new SteamApiException("Steam API 未返回账号信息，请确认 SteamID 是否正确。");
        }

        var player = players[0];
        var level = await TryGetPlayerLevelAsync(steamId64, cancellationToken);
        var profileItems = await TryGetProfileItemsEquippedAsync(steamId64, cancellationToken);

        return new SteamProfile
        {
            SteamId64 = steamId64,
            DisplayName = GetString(player, "personaname"),
            AvatarUrl = GetString(player, "avatarfull"),
            AvatarFrameUrl = profileItems.AvatarFrameUrl,
            AvatarFrameVideoUrl = profileItems.AvatarFrameVideoUrl,
            AnimatedAvatarUrl = profileItems.AnimatedAvatarUrl,
            AnimatedAvatarVideoUrl = profileItems.AnimatedAvatarVideoUrl,
            ProfileBackgroundUrl = profileItems.ProfileBackgroundUrl,
            ProfileBackgroundVideoUrl = profileItems.ProfileBackgroundVideoUrl,
            ProfileUrl = GetString(player, "profileurl"),
            CountryCode = GetString(player, "loccountrycode"),
            StateText = MapPersonaState(GetInt32(player, "personastate")),
            Level = level
        };
    }

    public async Task<IReadOnlyList<SteamGame>> GetOwnedGamesAsync(string steamId64, CancellationToken cancellationToken)
    {
        _options.EnsureApiKey();

        var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&steamid={steamId64}&include_appinfo=1&include_played_free_games=1";
        using var document = await GetJsonAsync(url, "获取游戏库", cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out var response)
            || !response.TryGetProperty("games", out var gamesElement))
        {
            throw new SteamApiException("该账号的游戏库不可见，或 Steam API 未返回游戏列表。");
        }

        var games = new List<SteamGame>();
        foreach (var game in gamesElement.EnumerateArray())
        {
            var appId = GetInt32(game, "appid");
            var iconHash = GetString(game, "img_icon_url");
            games.Add(new SteamGame
            {
                AppId = appId,
                Name = GetString(game, "name"),
                PlaytimeMinutes = GetInt32(game, "playtime_forever"),
                RecentPlaytimeMinutes = GetInt32(game, "playtime_2weeks"),
                IconUrl = string.IsNullOrWhiteSpace(iconHash)
                    ? string.Empty
                    : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconHash}.jpg",
                LastPlayedAt = TryGetUnixTime(game, "rtime_last_played")
            });
        }

        return games
            .OrderByDescending(game => game.PlaytimeMinutes)
            .ThenBy(game => game.Name)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, int>> GetRecentPlaytimeByAppIdAsync(string steamId64, CancellationToken cancellationToken)
    {
        _options.EnsureApiKey();

        var url = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&steamid={steamId64}";
        using var document = await GetJsonAsync(url, "获取最近游玩", cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out var response)
            || !response.TryGetProperty("games", out var gamesElement))
        {
            return new Dictionary<int, int>();
        }

        return gamesElement
            .EnumerateArray()
            .Where(game => game.TryGetProperty("appid", out _))
            .GroupBy(game => GetInt32(game, "appid"))
            .ToDictionary(
                group => group.Key,
                group => group.Max(game => GetInt32(game, "playtime_2weeks")));
    }

    private async Task<int?> TryGetPlayerLevelAsync(string steamId64, CancellationToken cancellationToken)
    {
        var url = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&steamid={steamId64}";
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("response", out var rootResponse)
                && rootResponse.TryGetProperty("player_level", out var level)
                ? level.GetInt32()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ProfileItems> TryGetProfileItemsEquippedAsync(string steamId64, CancellationToken cancellationToken)
    {
        var url = $"https://api.steampowered.com/IPlayerService/GetProfileItemsEquipped/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&steamid={steamId64}&language=schinese";
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProfileItems();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("response", out var responseRoot))
            {
                return new ProfileItems();
            }

            var avatarFrame = TryGetProfileItem(responseRoot, "avatar_frame");
            var animatedAvatar = TryGetProfileItem(responseRoot, "animated_avatar");
            var profileBackground = TryGetProfileItem(responseRoot, "profile_background", "background");

            if (string.IsNullOrWhiteSpace(avatarFrame.VideoUrl))
            {
                avatarFrame = avatarFrame.Merge(await TryGetDedicatedProfileItemAsync(
                    steamId64,
                    "GetAvatarFrame",
                    cancellationToken,
                    "avatar_frame",
                    "frame",
                    "profile_item"));
            }

            if (string.IsNullOrWhiteSpace(animatedAvatar.VideoUrl))
            {
                animatedAvatar = animatedAvatar.Merge(await TryGetDedicatedProfileItemAsync(
                    steamId64,
                    "GetAnimatedAvatar",
                    cancellationToken,
                    "animated_avatar",
                    "avatar",
                    "profile_item"));
            }

            return new ProfileItems
            {
                AvatarFrameUrl = avatarFrame.ImageUrl,
                AvatarFrameVideoUrl = avatarFrame.VideoUrl,
                AnimatedAvatarUrl = animatedAvatar.ImageUrl,
                AnimatedAvatarVideoUrl = animatedAvatar.VideoUrl,
                ProfileBackgroundUrl = profileBackground.ImageUrl,
                ProfileBackgroundVideoUrl = profileBackground.VideoUrl
            };
        }
        catch (JsonException)
        {
            return new ProfileItems();
        }
        catch (HttpRequestException)
        {
            return new ProfileItems();
        }
    }

    private async Task<ProfileItem> TryGetDedicatedProfileItemAsync(
        string steamId64,
        string endpointName,
        CancellationToken cancellationToken,
        params string[] itemNames)
    {
        var url = $"https://api.steampowered.com/IPlayerService/{endpointName}/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&steamid={steamId64}&language=schinese";
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProfileItem();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("response", out var responseRoot)
                ? TryGetProfileItem(responseRoot, itemNames)
                : new ProfileItem();
        }
        catch (JsonException)
        {
            return new ProfileItem();
        }
        catch (HttpRequestException)
        {
            return new ProfileItem();
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string operationName, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new SteamApiException(BuildHttpErrorMessage(operationName, response.StatusCode));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (SteamApiException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new SteamApiException($"{operationName}失败：Steam 返回的数据格式无法解析。");
        }
        catch (HttpRequestException ex)
        {
            throw new SteamApiException($"{operationName}失败：网络请求失败。{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SteamApiException($"{operationName}失败：请求超时。");
        }
    }

    private static string BuildHttpErrorMessage(string operationName, HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{operationName}失败：Steam API Key 无效，或该账号数据无权访问。",
            (HttpStatusCode)429 => $"{operationName}失败：Steam API 请求过于频繁，请稍后再试。",
            >= HttpStatusCode.InternalServerError => $"{operationName}失败：Steam 服务暂时不可用，请稍后再试。",
            _ => $"{operationName}失败：Steam API 返回 HTTP {(int)statusCode}。"
        };
    }

    private static string MapPersonaState(int state)
    {
        return state switch
        {
            0 => "离线",
            1 => "在线",
            2 => "忙碌",
            3 => "离开",
            4 => "打盹",
            5 => "想交易",
            6 => "想游戏",
            _ => "未知"
        };
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static DateTimeOffset? TryGetUnixTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || !property.TryGetInt64(out var unixTime) || unixTime <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixTime);
    }

    private static ProfileItem TryGetProfileItem(JsonElement root, params string[] itemNames)
    {
        JsonElement? item = null;
        foreach (var itemName in itemNames)
        {
            if (root.TryGetProperty(itemName, out var namedItem) && namedItem.ValueKind == JsonValueKind.Object)
            {
                item = namedItem;
                break;
            }
        }

        if (item is null && root.ValueKind == JsonValueKind.Object)
        {
            var objectChildren = root
                .EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.Object)
                .ToList();
            if (objectChildren.Count == 1)
            {
                item = objectChildren[0].Value;
            }
        }

        if (item is null)
        {
            return new ProfileItem();
        }

        var itemElement = item.Value;
        var imagePath = GetFirstNonEmptyString(itemElement, "image_large", "image_small", "image")
            ?? TryFindStringByNames(itemElement, "image_large", "image_small", "image");
        var videoPath = GetFirstNonEmptyString(
                itemElement,
                "movie_mp4",
                "movie_mp4_small",
                "video_mp4",
                "mp4",
                "movie_webm",
                "movie_webm_small",
                "video_webm",
                "webm",
                "movie",
                "video")
            ?? TryFindStringByNames(
                itemElement,
                "movie_mp4",
                "movie_mp4_small",
                "video_mp4",
                "mp4",
                "movie_webm",
                "movie_webm_small",
                "video_webm",
                "webm",
                "movie",
                "video");

        return new ProfileItem
        {
            ImageUrl = NormalizeSteamCommunityAssetUrl(imagePath),
            VideoUrl = NormalizeSteamCommunityAssetUrl(videoPath)
        };
    }

    private static string? GetFirstNonEmptyString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryFindStringByNames(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && propertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                var nested = TryFindStringByNames(property.Value, propertyNames);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = TryFindStringByNames(item, propertyNames);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string NormalizeSteamCommunityAssetUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return value;
        }

        return $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/{value.TrimStart('/')}";
    }

    private sealed class ProfileItem
    {
        public string ImageUrl { get; init; } = string.Empty;

        public string VideoUrl { get; init; } = string.Empty;

        public ProfileItem Merge(ProfileItem fallback)
        {
            return new ProfileItem
            {
                ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? fallback.ImageUrl : ImageUrl,
                VideoUrl = string.IsNullOrWhiteSpace(VideoUrl) ? fallback.VideoUrl : VideoUrl
            };
        }
    }

    private sealed class ProfileItems
    {
        public string AvatarFrameUrl { get; init; } = string.Empty;

        public string AvatarFrameVideoUrl { get; init; } = string.Empty;

        public string AnimatedAvatarUrl { get; init; } = string.Empty;

        public string AnimatedAvatarVideoUrl { get; init; } = string.Empty;

        public string ProfileBackgroundUrl { get; init; } = string.Empty;

        public string ProfileBackgroundVideoUrl { get; init; } = string.Empty;
    }
}
