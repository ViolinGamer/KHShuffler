using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BetterGameShuffler.TwitchIntegration;

public class EffectManager : IDisposable
{
    private readonly List<ActiveEffect> _activeEffects = new();
    private readonly Queue<TwitchEffectConfig> _effectQueue = new();
    private readonly Random _random = new();
    private readonly MainForm _mainForm;
    private readonly WpfEffectOverlay _overlay;
    private readonly TwitchEffectSettings _settings;
    
    public bool StackEffects { get; set; } = true;
    public bool QueueEffects { get; set; } = false;
    
    public EffectManager(MainForm mainForm, TwitchEffectSettings? settings = null)
    {
        _mainForm = mainForm;
        _settings = settings ?? new TwitchEffectSettings();
        _overlay = new WpfEffectOverlay(mainForm); // Pass MainForm reference for game window integration
        
        // Ensure effect folders exist using configurable directories
        _settings.EnsureDirectoriesExist();
    }
    
    public async Task ApplyEffect(TwitchEffectConfig effect, string username, TimeSpan duration)
    {
        if (QueueEffects && _activeEffects.Any())
        {
            _effectQueue.Enqueue(effect);
            _overlay.ShowEffectNotification($"{effect.Name} queued by {username}!");
            return;
        }
        
        if (!StackEffects && _activeEffects.Any(e => e.Config.Type == effect.Type))
        {
            // Don't stack same effect types
            return;
        }
        
        var activeEffect = new ActiveEffect
        {
            Config = effect,
            Username = username,
            StartTime = DateTime.UtcNow,
            Duration = duration,
            EndTime = DateTime.UtcNow.Add(duration)
        };
        
        _activeEffects.Add(activeEffect);
        
        // Show activation notification with effect name, user, and duration
        _overlay.ShowEffectActivationNotification(effect.Name, username, (int)duration.TotalSeconds);
        
        // Apply the effect
        await ExecuteEffect(activeEffect);
        
        // Schedule cleanup
        _ = Task.Delay(duration).ContinueWith(_ => CleanupEffect(activeEffect));
    }
    
    private async Task ExecuteEffect(ActiveEffect effect)
    {
        switch (effect.Config.Type)
        {
            case TwitchEffectType.ChaosMode:
                ApplyChaosMode(effect.Duration);
                break;
                
            case TwitchEffectType.TimerDecrease:
                ApplyTimerDecrease(effect.Duration);
                break;
                
            case TwitchEffectType.RandomImage:
                await ApplyRandomImage(effect.Duration);
                break;
                
            case TwitchEffectType.BlacklistGame:
                ApplyBlacklistGame(effect.Duration);
                break;
                
            case TwitchEffectType.ColorFilter:
                ApplyColorFilter(effect.Duration);
                break;
                
            case TwitchEffectType.RandomSound:
                await ApplyRandomSound();
                break;
                
            case TwitchEffectType.StaticHUD:
                await ApplyStaticHUD(effect.Duration);
                break;
                
            case TwitchEffectType.BlurFilter:
                ApplyBlurFilter(effect.Duration);
                break;
                
            case TwitchEffectType.MirrorMode:
                ApplyMirrorMode(effect.Duration);
                break;
        }
    }
    
