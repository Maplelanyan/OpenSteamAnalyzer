namespace OpenSteamAnalyzer.ViewModels;

public sealed class AccountHistoryItem
{
    private const char Separator = '\t';

    public AccountHistoryItem(string value)
    {
        var parsed = Parse(value);
        Value = parsed.Value;
        DisplayText = parsed.DisplayText;
    }

    public string Value { get; }

    public string DisplayText { get; }

    public override string ToString()
    {
        return DisplayText;
    }

    public static string BuildDisplayText(string value)
    {
        return Parse(value).DisplayText;
    }

    public static string BuildKey(string value)
    {
        return Parse(value).Value;
    }

    public static string BuildStoredValue(string value, string displayName)
    {
        value = value.Trim();
        displayName = displayName.Trim();
        return string.IsNullOrWhiteSpace(displayName)
            ? value
            : $"{value}{Separator}{displayName}";
    }

    private static (string Value, string DisplayText) Parse(string value)
    {
        value = value.Trim();
        var separatorIndex = value.IndexOf(Separator);
        if (separatorIndex > 0)
        {
            var storedValue = value[..separatorIndex].Trim();
            var displayText = value[(separatorIndex + 1)..].Trim();
            return (storedValue, string.IsNullOrWhiteSpace(displayText) ? BuildLegacyDisplayText(storedValue) : displayText);
        }

        return (value, BuildLegacyDisplayText(value));
    }

    private static string BuildLegacyDisplayText(string value)
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
