using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using BetterGameShuffler; // Add this to access Settings class

namespace BetterGameShuffler.TwitchIntegration;

public class TwitchEffectSettings
{
    private const string REGISTRY_KEY = @"SOFTWARE\BetterGameShuffler";
    
    public bool StackEffects { get; set; } = true;
    public bool QueueEffects { get; set; } = false;
    public int DefaultEffectDurationSeconds { get; set; } = 30;
    public int MinBitsForEffect { get; set; } = 50;
    
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
            var value = Settings.ImagesDirectory;
            Debug.WriteLine($"TwitchEffectSettings.ImagesDirectory GET: '{value}'");
            return value;
        }
        set 
        {
            Debug.WriteLine($"TwitchEffectSettings.ImagesDirectory SET: '{value}'");
            Settings.ImagesDirectory = value;
        }
    }
    
    public string SoundsDirectory 
    { 
        get 
        {
            var value = Settings.SoundsDirectory;
            Debug.WriteLine($"TwitchEffectSettings.SoundsDirectory GET: '{value}'");
            return value;
        }
        set 
        {
            Debug.WriteLine($"TwitchEffectSettings.SoundsDirectory SET: '{value}'");
            Settings.SoundsDirectory = value;
        }
    }
    
    public string HudDirectory 
    { 
        get 
        {
            var value = Settings.HudDirectory;
            Debug.WriteLine($"TwitchEffectSettings.HudDirectory GET: '{value}'");
            return value;
        }
        set 
        {
            Debug.WriteLine($"TwitchEffectSettings.HudDirectory SET: '{value}'");
            Settings.HudDirectory = value;
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
                Weight = 0.3
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
            EffectConfigs[effectType].Enabled = enabled;
        }
    }
    
    private void SaveEffectConfigsToRegistry()
    {
        foreach (var kvp in EffectConfigs)
        {
            SetRegistryValue($"Effect_{kvp.Key}_Enabled", kvp.Value.Enabled);
        }
    }
    
    // Helper methods for directory access
    public string GetFullImagesPath() => Path.GetFullPath(ImagesDirectory);
    public string GetFullSoundsPath() => Path.GetFullPath(SoundsDirectory);
    public string GetFullHudPath() => Path.GetFullPath(HudDirectory);
    
    public void EnsureDirectoriesExist()
    {
        var directories = new[] { ImagesDirectory, SoundsDirectory, HudDirectory };
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
    
    /// <summary>
    /// Gets enabled effects for random selection, respecting weights
    /// </summary>
    public List<TwitchEffectConfig> GetEnabledEffects()
    {
        return EffectConfigs.Values.Where(e => e.Enabled).ToList();
    }
    
    /// <summary>
    /// Gets a random effect from enabled effects, considering weights
    /// </summary>
    public TwitchEffectConfig? GetRandomEnabledEffect(Random? random = null)
    {
        var enabledEffects = GetEnabledEffects();
        if (!enabledEffects.Any()) return null;
        
        random ??= new Random();
        
        // Calculate total weight
        var totalWeight = enabledEffects.Sum(e => e.Weight);
        var randomValue = random.NextDouble() * totalWeight;
        
        // Select based on weighted probability
        var currentWeight = 0.0;
        foreach (var effect in enabledEffects)
        {
            currentWeight += effect.Weight;
            if (randomValue <= currentWeight)
                return effect;
        }
        
        // Fallback to first enabled effect
        return enabledEffects.First();
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
        var tempAuthenticated = IsAuthenticated;
        
        var tempTier1 = Tier1SubDuration;
        var tempTier2 = Tier2SubDuration;
        var tempTier3 = Tier3SubDuration;
        var tempPrime = PrimeSubDuration;
        var tempBits = BitsPerSecond;
        
        var tempDelay = MultiEffectDelayMs;
        var tempMax = MaxSimultaneousEffects;
        
        // Re-trigger setters to ensure registry save
        TwitchIntegrationEnabled = tempEnabled;
        TwitchChannelName = tempChannel;
        TwitchClientId = tempClientId;
        TwitchClientSecret = tempClientSecret;
        TwitchAccessToken = tempAccessToken;
        IsAuthenticated = tempAuthenticated;
        
        Tier1SubDuration = tempTier1;
        Tier2SubDuration = tempTier2;
        Tier3SubDuration = tempTier3;
        PrimeSubDuration = tempPrime;
        BitsPerSecond = tempBits;
        
        MultiEffectDelayMs = tempDelay;
        MaxSimultaneousEffects = tempMax;
        
        Debug.WriteLine("TwitchEffectSettings: Manual save completed");
    }
}

public class TwitchEffectConfig
{
    public TwitchEffectType Type { get; set; }
    public string Name { get; set; } = "";
    public TimeSpan Duration { get; set; }
    
    private bool _enabled = true;
    public bool Enabled 
    { 
        get => _enabled;
        set 
        {
            _enabled = value;
            // Auto-save when enabled state changes
            SaveEnabledState();
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
            Debug.WriteLine($"TwitchEffectConfig: Saved {Type} enabled={_enabled}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEffectConfig: Save error for {Type}: {ex.Message}");
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
    MirrorMode
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