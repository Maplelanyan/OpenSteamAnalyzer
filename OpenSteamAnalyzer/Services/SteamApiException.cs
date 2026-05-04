namespace OpenSteamAnalyzer.Services;

public sealed class SteamApiException : Exception
{
    public SteamApiException(string message)
        : base(message)
    {
    }
}
