using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using BetterGameShuffler; // Add this to access Settings class

namespace BetterGameShuffler.TwitchIntegration;

public class TwitchEffectSettings
{
    private const string REGISTRY_KEY = @"SOFTWARE\BetterGameShuffler";

    // Stack/Queue effects settings with registry persistence (using main Settings class)
    public bool StackEffects
    {
        get => GetRegistryBool("StackEffects", true);
        set => SetRegistryValue("StackEffects", value);
    }

    public bool QueueEffects
    {
        get => GetRegistryBool("QueueEffects", false);
        set => SetRegistryValue("QueueEffects", value);
    }
    public int DefaultEffectDurationSeconds { get; set; } = 30;
    public int MinBitsForEffect { get; set; } = 50;

    // Ban Game Settings with Registry persistence
    public int BanGameShufflesTier1
    {
        get => GetRegistryInt("BanGameShufflesTier1", 3);
        set => SetRegistryValue("BanGameShufflesTier1", value);
    }

    public int BanGameShufflesTier2
    {
        get => GetRegistryInt("BanGameShufflesTier2", 4);
        set => SetRegistryValue("BanGameShufflesTier2", value);
    }

    public int BanGameShufflesTier3
    {
        get => GetRegistryInt("BanGameShufflesTier3", 5);
        set => SetRegistryValue("BanGameShufflesTier3", value);
    }

    public int BanGameShufflesPrime
    {
        get => GetRegistryInt("BanGameShufflesPrime", 3);
        set => SetRegistryValue("BanGameShufflesPrime", value);
    }

    public int BanGameShufflesPer100Bits
    {
        get => GetRegistryInt("BanGameShufflesPer100Bits", 2);
        set => SetRegistryValue("BanGameShufflesPer100Bits", value);
    }

    // Twitch Integration Settings with Registry persistence
    public bool TwitchIntegrationEnabled
    {
        get => GetRegistryBool("TwitchIntegrationEnabled", false);
        set => SetRegistryValue("TwitchIntegrationEnabled", value);
    }

    public string TwitchChannelName
    {
        get => GetRegistryString("TwitchChannelName", "");
        set => SetRegistryValue("TwitchChannelName", value);
    }

    public string TwitchAccessToken
    {
        get => GetRegistryString("TwitchAccessToken", "");
        set => SetRegistryValue("TwitchAccessToken", value);
    }

    public string TwitchRefreshToken
    {
        get => GetRegistryString("TwitchRefreshToken", "");
        set => SetRegistryValue("TwitchRefreshToken", value);
    }

    public string TwitchClientId
    {
        get => GetRegistryString("TwitchClientId", "");
        set => SetRegistryValue("TwitchClientId", value);
    }

    public string TwitchClientSecret
    {
        get => GetRegistryString("TwitchClientSecret", "");
        set => SetRegistryValue("TwitchClientSecret", value);
    }

    public bool IsAuthenticated
    {
        get => GetRegistryBool("IsAuthenticated", false);
        set => SetRegistryValue("IsAuthenticated", value);
    }

    // Effect Duration Settings (in seconds) with Registry persistence
    public int Tier1SubDuration
    {
        get => GetRegistryInt("Tier1SubDuration", 15);
        set => SetRegistryValue("Tier1SubDuration", value);
    }

    public int Tier2SubDuration
    {
        get => GetRegistryInt("Tier2SubDuration", 20);
        set => SetRegistryValue("Tier2SubDuration", value);
    }

    public int Tier3SubDuration
    {
        get => GetRegistryInt("Tier3SubDuration", 25);
        set => SetRegistryValue("Tier3SubDuration", value);
    }

    public int PrimeSubDuration
    {
        get => GetRegistryInt("PrimeSubDuration", 15);
        set => SetRegistryValue("PrimeSubDuration", value);
    }

    public int BitsPerSecond
    {
        get => GetRegistryInt("BitsPerSecond", 25);
        set => SetRegistryValue("BitsPerSecond", value);
    }

    // Effect Spacing Settings with Registry persistence
    public int MultiEffectDelayMs
    {
        get => GetRegistryInt("MultiEffectDelayMs", 500);
        set => SetRegistryValue("MultiEffectDelayMs", value);
    }

