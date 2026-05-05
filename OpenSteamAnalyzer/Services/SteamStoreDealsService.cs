using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public sealed class SteamStoreDealsService : IStoreDealsService
{
    private const decimal FallbackUsdToCnyRate = 7.25m;

    private static readonly Regex ResultRowRegex = new(
        @"<a\s+[^>]*class=""[^""]*search_result_row[^""]*""[^>]*>.*?</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HrefRegex = new(
        @"href=""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AppIdRegex = new(
        @"data-ds-appid=""(?<value>\d+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleRegex = new(
        @"<span\s+class=""title"">(?<value>.*?)</span>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ImageRegex = new(
        @"<img[^>]+src=""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DiscountRegex = new(
        @"discount_pct[^>]*>\s*(?<value>-\d+%)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex OriginalPriceRegex = new(
        @"discount_original_price[^>]*>\s*(?<value>.*?)\s*</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FinalPriceRegex = new(
        @"discount_final_price[^>]*>\s*(?<value>.*?)\s*</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly HttpClient _httpClient;

    public SteamStoreDealsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<StoreDiscountGame>> GetCurrentDiscountsAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            return await GetSteamStoreDiscountsAsync(count, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await GetCheapSharkSteamDiscountsAsync(count, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<StoreDiscountGame>> GetSteamStoreDiscountsAsync(int count, CancellationToken cancellationToken)
    {
        var targetCount = Math.Clamp(count, 20, 500);
        var games = new List<StoreDiscountGame>();
        const int pageSize = 100;
        for (var start = 0; games.Count < targetCount; start += pageSize)
        {
            var pageGames = await GetSteamStoreDiscountPageAsync(start, Math.Min(pageSize, targetCount - games.Count), cancellationToken);
            if (pageGames.Count == 0)
            {
                break;
            }

            games.AddRange(pageGames);
        }

        return games
            .GroupBy(game => game.AppId?.ToString() ?? game.StoreUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(game => game.DiscountPercent)
            .ThenBy(game => game.Name)
            .Take(targetCount)
            .ToList();
    }

    private async Task<IReadOnlyList<StoreDiscountGame>> GetSteamStoreDiscountPageAsync(
        int start,
        int count,
        CancellationToken cancellationToken)
    {
        var url = $"https://store.steampowered.com/search/results/?query&start={start}&count={count}&dynamic_data=&sort_by=_ASC&specials=1&infinite=1&cc=cn&l=schinese";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("OpenSteamAnalyzer/1.0");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SteamApiException($"获取 Steam 打折列表失败：HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("results_html", out var resultsHtmlElement))
        {
            return Array.Empty<StoreDiscountGame>();
        }

        var resultsHtml = resultsHtmlElement.GetString() ?? string.Empty;
        return ResultRowRegex
            .Matches(resultsHtml)
            .Select(match => ParseResultRow(match.Value))
            .Where(game => game is { DiscountPercent: > 0 })
            .Cast<StoreDiscountGame>()
            .OrderByDescending(game => game.DiscountPercent)
            .ThenBy(game => game.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<StoreDiscountGame>> GetCheapSharkSteamDiscountsAsync(int count, CancellationToken cancellationToken)
    {
        var targetCount = Math.Clamp(count, 20, 500);
        var games = new List<StoreDiscountGame>();
        const int pageSize = 100;
        for (var pageNumber = 0; games.Count < targetCount; pageNumber++)
        {
            var pageGames = await GetCheapSharkSteamDiscountPageAsync(
                pageNumber,
                Math.Min(pageSize, targetCount - games.Count),
                cancellationToken);
            if (pageGames.Count == 0)
            {
                break;
            }

            games.AddRange(pageGames);
        }

        return games
            .GroupBy(game => game.AppId?.ToString() ?? game.StoreUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(game => game.DiscountPercent)
            .ThenBy(game => game.Name)
            .Take(targetCount)
            .ToList();
    }

    private async Task<IReadOnlyList<StoreDiscountGame>> GetCheapSharkSteamDiscountPageAsync(
        int pageNumber,
        int count,
        CancellationToken cancellationToken)
    {
        var url = $"https://www.cheapshark.com/api/1.0/deals?storeID=1&pageSize={count}&pageNumber={pageNumber}&sortBy=Savings";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("OpenSteamAnalyzer/1.0");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SteamApiException($"获取备用折扣列表失败：HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<StoreDiscountGame>();
        }

        return document.RootElement
            .EnumerateArray()
            .Select(ParseCheapSharkDeal)
            .Where(game => game is { DiscountPercent: > 0 })
            .Cast<StoreDiscountGame>()
            .OrderByDescending(game => game.DiscountPercent)
            .ThenBy(game => game.Name)
            .ToList();
    }

    private static StoreDiscountGame? ParseResultRow(string rowHtml)
    {
        var name = Extract(rowHtml, TitleRegex);
        var discount = Extract(rowHtml, DiscountRegex);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(discount))
        {
            return null;
        }

        return new StoreDiscountGame
        {
            AppId = int.TryParse(Extract(rowHtml, AppIdRegex), out var appId) ? appId : null,
            Name = CleanHtml(name),
            ImageUrl = WebUtility.HtmlDecode(Extract(rowHtml, ImageRegex)),
            StoreUrl = WebUtility.HtmlDecode(Extract(rowHtml, HrefRegex)),
            DiscountPercent = int.TryParse(discount.Trim().TrimStart('-').TrimEnd('%'), out var percent) ? percent : 0,
            OriginalPrice = CleanHtml(Extract(rowHtml, OriginalPriceRegex)),
            FinalPrice = CleanHtml(Extract(rowHtml, FinalPriceRegex))
        };
    }

    private static StoreDiscountGame? ParseCheapSharkDeal(JsonElement deal)
    {
        var name = GetString(deal, "title");
        var steamAppId = GetString(deal, "steamAppID");
        var normalPrice = GetString(deal, "normalPrice");
        var salePrice = GetString(deal, "salePrice");
        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(normalPrice)
            || string.IsNullOrWhiteSpace(salePrice))
        {
            return null;
        }

        var discountPercent = 0;
        if (decimal.TryParse(GetString(deal, "savings"), out var savings))
        {
            discountPercent = (int)Math.Round(savings, MidpointRounding.AwayFromZero);
        }

        return new StoreDiscountGame
        {
            AppId = int.TryParse(steamAppId, out var appId) ? appId : null,
            Name = name,
            ImageUrl = GetString(deal, "thumb"),
            StoreUrl = int.TryParse(steamAppId, out var parsedAppId)
                ? $"https://store.steampowered.com/app/{parsedAppId}/"
                : string.Empty,
            DiscountPercent = discountPercent,
            OriginalPrice = FormatUsdAsCny(normalPrice),
            FinalPrice = FormatUsdAsCny(salePrice)
        };
    }

    private static string Extract(string value, Regex regex)
    {
        var match = regex.Match(value);
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static string CleanHtml(string value)
    {
        value = Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(value).Trim();
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string FormatUsdAsCny(string value)
    {
        return decimal.TryParse(value, out var price)
            ? $"约 ¥{price * FallbackUsdToCnyRate:0.00}"
            : value;
    }
}
