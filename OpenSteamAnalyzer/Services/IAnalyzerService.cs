using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public interface IAnalyzerService
{
    LibraryAnalysis Analyze(IReadOnlyList<SteamGame> games);
}
