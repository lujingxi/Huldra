using System.Text.Json;
using Huldra.Agent.Models;

namespace Huldra.Agent.Services;

public class ConfigManager
{
    private readonly string _configFilePath;
    private readonly string _appStateFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigManager(string? customBasePath = null)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string baseDir = customBasePath ?? Path.Combine(userProfile, ".huldra");
        Directory.CreateDirectory(baseDir);

        _configFilePath = Path.Combine(baseDir, "config.json");
        _appStateFilePath = Path.Combine(baseDir, "state.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    // --- Explicit Configuration ---
    public async Task<AppConfig> LoadConfigAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            var defaultConfig = new AppConfig();
            await SaveConfigAsync(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(_configFilePath, json);
    }

    // --- Implicit Application State ---
    public async Task<AppState> LoadAppStateAsync()
    {
        if (!File.Exists(_appStateFilePath))
        {
            var defaultState = new AppState();
            await SaveAppStateAsync(defaultState);
            return defaultState;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_appStateFilePath);
            return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public async Task SaveAppStateAsync(AppState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_appStateFilePath, json);
    }
}