    public int MaxSimultaneousEffects
    {
        get => GetRegistryInt("MaxSimultaneousEffects", 5);
        set => SetRegistryValue("MaxSimultaneousEffects", value);
    }

    // Directory Settings for Effect Resources - Load from persistent storage
    public string ImagesDirectory
    {
        get
        {
            var value = GetRegistryString("TwitchEffects_ImagesDirectory", "images");

            // SECURITY FIX: Prevent system directory access
            if (value.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("system32"))
            {
                Debug.WriteLine($"TwitchEffectSettings.ImagesDirectory SECURITY: Blocked system path '{value}', using 'images' instead");
                value = "images";
                // Reset the registry to safe value
                SetRegistryValue("TwitchEffects_ImagesDirectory", "images");
            }

            Debug.WriteLine($"TwitchEffectSettings.ImagesDirectory GET: '{value}'");
            return value;
        }
        set
        {
            Debug.WriteLine($"TwitchEffectSettings.ImagesDirectory SET: '{value}'");
            SetRegistryValue("TwitchEffects_ImagesDirectory", value ?? "images");
        }
    }

    public string SoundsDirectory
    {
        get
        {
            var value = GetRegistryString("TwitchEffects_SoundsDirectory", "sounds");

            // SECURITY FIX: Prevent system directory access
            if (value.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("system32"))
            {
                Debug.WriteLine($"TwitchEffectSettings.SoundsDirectory SECURITY: Blocked system path '{value}', using 'sounds' instead");
                value = "sounds";
                // Reset the registry to safe value
                SetRegistryValue("TwitchEffects_SoundsDirectory", "sounds");
            }

            Debug.WriteLine($"TwitchEffectSettings.SoundsDirectory GET: '{value}'");
            return value;
        }
        set
        {
            Debug.WriteLine($"TwitchEffectSettings.SoundsDirectory SET: '{value}'");
            SetRegistryValue("TwitchEffects_SoundsDirectory", value ?? "sounds");
        }
    }

    public string HudDirectory
    {
        get
        {
            var value = GetRegistryString("TwitchEffects_HudDirectory", "hud");

            // SECURITY FIX: Prevent system directory access
            if (value.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("system32"))
            {
                Debug.WriteLine($"TwitchEffectSettings.HudDirectory SECURITY: Blocked system path '{value}', using 'hud' instead");
                value = "hud";
                // Reset the registry to safe value
                SetRegistryValue("TwitchEffects_HudDirectory", "hud");
            }

            Debug.WriteLine($"TwitchEffectSettings.HudDirectory GET: '{value}'");
            return value;
        }
        set
        {
            Debug.WriteLine($"TwitchEffectSettings.HudDirectory SET: '{value}'");
            SetRegistryValue("TwitchEffects_HudDirectory", value ?? "hud");
        }
    }

    public string GreenScreenDirectory
    {
        get
        {
            var value = GetRegistryString("TwitchEffects_GreenScreenDirectory", "greenscreen");

            // SECURITY FIX: Prevent system directory access
            if (value.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("system32"))
            {
                Debug.WriteLine($"TwitchEffectSettings.GreenScreenDirectory SECURITY: Blocked system path '{value}', using 'greenscreen' instead");
                value = "greenscreen";
                // Reset the registry to safe value
                SetRegistryValue("TwitchEffects_GreenScreenDirectory", "greenscreen");
            }

            Debug.WriteLine($"TwitchEffectSettings.GreenScreenDirectory GET: '{value}'");
            return value;
        }
        set
        {
            Debug.WriteLine($"TwitchEffectSettings.GreenScreenDirectory SET: '{value}'");
            SetRegistryValue("TwitchEffects_GreenScreenDirectory", value ?? "greenscreen");
        }
    }

    public string BlurDirectory
    {
        get
        {
            var value = GetRegistryString("TwitchEffects_BlurDirectory", "blur");

            // SECURITY FIX: Prevent system directory access
            if (value.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("system32"))
            {
                Debug.WriteLine($"TwitchEffectSettings.BlurDirectory SECURITY: Blocked system path '{value}', using 'blur' instead");
                value = "blur";
                // Reset the registry to safe value
                SetRegistryValue("TwitchEffects_BlurDirectory", "blur");
            }

            Debug.WriteLine($"TwitchEffectSettings.BlurDirectory GET: '{value}'");
            return value;
        }
        set
        {
            Debug.WriteLine($"TwitchEffectSettings.BlurDirectory SET: '{value}'");
            SetRegistryValue("TwitchEffects_BlurDirectory", value ?? "blur");
        }
    }

