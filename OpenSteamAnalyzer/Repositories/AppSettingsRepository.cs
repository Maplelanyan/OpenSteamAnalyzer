using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer.Repositories;

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly string _settingsPath;

    public AppSettingsRepository()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "OpenSteamAnalyzer");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public string? LastLoadError { get; private set; }

    public AppSettings Load()
    {
        LastLoadError = null;

        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var storedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            var apiKey = DecryptApiKey(storedSettings.EncryptedSteamApiKey);

            if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(storedSettings.SteamApiKey))
            {
                apiKey = storedSettings.SteamApiKey;
                Save(new AppSettings
                {
                    SteamInput = storedSettings.SteamInput,
                    SteamInputHistory = BuildHistory(storedSettings),
                    SteamApiKey = apiKey
                });
            }

            return new AppSettings
            {
                SteamInput = storedSettings.SteamInput,
                SteamInputHistory = BuildHistory(storedSettings),
                SteamApiKey = apiKey
            };
        }
        catch (Exception ex)
        {
            LastLoadError = $"本地设置读取失败：{ex.Message}";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        LastLoadError = null;

        var storedSettings = new AppSettings
        {
            SteamInput = settings.SteamInput,
            SteamInputHistory = BuildHistory(settings),
            EncryptedSteamApiKey = EncryptApiKey(settings.SteamApiKey)
        };
        var json = JsonSerializer.Serialize(storedSettings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    private static List<string> BuildHistory(AppSettings settings)
    {
        return new[] { settings.SteamInput }
            .Concat(settings.SteamInputHistory ?? Enumerable.Empty<string>())
            .Select(input => input.Trim())
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .GroupBy(AccountHistoryItem.BuildDisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(20)
            .ToList();
    }

    private static string EncryptApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    private static string DecryptApiKey(string encryptedApiKey)
    {
        if (string.IsNullOrWhiteSpace(encryptedApiKey))
        {
            return string.Empty;
        }

        var protectedBytes = Convert.FromBase64String(encryptedApiKey);
        var bytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }
}