    private void ApplyChaosMode(TimeSpan duration)
    {
        var originalMin = _mainForm.MinSeconds;
        var originalMax = _mainForm.MaxSeconds;
        
        // Set chaos timing (5-second switches)
        _mainForm.SetTimerRange(5, 5);
        
        // Trigger immediate game switch when chaos shuffling activates
        try
        {
            // Use reflection to access the private ScheduleNextSwitch method with immediate parameter
            var scheduleMethod = _mainForm.GetType().GetMethod("ScheduleNextSwitch", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (scheduleMethod != null)
            {
                scheduleMethod.Invoke(_mainForm, new object[] { true }); // immediate = true
                Debug.WriteLine("Chaos Shuffling: Triggered immediate game switch");
            }
            else
            {
                Debug.WriteLine("Chaos Shuffling: Could not find ScheduleNextSwitch method for immediate switch");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chaos Shuffling: Failed to trigger immediate switch: {ex.Message}");
        }
        
        Debug.WriteLine($"Chaos Shuffling activated: Timer changed from {originalMin}-{originalMax}s to 5s for {duration.TotalSeconds}s");
        
        // Schedule restoration of original timers
        Task.Delay(duration).ContinueWith(_ =>
        {
            _mainForm.BeginInvoke(new Action(() => 
            {
                _mainForm.SetTimerRange(originalMin, originalMax);
                Debug.WriteLine($"Chaos Shuffling ended: Timer restored to {originalMin}-{originalMax}s");
            }));
        });
    }
    
    private void ApplyTimerDecrease(TimeSpan duration)
    {
        var originalMin = _mainForm.MinSeconds;
        var originalMax = _mainForm.MaxSeconds;
        
        // Decrease timers by 50%
        var newMin = Math.Max(1, originalMin / 2);
        var newMax = Math.Max(2, originalMax / 2);
        
        _mainForm.SetTimerRange(newMin, newMax);
        
        Task.Delay(duration).ContinueWith(_ =>
        {
            _mainForm.BeginInvoke(new Action(() => _mainForm.SetTimerRange(originalMin, originalMax)));
        });
    }
    
    private async Task ApplyRandomImage(TimeSpan duration)
    {
        // Get the current working directory for debugging
        var currentDirectory = Directory.GetCurrentDirectory();
        var imagesPath = Path.Combine(currentDirectory, _settings.ImagesDirectory);
        var fullImagesPath = Path.GetFullPath(_settings.ImagesDirectory);
        
        Debug.WriteLine($"ApplyRandomImage Debug Info:");
        Debug.WriteLine($"  Current working directory: {currentDirectory}");
        Debug.WriteLine($"  Settings ImagesDirectory: {_settings.ImagesDirectory}");
        Debug.WriteLine($"  Images path (combined): {imagesPath}");
        Debug.WriteLine($"  Images path (full): {fullImagesPath}");
        Debug.WriteLine($"  Directory.Exists(settings.ImagesDirectory): {Directory.Exists(_settings.ImagesDirectory)}");
        Debug.WriteLine($"  Directory.Exists(imagesPath): {Directory.Exists(imagesPath)}");
        Debug.WriteLine($"  Directory.Exists(fullImagesPath): {Directory.Exists(fullImagesPath)}");
        
        var imageFiles = ImageLoader.GetSupportedImageFiles(_settings.ImagesDirectory);
        
        Debug.WriteLine($"ApplyRandomImage: Found {imageFiles.Length} supported image files in '{_settings.ImagesDirectory}' folder");
        
        if (imageFiles.Length == 0)
        {
            Debug.WriteLine($"No supported image files found in {_settings.ImagesDirectory} folder");
            
            // Check multiple possible image directory locations
            var possiblePaths = new[]
            {
                _settings.ImagesDirectory,
                Path.Combine(currentDirectory, _settings.ImagesDirectory),
                Path.Combine(Environment.CurrentDirectory, _settings.ImagesDirectory),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.ImagesDirectory),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", _settings.ImagesDirectory)
            };
            
            foreach (var path in possiblePaths)
            {
                Debug.WriteLine($"Checking path: {path}");
                if (Directory.Exists(path))
                {
                    var allFiles = Directory.GetFiles(path);
                    Debug.WriteLine($"  Directory exists and contains {allFiles.Length} total files:");
                    foreach (var file in allFiles)
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        var isSupported = ImageLoader.IsSupportedImageFormat(file);
                        Debug.WriteLine($"    - {Path.GetFileName(file)} ({ext}) - Supported: {isSupported}");
                    }
                }
                else
                {
                    Debug.WriteLine($"  Directory does not exist");
                }
            }
            
            return;
        }
        
        Debug.WriteLine($"Selecting random image from {imageFiles.Length} available files:");
        for (int i = 0; i < imageFiles.Length; i++)
        {
            Debug.WriteLine($"  [{i}] {Path.GetFileName(imageFiles[i])}");
        }
        
        // Try to load an image, with fallback logic for WebP issues
        string? selectedImage = null;
        int attempts = 0;
        int maxAttempts = Math.Min(5, imageFiles.Length); // Try up to 5 files or all files if fewer
        
        while (selectedImage == null && attempts < maxAttempts)
        {
            var candidateImage = imageFiles[_random.Next(imageFiles.Length)];
            attempts++;
            
            Debug.WriteLine($"Attempt {attempts}: Trying to load {Path.GetFileName(candidateImage)}");
            
            // Test if the image can be loaded before using it
            var testImage = ImageLoader.LoadImage(candidateImage);
            if (testImage != null)
            {
                testImage.Dispose(); // Clean up test image
                selectedImage = candidateImage;
                Debug.WriteLine($"? Successfully verified image: {Path.GetFileName(selectedImage)}");
            }
            else
            {
                Debug.WriteLine($"? Failed to load image: {Path.GetFileName(candidateImage)}, trying another...");
            }
        }
        
        if (selectedImage == null)
        {
            Debug.WriteLine($"? Failed to load any image after {attempts} attempts. All images may be corrupted or unsupported.");
            return;
        }
        
        Debug.WriteLine($"Selected image: {Path.GetFileName(selectedImage)}");
        
        _overlay.ShowMovingImage(selectedImage, duration);
        await Task.CompletedTask;
    }
    
