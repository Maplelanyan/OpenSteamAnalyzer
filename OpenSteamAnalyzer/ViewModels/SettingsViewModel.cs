using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.Repositories;
using OpenSteamAnalyzer.Services;
using Prism.Mvvm;

namespace OpenSteamAnalyzer.ViewModels;

public sealed class SettingsViewModel : BindableBase
{
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly AppSettings _settings;
    private readonly SteamApiOptions _steamApiOptions;
    private readonly DashboardViewModel _dashboard;
    private string _steamInput;
    private string _statusText = "设置已从本地加载";

    public SettingsViewModel(
        IAppSettingsRepository settingsRepository,
        AppSettings settings,
        SteamApiOptions steamApiOptions,
        DashboardViewModel dashboard)
    {
        _settingsRepository = settingsRepository;
        _settings = settings;
        _steamApiOptions = steamApiOptions;
        _dashboard = dashboard;
        _steamInput = settings.SteamInput;
        SaveCommand = new DelegateCommand(Save);

        if (!string.IsNullOrWhiteSpace(settingsRepository.LastLoadError))
        {
            _statusText = settingsRepository.LastLoadError;
        }
        else if (UsesEnvironmentApiKey)
        {
            _statusText = "当前优先使用环境变量 STEAM_API_KEY";
        }
    }

    public DelegateCommand SaveCommand { get; }

    public string SteamInput
    {
        get => _steamInput;
        set => SetProperty(ref _steamInput, value);
    }

    public string ApiKeyInput { get; set; } = string.Empty;

    public bool HasSavedApiKey => !string.IsNullOrWhiteSpace(_settings.SteamApiKey);

    public bool UsesEnvironmentApiKey => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STEAM_API_KEY"));

    public string ApiKeyStateText
    {
        get
        {
            if (UsesEnvironmentApiKey)
            {
                return "已检测到环境变量 STEAM_API_KEY，运行时会优先使用它";
            }

            return HasSavedApiKey ? "已保存本地 Steam API Key" : "尚未保存 Steam API Key";
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void UpdateApiKeyInput(string apiKey)
    {
        ApiKeyInput = apiKey.Trim();
    }

    private void Save()
    {
        try
        {
            var apiKey = string.IsNullOrWhiteSpace(ApiKeyInput)
                ? _settings.SteamApiKey
                : ApiKeyInput;
            var updatedSettings = new AppSettings
            {
                SteamInput = SteamInput.Trim(),
                SteamInputHistory = BuildHistory(SteamInput.Trim()),
                SteamApiKey = apiKey
            };

            _settingsRepository.Save(updatedSettings);
            _settings.SteamInput = updatedSettings.SteamInput;
            _settings.SteamInputHistory = updatedSettings.SteamInputHistory;
            _settings.SteamApiKey = updatedSettings.SteamApiKey;
            _dashboard.AccountInput = updatedSettings.SteamInput;
            _dashboard.RefreshAccountHistory();
            _steamApiOptions.ApiKey = UsesEnvironmentApiKey
                ? Environment.GetEnvironmentVariable("STEAM_API_KEY") ?? string.Empty
                : updatedSettings.SteamApiKey;
            ApiKeyInput = string.Empty;

            RaisePropertyChanged(nameof(HasSavedApiKey));
            RaisePropertyChanged(nameof(ApiKeyStateText));
            StatusText = "设置已保存";
        }
        catch (Exception ex)
        {
            StatusText = $"保存设置失败：{ex.Message}";
        }
    }

    private List<string> BuildHistory(string steamInput)
    {
        return new[] { steamInput }
            .Concat(_settings.SteamInputHistory)
            .Select(input => input.Trim())
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .GroupBy(AccountHistoryItem.BuildDisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(20)
            .ToList();
    }
}
