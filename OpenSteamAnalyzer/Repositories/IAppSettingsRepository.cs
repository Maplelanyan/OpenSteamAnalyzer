using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Repositories;

public interface IAppSettingsRepository
{
    string? LastLoadError { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}