    private void ApplyBlacklistGame(TimeSpan duration)
    {
        var gameNames = _mainForm.GetTargetGameNames();
        if (gameNames.Count == 0) return;
        
        var randomGame = gameNames[_random.Next(gameNames.Count)];
        _mainForm.BlacklistGame(randomGame, duration);
        
        Debug.WriteLine($"Blacklisted {randomGame} for {duration.TotalMinutes:F1} minutes");
    }
    
    private void ApplyColorFilter(TimeSpan duration)
    {
        // Generate a completely random color using random RGB values
        var red = _random.Next(0, 256);
        var green = _random.Next(0, 256);
        var blue = _random.Next(0, 256);
        
        // Use 30% opacity (255 * 0.30 = 76)
        var randomColor = Color.FromArgb(76, red, green, blue);
        
        Debug.WriteLine($"Random color filter: R={red}, G={green}, B={blue}, Opacity=30%");
        
        _overlay.ShowColorFilter(randomColor, duration);
    }
    
    private async Task ApplyRandomSound()
    {
        var soundFiles = Directory.GetFiles(_settings.SoundsDirectory, "*.wav")
                                .Concat(Directory.GetFiles(_settings.SoundsDirectory, "*.mp3"))
                                .ToArray();
        
        if (soundFiles.Length == 0)
        {
            Debug.WriteLine($"No sound files found in {_settings.SoundsDirectory} folder");
            return;
        }
        
        var selectedSound = soundFiles[_random.Next(soundFiles.Length)];
        var soundName = Path.GetFileNameWithoutExtension(selectedSound);
        
        // Show sound name overlay
        _overlay.ShowSoundNotification(soundName);
        
        // Play sound
        try
        {
            if (selectedSound.EndsWith(".wav"))
            {
                var player = new SoundPlayer(selectedSound);
                player.Play();
            }
            else
            {
                // For MP3 files, you'd need a different audio library
                Debug.WriteLine($"Playing: {selectedSound} (MP3 support requires additional libraries)");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to play sound: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }
    
    private async Task ApplyStaticHUD(TimeSpan duration)
    {
        var hudFiles = ImageLoader.GetSupportedImageFiles(_settings.HudDirectory)
                              .Where(f => !f.EndsWith("hud_hide.png", StringComparison.OrdinalIgnoreCase))
                              .ToArray();
        
        Debug.WriteLine($"ApplyStaticHUD: Found {hudFiles.Length} supported HUD files in '{_settings.HudDirectory}' folder");
        
        if (hudFiles.Length == 0)
        {
            Debug.WriteLine($"No supported HUD overlay files found in {_settings.HudDirectory} folder");
            
            // Additional debugging: Check if directory exists and list all files
            if (Directory.Exists(_settings.HudDirectory))
            {
                var allFiles = Directory.GetFiles(_settings.HudDirectory);
                Debug.WriteLine($"Directory '{_settings.HudDirectory}' exists and contains {allFiles.Length} total files:");
                foreach (var file in allFiles)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var isSupported = ImageLoader.IsSupportedImageFormat(file);
                    var isHudHide = file.EndsWith("hud_hide.png", StringComparison.OrdinalIgnoreCase);
                    Debug.WriteLine($"  - {Path.GetFileName(file)} ({ext}) - Supported: {isSupported}, IsHudHide: {isHudHide}");
                }
            }
            else
            {
                Debug.WriteLine($"Directory '{_settings.HudDirectory}' does not exist");
            }
            
            return;
        }
        
        Debug.WriteLine($"Selecting random HUD from {hudFiles.Length} available files:");
        for (int i = 0; i < hudFiles.Length; i++)
        {
            Debug.WriteLine($"  [{i}] {Path.GetFileName(hudFiles[i])}");
        }
        
        var selectedHUD = hudFiles[_random.Next(hudFiles.Length)];
        Debug.WriteLine($"Selected HUD: {Path.GetFileName(selectedHUD)}");
        
        _overlay.ShowStaticImage(selectedHUD, duration);
        await Task.CompletedTask;
    }
    
    private void ApplyBlurFilter(TimeSpan duration)
    {
        _overlay.ShowBlurFilter(duration);
    }
    
    private void ApplyMirrorMode(TimeSpan duration)
    {
        _overlay.ShowMirrorEffect(duration);
    }
    
    private void CleanupEffect(ActiveEffect effect)
    {
        _activeEffects.Remove(effect);
        
        // Process next effect in queue
        if (_effectQueue.Count > 0)
        {
            var nextEffect = _effectQueue.Dequeue();
            _ = Task.Run(() => ApplyEffect(nextEffect, "Queued", nextEffect.Duration));
        }
    }
    
    public void ClearAllEffects()
    {
        _overlay?.ClearAllEffects();
        Debug.WriteLine("Cleared all effects via EffectManager");
    }
    
    public void Dispose()
    {
        _overlay?.Dispose();
    }
}

public class ActiveEffect
{
    public TwitchEffectConfig Config { get; set; } = new();
    public String Username { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}