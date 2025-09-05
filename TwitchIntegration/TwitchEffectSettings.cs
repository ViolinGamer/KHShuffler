using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BetterGameShuffler; // Add this to access Settings class

namespace BetterGameShuffler.TwitchIntegration;

public class TwitchEffectSettings
{
    public bool StackEffects { get; set; } = true;
    public bool QueueEffects { get; set; } = false;
    public int DefaultEffectDurationSeconds { get; set; } = 30;
    public int MinBitsForEffect { get; set; } = 50;
    
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
    
    public string BlurDirectory 
    { 
        get 
        {
            var value = Settings.BlurDirectory;
            Debug.WriteLine($"TwitchEffectSettings.BlurDirectory GET: '{value}'");
            return value;
        }
        set 
        {
            Debug.WriteLine($"TwitchEffectSettings.BlurDirectory SET: '{value}'");
            Settings.BlurDirectory = value;
        }
    }
    
    public Dictionary<TwitchEffectType, TwitchEffectConfig> EffectConfigs { get; set; } = new()
    {
        [TwitchEffectType.ChaosMode] = new() 
        { 
            Type = TwitchEffectType.ChaosMode,
            Name = "CHAOS SHUFFLING",
            Duration = TimeSpan.FromSeconds(30), 
            Enabled = true, 
            BitsRequired = 100,
            SubsRequired = 1
        },
        [TwitchEffectType.TimerDecrease] = new() 
        { 
            Type = TwitchEffectType.TimerDecrease,
            Name = "SPEED BOOST",
            Duration = TimeSpan.FromSeconds(60), 
            Enabled = true, 
            BitsRequired = 75,
            SubsRequired = 1
        },
        [TwitchEffectType.RandomImage] = new() 
        { 
            Type = TwitchEffectType.RandomImage,
            Name = "CHAOS EMOTE",
            Duration = TimeSpan.FromSeconds(15), 
            Enabled = true, 
            BitsRequired = 50,
            SubsRequired = 1
        },
        [TwitchEffectType.BlacklistGame] = new() 
        { 
            Type = TwitchEffectType.BlacklistGame,
            Name = "GAME BAN",
            Duration = TimeSpan.FromMinutes(3), 
            Enabled = true, 
            BitsRequired = 300,
            SubsRequired = 5
        },
        [TwitchEffectType.ColorFilter] = new() 
        { 
            Type = TwitchEffectType.ColorFilter,
            Name = "COLOR CHAOS",
            Duration = TimeSpan.FromSeconds(25), 
            Enabled = true, 
            BitsRequired = 125,
            SubsRequired = 2
        },
        [TwitchEffectType.RandomSound] = new() 
        { 
            Type = TwitchEffectType.RandomSound,
            Name = "SOUND STORM",
            Duration = TimeSpan.FromSeconds(10), 
            Enabled = true, 
            BitsRequired = 25,
            SubsRequired = 1
        },
        [TwitchEffectType.StaticHUD] = new() 
        { 
            Type = TwitchEffectType.StaticHUD,
            Name = "HUD OVERLAY",
            Duration = TimeSpan.FromSeconds(20), 
            Enabled = true, 
            BitsRequired = 100,
            SubsRequired = 1
        },
        [TwitchEffectType.BlurFilter] = new() 
        { 
            Type = TwitchEffectType.BlurFilter,
            Name = "BLUR VISION",
            Duration = TimeSpan.FromSeconds(15), 
            Enabled = true, 
            BitsRequired = 200,
            SubsRequired = 3
        },
        [TwitchEffectType.MirrorMode] = new() 
        { 
            Type = TwitchEffectType.MirrorMode,
            Name = "MIRROR MODE",
            Duration = TimeSpan.FromSeconds(20), 
            Enabled = true, 
            BitsRequired = 150,
            SubsRequired = 2
        }
    };
    
    // Helper methods for directory access
    public string GetFullImagesPath() => Path.GetFullPath(ImagesDirectory);
    public string GetFullSoundsPath() => Path.GetFullPath(SoundsDirectory);
    public string GetFullHudPath() => Path.GetFullPath(HudDirectory);
    public string GetFullBlurPath() => Path.GetFullPath(BlurDirectory);
    
    public void EnsureDirectoriesExist()
    {
        var directories = new[] { ImagesDirectory, SoundsDirectory, HudDirectory, BlurDirectory };
        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
    
    public TwitchEffectConfig? GetRandomSubscriptionEffect()
    {
        var availableEffects = EffectConfigs.Values
            .Where(e => e.Enabled && e.SubsRequired <= 1)
            .ToList();
            
        return availableEffects.Count > 0 
            ? availableEffects[new Random().Next(availableEffects.Count)]
            : null;
    }
    
    public TwitchEffectConfig? GetEffectForBits(int bits)
    {
        var availableEffects = EffectConfigs.Values
            .Where(e => e.Enabled && bits >= e.BitsRequired)
            .OrderByDescending(e => e.BitsRequired)
            .ToList();
            
        return availableEffects.FirstOrDefault();
    }
}

public class TwitchEffectConfig
{
    public TwitchEffectType Type { get; set; }
    public string Name { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public bool Enabled { get; set; } = true;
    public int BitsRequired { get; set; } = 50;
    public int SubsRequired { get; set; } = 1;
}

public enum TwitchEffectType
{
    ChaosMode,
    TimerDecrease,
    SplitScreen,
    UpsideDown,
    RandomImage,
    BlacklistGame,
    BlurFilter,
    ColorFilter,
    RandomSound,
    StaticHUD,
    MirrorMode
}

public class TwitchEffectEventArgs : EventArgs
{
    public TwitchEffectConfig Effect { get; set; } = new();
    public string Username { get; set; } = "";
    public string Trigger { get; set; } = "";
    public TimeSpan Duration { get; set; }
}