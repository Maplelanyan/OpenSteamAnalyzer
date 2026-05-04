namespace OpenSteamAnalyzer.Models;

public sealed class AppSettings
{
    public string SteamInput { get; set; } = string.Empty;

    public string SteamApiKey { get; set; } = string.Empty;

    public string EncryptedSteamApiKey { get; set; } = string.Empty;
}