    public Dictionary<TwitchEffectType, TwitchEffectConfig> EffectConfigs { get; set; } = new();

    public TwitchEffectSettings()
    {
        InitializeEffectConfigs();
        LoadEffectConfigsFromRegistry();
    }

    private void InitializeEffectConfigs()
    {
        EffectConfigs = new Dictionary<TwitchEffectType, TwitchEffectConfig>
        {
            [TwitchEffectType.ChaosMode] = new()
            {
                Type = TwitchEffectType.ChaosMode,
                Name = "CHAOS SHUFFLING",
                Duration = TimeSpan.FromSeconds(30),
                Enabled = true,
                BitsRequired = 100,
                SubsRequired = 1,
                Weight = 1.0
            },
            [TwitchEffectType.RandomImage] = new()
            {
                Type = TwitchEffectType.RandomImage,
                Name = "CHAOS EMOTE",
                Duration = TimeSpan.FromSeconds(15),
                Enabled = true,
                BitsRequired = 50,
                SubsRequired = 1,
                Weight = 1.5
            },
            [TwitchEffectType.BlacklistGame] = new()
            {
                Type = TwitchEffectType.BlacklistGame,
                Name = "GAME BAN",
                Duration = TimeSpan.FromMinutes(3),
                Enabled = true,
                BitsRequired = 300,
                SubsRequired = 5,
                Weight = 1.0  // Increased from 0.3 to make it more likely to be selected
            },
            [TwitchEffectType.ColorFilter] = new()
            {
                Type = TwitchEffectType.ColorFilter,
                Name = "COLOR CHAOS",
                Duration = TimeSpan.FromSeconds(25),
                Enabled = true,
                BitsRequired = 125,
                SubsRequired = 2,
                Weight = 1.2
            },
            [TwitchEffectType.RandomSound] = new()
            {
                Type = TwitchEffectType.RandomSound,
                Name = "RANDOM SOUND",
                Duration = TimeSpan.FromSeconds(10),
                Enabled = true,
                BitsRequired = 25,
                SubsRequired = 1,
                Weight = 2.0
            },
            [TwitchEffectType.StaticHUD] = new()
            {
                Type = TwitchEffectType.StaticHUD,
                Name = "HUD OVERLAY",
                Duration = TimeSpan.FromSeconds(20),
                Enabled = true,
                BitsRequired = 100,
                SubsRequired = 1,
                Weight = 1.0
            },
            [TwitchEffectType.MirrorMode] = new()
            {
                Type = TwitchEffectType.MirrorMode,
                Name = "MIRROR MODE",
                Duration = TimeSpan.FromSeconds(20),
                Enabled = true,
                BitsRequired = 150,
                SubsRequired = 2,
                Weight = 0.8
            },
            [TwitchEffectType.GreenScreen] = new()
            {
                Type = TwitchEffectType.GreenScreen,
                Name = "GREEN SCREEN",
                Duration = TimeSpan.FromSeconds(25),
                Enabled = true,
                BitsRequired = 200,
                SubsRequired = 3,
                Weight = 1.0
            }
        };
    }

