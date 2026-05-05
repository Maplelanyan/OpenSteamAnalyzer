namespace OpenSteamAnalyzer.Models;

public sealed class StoreDiscountGame
{
    public int? AppId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public string CachedImagePath { get; init; } = string.Empty;

    public string DisplayImageSource => string.IsNullOrWhiteSpace(CachedImagePath)
        ? ImageUrl
        : CachedImagePath;

    public string StoreUrl { get; init; } = string.Empty;

    public int DiscountPercent { get; init; }

    public string DiscountText => DiscountPercent <= 0 ? "-" : $"-{DiscountPercent}%";

    public string OriginalPrice { get; init; } = string.Empty;

    public string FinalPrice { get; init; } = string.Empty;
}
