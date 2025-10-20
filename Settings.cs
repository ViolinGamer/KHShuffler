using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace BetterGameShuffler;

/// <summary>
/// Comprehensive settings management for all user preferences
/// Handles persistence to Windows Registry and file storage
/// </summary>
public class Settings
{
    private const string REGISTRY_KEY = @"SOFTWARE\BetterGameShuffler";

    #region Shuffle Timer Settings

    /// <summary>
    /// Minimum seconds between game switches
    /// </summary>
    public int MinSeconds
    {
        get => GetRegistryInt("MinSeconds", 10);
        set => SetRegistryValue("MinSeconds", value);
    }

    /// <summary>
    /// Maximum seconds between game switches
    /// </summary>
    public int MaxSeconds
    {
        get => GetRegistryInt("MaxSeconds", 10);
        set => SetRegistryValue("MaxSeconds", value);
    }

    #endregion

    #region Effect Stack/Queue Settings

    /// <summary>
    /// Whether to stack multiple effects simultaneously
    /// </summary>
    public bool StackEffects
    {
        get => GetRegistryBool("StackEffects", true);
        set => SetRegistryValue("StackEffects", value);
    }

    /// <summary>
    /// Whether to queue effects when one is already running
    /// </summary>
    public bool QueueEffects
    {
        get => GetRegistryBool("QueueEffects", false);
        set => SetRegistryValue("QueueEffects", value);
    }

    #endregion

    #region UI Preferences

    /// <summary>
    /// Whether dark mode is enabled
    /// </summary>
    public bool DarkModeEnabled
    {
        get => GetRegistryBool("DarkModeEnabled", false);
        set => SetRegistryValue("DarkModeEnabled", value);
    }

    /// <summary>
    /// Whether to force borderless fullscreen
    /// </summary>
    public bool ForceBorderlessFullscreen
    {
        get => GetRegistryBool("ForceBorderlessFullscreen", true);
        set => SetRegistryValue("ForceBorderlessFullscreen", value);
    }

    #endregion

    #region Custom Game Names Storage

    private readonly string _customGameNamesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterGameShuffler",
        "custom_game_names.json"
    );

    /// <summary>
    /// Dictionary of custom game names
    /// Key format: "ProcessName|WindowTitle"
    /// Value: Custom display name
    /// </summary>
    private Dictionary<string, string> _customGameNames = new();

    /// <summary>
    /// Gets all custom game names
    /// </summary>
    public Dictionary<string, string> GetCustomGameNames()
    {
        return new Dictionary<string, string>(_customGameNames);
    }

    /// <summary>
    /// Gets all custom game names (returns a copy)
    /// </summary>
    public Dictionary<string, string> GetAllCustomGameNames()
    {
        return new Dictionary<string, string>(_customGameNames);
    }

    /// <summary>
    /// Sets a custom name for a specific game
    /// </summary>
    public void SetCustomGameName(string processName, string windowTitle, string customName)
    {
        var key = $"{processName}|{windowTitle}";

        if (string.IsNullOrWhiteSpace(customName))
        {
            _customGameNames.Remove(key);
        }
        else
        {
            _customGameNames[key] = customName;
        }

        SaveCustomGameNames();
        Debug.WriteLine($"Settings: Custom game name set - Key: '{key}', Value: '{customName}'");
    }

    /// <summary>
    /// Gets a custom name for a specific game
    /// </summary>
    public string? GetCustomGameName(string processName, string windowTitle)
    {
        var key = $"{processName}|{windowTitle}";
        return _customGameNames.TryGetValue(key, out var name) ? name : null;
    }

    /// <summary>
    /// Removes a custom game name
    /// </summary>
    public void RemoveCustomGameName(string processName, string windowTitle)
    {
        var key = $"{processName}|{windowTitle}";
        if (_customGameNames.Remove(key))
        {
            SaveCustomGameNames();
            Debug.WriteLine($"Settings: Custom game name removed - Key: '{key}'");
        }
    }

    /// <summary>
    /// Loads custom game names from file
    /// </summary>
    public void LoadCustomGameNames()
    {
        try
        {
            if (!File.Exists(_customGameNamesPath))
            {
                Debug.WriteLine("Settings: No custom game names file found, starting with empty list");
                _customGameNames = new Dictionary<string, string>();
                return;
            }

            var json = File.ReadAllText(_customGameNamesPath);
            _customGameNames = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                              ?? new Dictionary<string, string>();

            Debug.WriteLine($"Settings: Loaded {_customGameNames.Count} custom game names from file");

            foreach (var kvp in _customGameNames)
            {
                Debug.WriteLine($"Settings: Custom name - '{kvp.Key}' -> '{kvp.Value}'");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error loading custom game names: {ex.Message}");
            _customGameNames = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Saves custom game names to file
    /// </summary>
    private void SaveCustomGameNames()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_customGameNamesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_customGameNames, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_customGameNamesPath, json);
            Debug.WriteLine($"Settings: Saved {_customGameNames.Count} custom game names to file");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error saving custom game names: {ex.Message}");
        }
    }

    #endregion

    #region Registry Helper Methods

    private int GetRegistryInt(string valueName, int defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            return key?.GetValue(valueName) is int value ? value : defaultValue;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error reading registry int '{valueName}': {ex.Message}");
            return defaultValue;
        }
    }

    private bool GetRegistryBool(string valueName, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            return key?.GetValue(valueName) is int value ? value != 0 : defaultValue;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error reading registry bool '{valueName}': {ex.Message}");
            return defaultValue;
        }
    }

    private void SetRegistryValue(string valueName, object value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);

            // Convert bool to int for registry storage
            if (value is bool boolValue)
            {
                key.SetValue(valueName, boolValue ? 1 : 0, RegistryValueKind.DWord);
            }
            else
            {
                key.SetValue(valueName, value);
            }

            Debug.WriteLine($"Settings: Registry value set - '{valueName}' = '{value}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error setting registry value '{valueName}': {ex.Message}");
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes settings by loading all stored preferences
    /// </summary>
    public void Initialize()
    {
        try
        {
            Debug.WriteLine("Settings: Initializing comprehensive settings system...");

            // Load custom game names from file
            LoadCustomGameNames();

            // Log current settings for debugging
            Debug.WriteLine($"Settings: Min seconds: {MinSeconds}");
            Debug.WriteLine($"Settings: Max seconds: {MaxSeconds}");
            Debug.WriteLine($"Settings: Stack effects: {StackEffects}");
            Debug.WriteLine($"Settings: Queue effects: {QueueEffects}");
            Debug.WriteLine($"Settings: Dark mode: {DarkModeEnabled}");
            Debug.WriteLine($"Settings: Force borderless: {ForceBorderlessFullscreen}");
            Debug.WriteLine($"Settings: Custom game names count: {_customGameNames.Count}");

            Debug.WriteLine("Settings: Initialization complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings: Error during initialization: {ex.Message}");
        }
    }

    #endregion
}