    // Registry helper methods
    private string GetRegistryString(string name, string defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            var value = key?.GetValue($"TwitchEffects_{name}")?.ToString() ?? defaultValue;
            Debug.WriteLine($"Registry GET {name}: '{value}'");
            return value;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry GET {name} error: {ex.Message}");
            return defaultValue;
        }
    }

    private bool GetRegistryBool(string name, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            var value = key?.GetValue($"TwitchEffects_{name}");
            if (value != null && bool.TryParse(value.ToString(), out bool result))
            {
                Debug.WriteLine($"Registry GET {name}: {result}");
                return result;
            }
            Debug.WriteLine($"Registry GET {name}: {defaultValue} (default)");
            return defaultValue;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry GET {name} error: {ex.Message}");
            return defaultValue;
        }
    }

    private int GetRegistryInt(string name, int defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            var value = key?.GetValue($"TwitchEffects_{name}");
            if (value != null && int.TryParse(value.ToString(), out int result))
            {
                Debug.WriteLine($"Registry GET {name}: {result}");
                return result;
            }
            Debug.WriteLine($"Registry GET {name}: {defaultValue} (default)");
            return defaultValue;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry GET {name} error: {ex.Message}");
            return defaultValue;
        }
    }

    private void SetRegistryValue(string name, object value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
            key.SetValue($"TwitchEffects_{name}", value);
            Debug.WriteLine($"Registry SET {name}: {value}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry SET {name} error: {ex.Message}");
        }
    }

    public void LoadEffectConfigsFromRegistry()
    {
        foreach (var effectType in EffectConfigs.Keys.ToList())
        {
            var enabled = GetRegistryBool($"Effect_{effectType}_Enabled", EffectConfigs[effectType].Enabled);

            // FIXED: Use the property setter instead of direct assignment to trigger auto-save
            var config = EffectConfigs[effectType];

            // Temporarily store the current value to avoid triggering save during load
            var originalEnabled = config._enabled;
            config._enabled = enabled; // Set the backing field directly during load

            Debug.WriteLine($"LoadEffectConfigsFromRegistry: Loaded {effectType}.Enabled = {enabled}");
        }
    }

    private void SaveEffectConfigsToRegistry()
    {
        foreach (var kvp in EffectConfigs)
        {
            SetRegistryValue($"Effect_{kvp.Key}_Enabled", kvp.Value.Enabled);
            Debug.WriteLine($"SaveEffectConfigsToRegistry: Saved {kvp.Key}.Enabled = {kvp.Value.Enabled}");
        }
    }

    // Helper methods for directory access
    public string GetFullImagesPath() => Path.GetFullPath(ImagesDirectory);
    public string GetFullSoundsPath() => Path.GetFullPath(SoundsDirectory);
    public string GetFullHudPath() => Path.GetFullPath(HudDirectory);
    public string GetFullGreenScreenPath() => Path.GetFullPath(GreenScreenDirectory);

    public void EnsureDirectoriesExist()
    {
        var directories = new[] { ImagesDirectory, SoundsDirectory, HudDirectory, GreenScreenDirectory };
        foreach (var dir in directories)
        {
            try
            {
                // SECURITY: Additional protection against system directories
                if (string.IsNullOrEmpty(dir) ||
                    dir.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                    dir.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                    dir.Contains("system32"))
                {
                    Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: SECURITY - Blocked dangerous directory '{dir}'");
                    continue; // Skip creating this directory
                }

                if (!Directory.Exists(dir))
                {
                    Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: Creating directory '{dir}'");
                    Directory.CreateDirectory(dir);
                    Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: Successfully created '{dir}'");
                }
                else
                {
                    Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: Directory '{dir}' already exists");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: ERROR with directory '{dir}': {ex.Message}");

                // If this was a system directory causing issues, show critical error and exit
                if (dir != null && (dir.StartsWith("C:\\WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                                  dir.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase) ||
                                  dir.Contains("system32")))
                {
                    Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: CRITICAL - System directory access blocked: {dir}");
                    MessageBox.Show($"Critical Error: Registry corruption detected!\n\n" +
                                  $"The application attempted to access a Windows system directory:\n" +
                                  $"'{dir}'\n\n" +
                                  $"This has been blocked for security. The application will close and " +
                                  $"reset to safe defaults on next startup.",
                                  "Security Protection - Registry Corruption",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
                    Environment.Exit(1);
                }

                // For other directories, just log and continue
                Debug.WriteLine($"TwitchEffectSettings.EnsureDirectoriesExist: Continuing despite error with '{dir}'");
            }
        }
    }

    /// <summary>
    /// Gets enabled effects for random selection, respecting weights
    /// </summary>
    public List<TwitchEffectConfig> GetEnabledEffects()
    {
        var enabled = EffectConfigs.Values.Where(e => e.Enabled).ToList();
        Debug.WriteLine($"GetEnabledEffects: Found {enabled.Count} enabled effects");
        foreach (var effect in enabled)
        {
            Debug.WriteLine($"  - {effect.Name} (Type: {effect.Type}, Enabled: {effect.Enabled}, Weight: {effect.Weight})");
        }
        return enabled;
    }

    /// <summary>
    /// Gets a random effect from enabled effects, considering weights
    /// </summary>
    public TwitchEffectConfig? GetRandomEnabledEffect(Random? random = null, bool preferGameBan = false)
    {
        var enabledEffects = GetEnabledEffects();
        if (!enabledEffects.Any()) return null;

        random ??= new Random();

        // If preferGameBan is true and Game Ban is enabled, return it
        if (preferGameBan && enabledEffects.Any(e => e.Type == TwitchEffectType.BlacklistGame))
        {
            var gameBanEffect = enabledEffects.First(e => e.Type == TwitchEffectType.BlacklistGame);
            Debug.WriteLine("GetRandomEnabledEffect: Preferring Game Ban effect for testing");
            return gameBanEffect;
        }

        // Check if we should exclude Game Ban effects due to insufficient unbanned games
        var availableEffects = new List<TwitchEffectConfig>(enabledEffects);

        // This check would need access to the current ban state, but TwitchEffectSettings doesn't have that
        // For now, we'll handle this logic in the EffectManager instead

        // Calculate total weight
        var totalWeight = availableEffects.Sum(e => e.Weight);
        var randomValue = random.NextDouble() * totalWeight;

        // Select based on weighted probability
        var currentWeight = 0.0;
        foreach (var effect in availableEffects)
        {
            currentWeight += effect.Weight;
            if (randomValue <= currentWeight)
                return effect;
        }

        // Fallback to first enabled effect
        return availableEffects.First();
    }

    /// <summary>
    /// Gets a random effect from enabled effects, filtering out Game Ban if insufficient games available
    /// </summary>
    public TwitchEffectConfig? GetRandomEnabledEffectWithBanCheck(int availableUnbannedGames, Random? random = null)
    {
        var enabledEffects = GetEnabledEffects();
        if (!enabledEffects.Any()) return null;

        random ??= new Random();

        // Filter out Game Ban effects if only 2 or fewer games would remain after banning
        var availableEffects = enabledEffects.Where(e =>
            e.Type != TwitchEffectType.BlacklistGame || availableUnbannedGames > 2).ToList();

        if (!availableEffects.Any())
        {
            // If filtering removed all effects, return null to indicate no valid effects
            return null;
        }

        // Calculate total weight for available effects
        var totalWeight = availableEffects.Sum(e => e.Weight);
        var randomValue = random.NextDouble() * totalWeight;

        // Select based on weighted probability
        var currentWeight = 0.0;
        foreach (var effect in availableEffects)
        {
            currentWeight += effect.Weight;
            if (randomValue <= currentWeight)
                return effect;
        }

        // Fallback to first available effect
        return availableEffects.First();
    }

    /// <summary>
    /// Gets multiple random effects for gift subs, ensuring variety
    /// </summary>
    public List<TwitchEffectConfig> GetMultipleRandomEffects(int count, Random? random = null)
    {
        var enabledEffects = GetEnabledEffects();
        if (!enabledEffects.Any()) return new List<TwitchEffectConfig>();

        random ??= new Random();
        var results = new List<TwitchEffectConfig>();
        var availableEffects = new List<TwitchEffectConfig>(enabledEffects);

        for (int i = 0; i < Math.Min(count, MaxSimultaneousEffects); i++)
        {
            if (!availableEffects.Any())
            {
                // Reset available effects if we've used them all
                availableEffects = new List<TwitchEffectConfig>(enabledEffects);
            }

            // Select weighted random effect
            var totalWeight = availableEffects.Sum(e => e.Weight);
            var randomValue = random.NextDouble() * totalWeight;

            var currentWeight = 0.0;
            TwitchEffectConfig? selectedEffect = null;

            foreach (var effect in availableEffects)
            {
                currentWeight += effect.Weight;
                if (randomValue <= currentWeight)
                {
                    selectedEffect = effect;
                    break;
                }
            }

            if (selectedEffect != null)
            {
                results.Add(selectedEffect);
                // Remove from available to ensure variety (unless it's the only effect)
                if (availableEffects.Count > 1)
                {
                    availableEffects.Remove(selectedEffect);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets multiple random effects for gift subs, ensuring variety and checking game ban availability
    /// </summary>
    public List<TwitchEffectConfig> GetMultipleRandomEffectsWithBanCheck(int count, int availableUnbannedGames, Random? random = null)
    {
        var enabledEffects = GetEnabledEffects();
        Debug.WriteLine($"GetMultipleRandomEffectsWithBanCheck: Found {enabledEffects.Count} enabled effects");
        Debug.WriteLine($"GetMultipleRandomEffectsWithBanCheck: Enabled effects: {string.Join(", ", enabledEffects.Select(e => e.Name))}");

        if (!enabledEffects.Any()) return new List<TwitchEffectConfig>();

        random ??= new Random();
        var results = new List<TwitchEffectConfig>();

        // Filter out Game Ban effects if insufficient games available
        var availableEffects = enabledEffects.Where(e =>
            e.Type != TwitchEffectType.BlacklistGame || availableUnbannedGames > 2).ToList();

        Debug.WriteLine($"GetMultipleRandomEffectsWithBanCheck: After Game Ban filtering: {availableEffects.Count} effects available");
        Debug.WriteLine($"GetMultipleRandomEffectsWithBanCheck: Available effects: {string.Join(", ", availableEffects.Select(e => e.Name))}");

        if (!availableEffects.Any())
        {
            Debug.WriteLine("GetMultipleRandomEffectsWithBanCheck: No effects available after filtering!");
            return new List<TwitchEffectConfig>();
        }

        for (int i = 0; i < Math.Min(count, MaxSimultaneousEffects); i++)
        {
            if (!availableEffects.Any())
            {
                // Reset available effects if we've used them all
                availableEffects = enabledEffects.Where(e =>
                    e.Type != TwitchEffectType.BlacklistGame || availableUnbannedGames > 2).ToList();
            }

            if (!availableEffects.Any()) break; // No valid effects available

            // Select weighted random effect
            var totalWeight = availableEffects.Sum(e => e.Weight);
            var randomValue = random.NextDouble() * totalWeight;

            var currentWeight = 0.0;
            TwitchEffectConfig? selectedEffect = null;

            foreach (var effect in availableEffects)
            {
                currentWeight += effect.Weight;
                if (randomValue <= currentWeight)
                {
                    selectedEffect = effect;
                    break;
                }
            }

            if (selectedEffect != null)
            {
                results.Add(selectedEffect);
                // Remove from available to ensure variety (unless it's the only effect)
                if (availableEffects.Count > 1)
                {
                    availableEffects.Remove(selectedEffect);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Calculates effect duration based on sub tier
    /// </summary>
    public TimeSpan GetSubEffectDuration(SubTier tier)
    {
        return tier switch
        {
            SubTier.Tier1 => TimeSpan.FromSeconds(Tier1SubDuration),
            SubTier.Tier2 => TimeSpan.FromSeconds(Tier2SubDuration),
            SubTier.Tier3 => TimeSpan.FromSeconds(Tier3SubDuration),
            SubTier.Prime => TimeSpan.FromSeconds(PrimeSubDuration),
            _ => TimeSpan.FromSeconds(Tier1SubDuration)
        };
    }

    /// <summary>
    /// Calculates effect duration based on bits amount
    /// </summary>
    public TimeSpan GetBitsEffectDuration(int bits)
    {
        var seconds = Math.Max(1, bits / BitsPerSecond);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Calculates ban duration in shuffles based on sub tier
    /// </summary>
    public int GetBanShufflesForSubTier(SubTier tier)
    {
        return tier switch
        {
            SubTier.Tier1 => BanGameShufflesTier1,
            SubTier.Tier2 => BanGameShufflesTier2,
            SubTier.Tier3 => BanGameShufflesTier3,
            SubTier.Prime => BanGameShufflesPrime,
            _ => BanGameShufflesTier1
        };
    }

    /// <summary>
    /// Calculates ban duration in shuffles based on bits amount
    /// </summary>
    public int GetBanShufflesForBits(int bits)
    {
        return Math.Max(1, (bits / 100) * BanGameShufflesPer100Bits);
    }

    public TwitchEffectConfig? GetRandomSubscriptionEffect()
    {
        return GetRandomEnabledEffect();
    }

    public TwitchEffectConfig? GetEffectForBits(int bits)
    {
        var availableEffects = EffectConfigs.Values
            .Where(e => e.Enabled && bits >= e.BitsRequired)
            .OrderByDescending(e => e.BitsRequired)
            .ToList();

        return availableEffects.FirstOrDefault();
    }

    /// <summary>
    /// Forces a save of all settings to registry (useful for manual save operations)
    /// </summary>
    public void SaveAllSettings()
    {
        Debug.WriteLine("TwitchEffectSettings: SaveAllSettings called");

        // Save effect configurations
        SaveEffectConfigsToRegistry();

        // Force re-save of all properties by triggering setters
        var tempEnabled = TwitchIntegrationEnabled;
        var tempChannel = TwitchChannelName;
        var tempClientId = TwitchClientId;
        var tempClientSecret = TwitchClientSecret;
        var tempAccessToken = TwitchAccessToken;
        var tempRefreshToken = TwitchRefreshToken;
        var tempAuthenticated = IsAuthenticated;

        var tempTier1 = Tier1SubDuration;
        var tempTier2 = Tier2SubDuration;
        var tempTier3 = Tier3SubDuration;
        var tempPrime = PrimeSubDuration;
        var tempBits = BitsPerSecond;

        var tempDelay = MultiEffectDelayMs;
        var tempMax = MaxSimultaneousEffects;

        var tempBanTier1 = BanGameShufflesTier1;
        var tempBanTier2 = BanGameShufflesTier2;
        var tempBanTier3 = BanGameShufflesTier3;
        var tempBanPrime = BanGameShufflesPrime;
        var tempBanBits = BanGameShufflesPer100Bits;

        // Re-trigger setters to ensure registry save
        TwitchIntegrationEnabled = tempEnabled;
        TwitchChannelName = tempChannel;
        TwitchClientId = tempClientId;
        TwitchClientSecret = tempClientSecret;
        TwitchAccessToken = tempAccessToken;
        TwitchRefreshToken = tempRefreshToken;
        IsAuthenticated = tempAuthenticated;

        Tier1SubDuration = tempTier1;
        Tier2SubDuration = tempTier2;
        Tier3SubDuration = tempTier3;
        PrimeSubDuration = tempPrime;
        BitsPerSecond = tempBits;

        MultiEffectDelayMs = tempDelay;
        MaxSimultaneousEffects = tempMax;

        BanGameShufflesTier1 = tempBanTier1;
        BanGameShufflesTier2 = tempBanTier2;
        BanGameShufflesTier3 = tempBanTier3;
        BanGameShufflesPrime = tempBanPrime;
        BanGameShufflesPer100Bits = tempBanBits;

        Debug.WriteLine("TwitchEffectSettings: Manual save completed");
    }
}

public class TwitchEffectConfig
{
    public TwitchEffectType Type { get; set; }
    public string Name { get; set; } = "";
    public TimeSpan Duration { get; set; }

    public bool _enabled = true; // Made public for loading purposes
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value) // Only save if value actually changed
            {
                _enabled = value;
                // Auto-save when enabled state changes
                SaveEnabledState();
                Debug.WriteLine($"TwitchEffectConfig.Enabled setter: {Type} = {value}");
            }
        }
    }

    public int BitsRequired { get; set; } = 50;
    public int SubsRequired { get; set; } = 1;
    public double Weight { get; set; } = 1.0; // Probability weight for random selection

    private void SaveEnabledState()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\BetterGameShuffler");
            key.SetValue($"TwitchEffects_Effect_{Type}_Enabled", _enabled);
            Debug.WriteLine($"TwitchEffectConfig.SaveEnabledState: Saved {Type} enabled={_enabled} to registry");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEffectConfig.SaveEnabledState: Save error for {Type}: {ex.Message}");
        }
    }
}

public enum TwitchEffectType
{
    ChaosMode,
    SplitScreen,
    UpsideDown,
    RandomImage,
    BlacklistGame,
    ColorFilter,
    RandomSound,
    StaticHUD,
    MirrorMode,
    GreenScreen
}

public enum SubTier
{
    Tier1,
    Tier2,
    Tier3,
    Prime
}

public class TwitchEffectEventArgs : EventArgs
{
    public TwitchEffectConfig Effect { get; set; } = new();
    public string Username { get; set; } = "";
    public string Trigger { get; set; } = "";
    public TimeSpan Duration { get; set; }
}

public class TwitchEventArgs : EventArgs
{
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
    public int Bits { get; set; } = 0;
    public SubTier SubTier { get; set; } = SubTier.Tier1;
    public int GiftCount { get; set; } = 1;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}