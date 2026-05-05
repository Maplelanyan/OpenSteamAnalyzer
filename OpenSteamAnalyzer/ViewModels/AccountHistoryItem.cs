namespace OpenSteamAnalyzer.ViewModels;

public sealed class AccountHistoryItem
{
    public AccountHistoryItem(string value)
    {
        Value = value;
        DisplayText = BuildDisplayText(value);
    }

    public string Value { get; }

    public string DisplayText { get; }

    public override string ToString()
    {
        return DisplayText;
    }

    public static string BuildDisplayText(string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.StartsWith("steamcommunity.com/", StringComparison.OrdinalIgnoreCase)
            ? $"https://{value}"
            : value;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && uri.Host.EndsWith("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length >= 2
                && (segments[0].Equals("id", StringComparison.OrdinalIgnoreCase)
                    || segments[0].Equals("profiles", StringComparison.OrdinalIgnoreCase)))
            {
                return Uri.UnescapeDataString(segments[1]);
            }
        }

        return value
            .TrimEnd('/')
            .Replace("https://steamcommunity.com/id/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://steamcommunity.com/id/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("steamcommunity.com/id/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("https://steamcommunity.com/profiles/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://steamcommunity.com/profiles/", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("steamcommunity.com/profiles/", string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
