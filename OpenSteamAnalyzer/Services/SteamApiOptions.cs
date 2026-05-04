using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public sealed class SteamApiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public static SteamApiOptions FromSettings(AppSettings settings)
    {
        var environmentApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY");
        return new SteamApiOptions
        {
            ApiKey = string.IsNullOrWhiteSpace(environmentApiKey)
                ? settings.SteamApiKey
                : environmentApiKey
        };
    }

    public void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("未配置 Steam API Key。请设置环境变量 STEAM_API_KEY 后重试。");
        }
    }
}
