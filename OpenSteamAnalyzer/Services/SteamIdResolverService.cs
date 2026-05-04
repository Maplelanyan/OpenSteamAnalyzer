using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenSteamAnalyzer.Services;

public sealed class SteamIdResolverService : ISteamIdResolverService
{
    private static readonly Regex SteamId64Regex = new(@"^\d{17}$", RegexOptions.Compiled);
    private static readonly Regex ProfileUrlRegex = new(@"steamcommunity\.com/profiles/(?<id>\d{17})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VanityUrlRegex = new(@"steamcommunity\.com/id/(?<name>[^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly SteamApiOptions _options;

    public SteamIdResolverService(HttpClient httpClient, SteamApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> ResolveSteamId64Async(string input, CancellationToken cancellationToken)
    {
        var value = input.Trim();
        if (SteamId64Regex.IsMatch(value))
        {
            return value;
        }

        var profileMatch = ProfileUrlRegex.Match(value);
        if (profileMatch.Success)
        {
            return profileMatch.Groups["id"].Value;
        }

        var vanityMatch = VanityUrlRegex.Match(value);
        if (vanityMatch.Success)
        {
            return await ResolveVanityUrlAsync(vanityMatch.Groups["name"].Value, cancellationToken);
        }

        throw new InvalidOperationException("请输入 SteamID64，或 steamcommunity.com/profiles / steamcommunity.com/id 主页链接。");
    }

    private async Task<string> ResolveVanityUrlAsync(string vanityName, CancellationToken cancellationToken)
    {
        _options.EnsureApiKey();

        var url = $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={Uri.EscapeDataString(_options.ApiKey)}&vanityurl={Uri.EscapeDataString(vanityName)}";
        using var document = await GetJsonAsync(url, "解析 Steam 个人主页链接", cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out var response))
        {
            throw new SteamApiException("解析 Steam 个人主页链接失败：Steam API 未返回有效数据。");
        }

        var success = response.TryGetProperty("success", out var successElement) && successElement.GetInt32() == 1;
        if (!success || !response.TryGetProperty("steamid", out var steamIdElement))
        {
            throw new SteamApiException("无法解析该 Steam 个人主页链接。");
        }

        return steamIdElement.GetString() ?? throw new SteamApiException("Steam API 未返回 SteamID64。");
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
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{operationName}失败：Steam API Key 无效。",
            (HttpStatusCode)429 => $"{operationName}失败：Steam API 请求过于频繁，请稍后再试。",
            >= HttpStatusCode.InternalServerError => $"{operationName}失败：Steam 服务暂时不可用，请稍后再试。",
            _ => $"{operationName}失败：Steam API 返回 HTTP {(int)statusCode}。"
        };
    }
}
