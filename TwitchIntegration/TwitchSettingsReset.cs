using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Utility to reset Twitch settings if they get corrupted
/// </summary>
public static class TwitchSettingsReset
{
    private const string REGISTRY_KEY = @"SOFTWARE\BetterGameShuffler";
    
    /// <summary>
    /// Resets all Twitch-related settings to defaults
    /// </summary>
    public static void ResetAllTwitchSettings()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
            
            Debug.WriteLine("TwitchSettingsReset: Resetting all Twitch settings to defaults");
            
            // Reset authentication settings
            key.SetValue("TwitchEffects_TwitchIntegrationEnabled", false);
            key.SetValue("TwitchEffects_TwitchChannelName", "");
            key.SetValue("TwitchEffects_TwitchAccessToken", "");
            key.SetValue("TwitchEffects_TwitchClientId", "");
            key.SetValue("TwitchEffects_TwitchClientSecret", "");
            key.SetValue("TwitchEffects_IsAuthenticated", false);
            
            // Reset duration settings to defaults
            key.SetValue("TwitchEffects_Tier1SubDuration", 15);
            key.SetValue("TwitchEffects_Tier2SubDuration", 20);
            key.SetValue("TwitchEffects_Tier3SubDuration", 25);
            key.SetValue("TwitchEffects_PrimeSubDuration", 15);
            key.SetValue("TwitchEffects_BitsPerSecond", 25);
            
            // Reset multi-effect settings
            key.SetValue("TwitchEffects_MultiEffectDelayMs", 500);
            key.SetValue("TwitchEffects_MaxSimultaneousEffects", 5);
            
            // Reset all effect enabled states to true
            var effectTypes = Enum.GetValues<TwitchEffectType>();
            foreach (var effectType in effectTypes)
            {
                key.SetValue($"TwitchEffects_Effect_{effectType}_Enabled", true);
            }
            
            Debug.WriteLine("TwitchSettingsReset: Successfully reset all settings to defaults");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsReset: Error resetting settings: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Enables all effects (useful when no effects are enabled)
    /// </summary>
    public static void EnableAllEffects()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
            
            Debug.WriteLine("TwitchSettingsReset: Enabling all effects");
            
            var effectTypes = Enum.GetValues<TwitchEffectType>();
            foreach (var effectType in effectTypes)
            {
                key.SetValue($"TwitchEffects_Effect_{effectType}_Enabled", true);
                Debug.WriteLine($"TwitchSettingsReset: Enabled {effectType}");
            }
            
            Debug.WriteLine("TwitchSettingsReset: Successfully enabled all effects");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsReset: Error enabling effects: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Lists all current Registry values for debugging
    /// </summary>
    public static void DebugRegistryValues()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            if (key == null)
            {
                Debug.WriteLine("TwitchSettingsReset: No registry key found");
                return;
            }
            
            Debug.WriteLine("TwitchSettingsReset: Current registry values:");
            
            var valueNames = key.GetValueNames();
            foreach (var valueName in valueNames)
            {
                if (valueName.StartsWith("TwitchEffects_"))
                {
                    var value = key.GetValue(valueName);
                    Debug.WriteLine($"  {valueName} = {value}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsReset: Error reading registry: {ex.Message}");
        }
    }
}