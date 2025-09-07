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
    private readonly MainForm? _mainForm;
    private readonly WpfEffectOverlay? _overlay;
    private readonly TwitchEffectSettings _settings;
    private readonly AudioPlayer _audioPlayer;
    
    public bool StackEffects { get; set; } = true;
    public bool QueueEffects { get; set; } = false;
    
    // Events for Twitch integration
    public event EventHandler<TwitchEffectEventArgs>? EffectApplied;
    public event EventHandler<string>? EffectStatusChanged;
    
    public EffectManager(MainForm? mainForm, TwitchEffectSettings? settings = null)
    {
        _mainForm = mainForm;
        _settings = settings ?? new TwitchEffectSettings();
        
        // Create overlay safely - make it optional if WPF isn't available
        try
        {
            _overlay = new WpfEffectOverlay(mainForm);
            Debug.WriteLine("EffectManager: WPF overlay created successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: Could not create WPF overlay: {ex.Message}");
            // For now, let's create a minimal overlay or skip overlay functionality
            // This allows the application to start even if WPF overlay fails
            _overlay = null!; // We'll handle null checks in overlay calls
        }
        
        _audioPlayer = new AudioPlayer();
        
        // Set default effect modes
        StackEffects = _settings.StackEffects;
        QueueEffects = _settings.QueueEffects;
        
        Debug.WriteLine($"EffectManager: Initialized with StackEffects={StackEffects}, QueueEffects={QueueEffects}");
        
        // Ensure effect folders exist using configurable directories
        _settings.EnsureDirectoriesExist();
    }
    
    /// <summary>
    /// Handles Twitch subscription events (single or gift subs)
    /// </summary>
    public async Task HandleTwitchSubscription(string username, SubTier subTier, int giftCount = 1)
    {
        Debug.WriteLine($"EffectManager.HandleTwitchSubscription: User={username}, Tier={subTier}, Count={giftCount}");
        
        var duration = _settings.GetSubEffectDuration(subTier);
        var effects = _settings.GetMultipleRandomEffects(giftCount, _random);
        
        Debug.WriteLine($"EffectManager: Duration={duration.TotalSeconds}s, Effects found={effects.Count}");
        
        if (effects.Count == 0)
        {
            Debug.WriteLine("EffectManager: No enabled effects available!");
            EffectStatusChanged?.Invoke(this, $"? No enabled effects available for {username}'s {giftCount}x {subTier} sub(s)");
            return;
        }
        
        Debug.WriteLine($"EffectManager: About to apply {effects.Count} effects for subscription");
        foreach (var effect in effects)
        {
            Debug.WriteLine($"  - Effect: {effect.Name} ({effect.Type})");
        }
        
        EffectStatusChanged?.Invoke(this, $"?? {username} triggered {effects.Count} effects with {giftCount}x {subTier} sub(s)!");
        
        await ApplyMultipleEffects(effects, username, duration, $"{giftCount}x {subTier} Sub");
    }
    
    /// <summary>
    /// Handles Twitch bits donations
    /// </summary>
    public async Task HandleTwitchBits(string username, int bitsAmount)
    {
        Debug.WriteLine($"EffectManager: Handling Twitch bits - User: {username}, Amount: {bitsAmount}");
        
        var duration = _settings.GetBitsEffectDuration(bitsAmount);
        var effect = _settings.GetRandomEnabledEffect(_random);
        
        if (effect == null)
        {
            Debug.WriteLine("EffectManager: No enabled effects available for bits donation");
            EffectStatusChanged?.Invoke(this, $"? No enabled effects available for {username}'s {bitsAmount} bits");
            return;
        }
        
        EffectStatusChanged?.Invoke(this, $"?? {username} triggered {effect.Name} with {bitsAmount} bits!");
        
        await ApplyEffect(effect, username, duration, $"{bitsAmount} Bits");
    }
    
    /// <summary>
    /// Applies multiple effects with proper spacing for gift subs
    /// </summary>
    private async Task ApplyMultipleEffects(List<TwitchEffectConfig> effects, string username, TimeSpan duration, string trigger)
    {
        Debug.WriteLine($"EffectManager.ApplyMultipleEffects: Called with {effects.Count} effects for {username} ({trigger})");
        
        if (!effects.Any())
        {
            Debug.WriteLine("EffectManager: No effects to apply");
            return;
        }
        
        Debug.WriteLine($"EffectManager: Applying {effects.Count} effects with {_settings.MultiEffectDelayMs}ms delay");
        
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            var effectUsername = effects.Count > 1 ? $"{username} ({i + 1}/{effects.Count})" : username;
            
            Debug.WriteLine($"EffectManager: Starting effect {i + 1}/{effects.Count}: {effect.Name} for {effectUsername}");
            
            // Apply the effect - WAIT for it to complete the setup phase
            try
            {
                Debug.WriteLine($"EffectManager: Calling ApplyEffect for {effect.Name}");
                await ApplyEffect(effect, effectUsername, duration, trigger);
                Debug.WriteLine($"EffectManager: Completed ApplyEffect for {effect.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EffectManager: Error in ApplyEffect for {effect.Name}: {ex.Message}");
            }
            
            // Add delay between effects (except for the last one)
            if (i < effects.Count - 1)
            {
                Debug.WriteLine($"EffectManager: Waiting {_settings.MultiEffectDelayMs}ms before next effect");
                await Task.Delay(_settings.MultiEffectDelayMs);
            }
        }
        
        Debug.WriteLine($"EffectManager.ApplyMultipleEffects: Completed launching all {effects.Count} effects");
    }
    
    public async Task ApplyEffect(TwitchEffectConfig effect, string username, TimeSpan duration)
    {
        await ApplyEffect(effect, username, duration, "Manual Test");
    }
    
    public async Task ApplyEffect(TwitchEffectConfig effect, string username, TimeSpan duration, string trigger)
    {
        Debug.WriteLine($"EffectManager.ApplyEffect: Starting {effect.Name} for {username} ({trigger}) - Duration: {duration.TotalSeconds}s");
        
        if (QueueEffects && _activeEffects.Any())
        {
            _effectQueue.Enqueue(effect);
            
            // Thread-safe overlay notification
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        _overlay?.ShowEffectNotification($"{effect.Name} queued by {username}!"));
                }
                else
                {
                    _overlay?.ShowEffectNotification($"{effect.Name} queued by {username}!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EffectManager: Error showing queue notification: {ex.Message}");
            }
            
            Debug.WriteLine($"EffectManager: Effect {effect.Name} queued for {username} ({trigger})");
            return;
        }
        
        if (!StackEffects && _activeEffects.Any(e => e.Config.Type == effect.Type))
        {
            // Don't stack same effect types
            Debug.WriteLine($"EffectManager: Effect {effect.Name} blocked (no stacking, same type active)");
            return;
        }
        
        var activeEffect = new ActiveEffect
        {
            Config = effect,
            Username = username,
            StartTime = DateTime.UtcNow,
            Duration = duration,
            EndTime = DateTime.UtcNow.Add(duration),
            Trigger = trigger
        };
        
        _activeEffects.Add(activeEffect);
        
        Debug.WriteLine($"EffectManager: Applying effect {effect.Name} for {username} ({trigger}) - Duration: {duration.TotalSeconds}s");
        
        // Show activation notification with effect name, user, and duration - THREAD-SAFE
        Debug.WriteLine($"EffectManager: Showing activation notification for {effect.Name}");
        
        try
        {
            // Simple overlay call - let the overlay handle thread safety internally
            _overlay?.ShowEffectActivationNotification(effect.Name, username, (int)duration.TotalSeconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: Error showing activation notification: {ex.Message}");
        }
        
        // Fire event for logging/statistics
        EffectApplied?.Invoke(this, new TwitchEffectEventArgs
        {
            Effect = effect,
            Username = username,
            Trigger = trigger,
            Duration = duration
        });
        
        // Apply the effect - THIS IS THE CRITICAL PART THAT WAS MISSING
        Debug.WriteLine($"EffectManager: About to execute effect {effect.Name} ({effect.Type})");
        try
        {
            await ExecuteEffect(activeEffect);
            Debug.WriteLine($"EffectManager: Completed executing effect {effect.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: ERROR executing effect {effect.Name}: {ex.Message}");
            Debug.WriteLine($"EffectManager: Exception stack trace: {ex.StackTrace}");
        }
        
        // Schedule cleanup
        _ = Task.Delay(duration).ContinueWith(_ => CleanupEffect(activeEffect));
    }
    
    private async Task ExecuteEffect(ActiveEffect effect)
    {
        Debug.WriteLine($"EffectManager.ExecuteEffect: Starting execution of {effect.Config.Name} ({effect.Config.Type})");
        
        try
        {
            switch (effect.Config.Type)
            {
                case TwitchEffectType.ChaosMode:
                    Debug.WriteLine($"EffectManager: Executing ChaosMode");
                    ApplyChaosMode(effect.Duration);
                    break;
                    
                case TwitchEffectType.RandomImage:
                    Debug.WriteLine($"EffectManager: Executing RandomImage");
                    await ApplyRandomImage(effect.Duration);
                    break;
                    
                case TwitchEffectType.BlacklistGame:
                    Debug.WriteLine($"EffectManager: Executing BlacklistGame");
                    ApplyBlacklistGame(effect.Duration);
                    break;
                    
                case TwitchEffectType.ColorFilter:
                    Debug.WriteLine($"EffectManager: Executing ColorFilter");
                    ApplyColorFilter(effect.Duration);
                    break;
                    
                case TwitchEffectType.RandomSound:
                    Debug.WriteLine($"EffectManager: Executing RandomSound");
                    await ApplyRandomSound();
                    break;
                    
                case TwitchEffectType.StaticHUD:
                    Debug.WriteLine($"EffectManager: Executing StaticHUD");
                    await ApplyStaticHUD(effect.Duration);
                    break;
                    
                case TwitchEffectType.MirrorMode:
                    Debug.WriteLine($"EffectManager: Executing MirrorMode");
                    ApplyMirrorMode(effect.Duration);
                    break;
                    
                default:
                    Debug.WriteLine($"EffectManager: Unknown effect type: {effect.Config.Type}");
                    break;
            }
            
            Debug.WriteLine($"EffectManager.ExecuteEffect: Successfully completed {effect.Config.Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: Error executing effect {effect.Config.Name}: {ex.Message}");
            Debug.WriteLine($"EffectManager: Exception details: {ex}");
            EffectStatusChanged?.Invoke(this, $"? Error applying {effect.Config.Name}: {ex.Message}");
        }
    }
    
    private void ApplyChaosMode(TimeSpan duration)
    {
        // Check if MainForm is available (might be null in test mode)
        if (_mainForm == null)
        {
            Debug.WriteLine("Chaos Shuffling: MainForm is null (test mode) - skipping chaos mode effect");
            return;
        }
        
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
        
        // Thread-safe overlay call
        try
        {
            _overlay?.ShowMovingImage(selectedImage, duration);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyRandomImage: Error showing overlay: {ex.Message}");
        }
    }
    
    private void ApplyBlacklistGame(TimeSpan duration)
    {
        // Check if MainForm is available (might be null in test mode)
        if (_mainForm == null)
        {
            Debug.WriteLine("Game Blacklist: MainForm is null (test mode) - skipping blacklist effect");
            return;
        }
        
        var gameNames = _mainForm.GetTargetGameNames();
        if (gameNames.Count == 0) 
        {
            Debug.WriteLine("Game Blacklist: No target games available to blacklist");
            return;
        }
        
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
        
        // Thread-safe overlay call
        try
        {
            _overlay?.ShowColorFilter(randomColor, duration);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyColorFilter: Error showing overlay: {ex.Message}");
        }
    }
    
    private async Task ApplyRandomSound()
    {
        // Get all supported audio files
        var soundFiles = new List<string>();
        
        Debug.WriteLine($"ApplyRandomSound: Scanning '{_settings.SoundsDirectory}' for audio files...");
        
        if (Directory.Exists(_settings.SoundsDirectory))
        {
            foreach (var supportedExtension in AudioPlayer.GetSupportedExtensions())
            {
                var files = Directory.GetFiles(_settings.SoundsDirectory, $"*{supportedExtension}");
                soundFiles.AddRange(files);
                Debug.WriteLine($"ApplyRandomSound: Found {files.Length} {supportedExtension} files");
            }
        }
        else
        {
            Debug.WriteLine($"ApplyRandomSound: Directory '{_settings.SoundsDirectory}' does not exist");
        }
        
        Debug.WriteLine($"ApplyRandomSound: Found {soundFiles.Count} total supported audio files in '{_settings.SoundsDirectory}' folder");
        
        if (soundFiles.Count == 0)
        {
            Debug.WriteLine($"ApplyRandomSound: No supported audio files found in {_settings.SoundsDirectory} folder");
            
            // Additional debugging: Check if directory exists and list all files
            if (Directory.Exists(_settings.SoundsDirectory))
            {
                var allFiles = Directory.GetFiles(_settings.SoundsDirectory);
                Debug.WriteLine($"ApplyRandomSound: Directory '{_settings.SoundsDirectory}' exists and contains {allFiles.Length} total files:");
                foreach (var file in allFiles)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var isSupported = AudioPlayer.IsSupportedFormat(ext);
                    Debug.WriteLine($"  - {Path.GetFileName(file)} ({ext}) - Supported: {isSupported}");
                }
            }
            else
            {
                Debug.WriteLine($"Directory '{_settings.SoundsDirectory}' does not exist");
            }
            
            return;
        }
        
        Debug.WriteLine($"ApplyRandomSound: Selecting random sound from {soundFiles.Count} available files:");
        for (int i = 0; i < Math.Min(soundFiles.Count, 10); i++) // Show up to 10 files to avoid spam
        {
            Debug.WriteLine($"  [{i}] {Path.GetFileName(soundFiles[i])}");
        }
        if (soundFiles.Count > 10)
        {
            Debug.WriteLine($"  ... and {soundFiles.Count - 10} more files");
        }
        
        var selectedSound = soundFiles[_random.Next(soundFiles.Count)];
        var soundName = Path.GetFileNameWithoutExtension(selectedSound);
        var extension = Path.GetExtension(selectedSound).ToLowerInvariant();
        
        Debug.WriteLine($"ApplyRandomSound: Selected sound: {Path.GetFileName(selectedSound)} ({extension})");
        
        // Show sound name overlay - Thread-safe
        try
        {
            _overlay?.ShowSoundNotification(soundName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyRandomSound: Error showing sound overlay: {ex.Message}");
        }
        
        // Play sound using the enhanced AudioPlayer
        try
        {
            Debug.WriteLine($"ApplyRandomSound: Starting playback of {extension} file: {soundName}");
            await _audioPlayer.PlayAsync(selectedSound);
            Debug.WriteLine($"ApplyRandomSound: Successfully started playback of {extension} file: {soundName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyRandomSound: Failed to play sound {selectedSound}: {ex.Message}");
            Debug.WriteLine($"ApplyRandomSound: Exception details: {ex}");
        }
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
        
        // Thread-safe overlay call
        try
        {
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    _overlay?.ShowStaticImage(selectedHUD, duration));
            }
            else
            {
                _overlay?.ShowStaticImage(selectedHUD, duration);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyStaticHUD: Error showing overlay: {ex.Message}");
        }
    }
    
    private void ApplyMirrorMode(TimeSpan duration)
    {
        // Thread-safe overlay call
        try
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    _overlay?.ShowMirrorEffect(duration));
            }
            else
            {
                _overlay?.ShowMirrorEffect(duration);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyMirrorMode: Error showing overlay: {ex.Message}");
        }
    }
    
    private void CleanupEffect(ActiveEffect effect)
    {
        _activeEffects.Remove(effect);
        
        Debug.WriteLine($"EffectManager: Cleaned up effect {effect.Config.Name} for {effect.Username} ({effect.Trigger})");
        
        // Process next effect in queue
        if (_effectQueue.Count > 0)
        {
            var nextEffect = _effectQueue.Dequeue();
            _ = Task.Run(() => ApplyEffect(nextEffect, "Queued", nextEffect.Duration, "Queue"));
        }
    }
    
    /// <summary>
    /// Gets current active effects for display/monitoring
    /// </summary>
    public List<ActiveEffect> GetActiveEffects() => new(_activeEffects);
    
    /// <summary>
    /// Gets count of queued effects
    /// </summary>
    public int GetQueuedEffectCount() => _effectQueue.Count;
    
    /// <summary>
    /// Checks if a specific effect type is currently active
    /// </summary>
    public bool IsEffectTypeActive(TwitchEffectType effectType) => 
        _activeEffects.Any(e => e.Config.Type == effectType);
    
    public void ClearAllEffects()
    {
        _overlay?.ClearAllEffects();
        _activeEffects.Clear();
        _effectQueue.Clear();
        Debug.WriteLine("Cleared all effects via EffectManager");
        EffectStatusChanged?.Invoke(this, "?? All effects cleared");
    }
    
    public void Dispose()
    {
        _audioPlayer?.Dispose();
        _overlay?.Dispose();
    }
}

public class ActiveEffect
{
    public TwitchEffectConfig Config { get; set; } = new();
    public string Username { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string Trigger { get; set; } = ""; // What triggered this effect (subscription, bits, etc.)
    
    /// <summary>
    /// Gets remaining time for this effect
    /// </summary>
    public TimeSpan RemainingTime => EndTime > DateTime.UtcNow ? EndTime - DateTime.UtcNow : TimeSpan.Zero;
    
    /// <summary>
    /// Gets progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage
    {
        get
        {
            var totalDuration = EndTime - StartTime;
            var elapsed = DateTime.UtcNow - StartTime;
            
            if (totalDuration.TotalMilliseconds <= 0) return 100;
            
            var progress = (elapsed.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100;
            return Math.Max(0, Math.Min(100, progress));
        }
    }
    
    /// <summary>
    /// Checks if this effect is still active
    /// </summary>
    public bool IsActive => DateTime.UtcNow < EndTime;
}