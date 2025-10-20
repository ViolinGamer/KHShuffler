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
    private WpfEffectOverlay? _overlay; // Not readonly so we can reinitialize it
    private readonly TwitchEffectSettings _settings;
    private readonly AudioPlayer _audioPlayer;

    // HUD overlay history to prevent same file twice in a row
    private string? _lastHudFile = null;

    // Game ban tracking with shuffle counts instead of time
    private readonly Dictionary<string, int> _bannedGameShuffles = new();

    // Mirror mode tracking for stacking time
    private DateTime? _mirrorModeEndTime = null;

    // Chaos mode tracking to prevent clearing during active chaos
    private DateTime? _chaosModeEndTime = null;
    private int _originalMinSeconds = 0;
    private int _originalMaxSeconds = 0;

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
            Debug.WriteLine($"EffectManager: Stack trace: {ex.StackTrace}");
            // For now, let's create a minimal overlay or skip overlay functionality
            // This allows the application to start even if WPF overlay fails
            _overlay = null; // Changed from null! to null for cleaner null checks

            // Show a visible error to the user about overlay issues
            try
            {
                if (mainForm != null)
                {
                    mainForm.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show($"⚠️ Warning: On-screen effects disabled due to overlay initialization failure.\n\n" +
                                      $"Error: {ex.Message}\n\n" +
                                      "Twitch effects will still work but visual notifications won't be shown.",
                                      "Overlay Initialization Failed",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Warning);
                    }));
                }
            }
            catch
            {
                // If even showing the error fails, just continue
            }
        }

        _audioPlayer = new AudioPlayer();

        // Set default effect modes
        StackEffects = _settings.StackEffects;
        QueueEffects = _settings.QueueEffects;

        Debug.WriteLine($"EffectManager: Initialized with StackEffects={StackEffects}, QueueEffects={QueueEffects}");

        // Ensure effect folders exist using configurable directories
        _settings.EnsureDirectoriesExist();

        // Subscribe to MainForm shuffle events to track banned games
        if (_mainForm != null)
        {
            // We'll add a shuffle event to MainForm later if needed
        }
    }

    /// <summary>
    /// Handles Twitch subscription events (single or gift subs)
    /// </summary>
    public async Task HandleTwitchSubscription(string username, SubTier subTier, int giftCount = 1)
    {
        Debug.WriteLine($"EffectManager.HandleTwitchSubscription: User={username}, Tier={subTier}, Count={giftCount}");

        var duration = _settings.GetSubEffectDuration(subTier);

        // Get the count of currently unbanned games for ban filtering
        var totalGames = _mainForm?.GetTargetGameNames()?.Count ?? 0;
        var bannedGames = _bannedGameShuffles.Count;
        var unbannedGames = totalGames - bannedGames;

        // Check if Game Ban is enabled for debugging
        var gameBanConfig = _settings.EffectConfigs.GetValueOrDefault(TwitchEffectType.BlacklistGame);
        var isGameBanEnabled = gameBanConfig?.Enabled ?? false;
        Debug.WriteLine($"EffectManager: Game Ban effect enabled: {isGameBanEnabled}");

        if (isGameBanEnabled)
        {
            Debug.WriteLine($"EffectManager: Game Ban weight: {gameBanConfig?.Weight}");
            Debug.WriteLine($"EffectManager: Game availability - Total: {totalGames}, Banned: {bannedGames}, Unbanned: {unbannedGames}");
        }

        // FIXED: Use different method selection based on test mode vs real mode
        List<TwitchEffectConfig> effects;

        if (_mainForm != null && totalGames > 0)
        {
            // Real mode with actual games - use ban checking
            Debug.WriteLine("EffectManager: Real mode - using ban check filtering for subscriptions");
            effects = _settings.GetMultipleRandomEffectsWithBanCheck(giftCount, unbannedGames, _random);
        }
        else
        {
            // Test mode OR no games loaded - allow all enabled effects including Game Ban
            Debug.WriteLine("EffectManager: Test mode or no games - allowing all enabled effects for subscriptions");
            effects = _settings.GetMultipleRandomEffects(giftCount, _random);
        }

        Debug.WriteLine($"EffectManager: Duration={duration.TotalSeconds}s, Effects found={effects.Count} (Unbanned games: {unbannedGames})");

        if (effects.Count == 0)
        {
            Debug.WriteLine("EffectManager: No enabled effects available!");
            EffectStatusChanged?.Invoke(this, $"No enabled effects available for {username}'s {giftCount}x {subTier} sub(s)");
            return;
        }

        Debug.WriteLine($"EffectManager: About to apply {effects.Count} effects for subscription");
        foreach (var effect in effects)
        {
            Debug.WriteLine($"  - Effect: {effect.Name} ({effect.Type}) - Weight: {effect.Weight}");
        }

        // Check if Game Ban was selected
        var selectedGameBan = effects.Any(e => e.Type == TwitchEffectType.BlacklistGame);
        if (isGameBanEnabled && !selectedGameBan)
        {
            Debug.WriteLine($"GAME BAN WAS NOT SELECTED despite being enabled (weight: {gameBanConfig?.Weight})");
            Debug.WriteLine($"Other effects selected: {string.Join(", ", effects.Select(e => $"{e.Name}(w:{e.Weight})"))}");

            // Show all enabled effects and their weights for debugging
            var allEnabled = _settings.GetEnabledEffects();
            Debug.WriteLine($"All enabled effects: {string.Join(", ", allEnabled.Select(e => $"{e.Name}(w:{e.Weight})"))}");
        }
        else if (selectedGameBan)
        {
            Debug.WriteLine($"GAME BAN WAS SELECTED for {username}!");
        }

        EffectStatusChanged?.Invoke(this, $"{username} triggered {effects.Count} effects with {giftCount}x {subTier} sub(s)!");

        await ApplyMultipleEffects(effects, username, duration, $"{giftCount}x {subTier} Sub", subTier);
    }

    /// <summary>
    /// Handles Twitch bits donations
    /// </summary>
    public async Task HandleTwitchBits(string username, int bitsAmount)
    {
        Debug.WriteLine($"EffectManager: Handling Twitch bits - User: {username}, Amount: {bitsAmount}");

        var duration = _settings.GetBitsEffectDuration(bitsAmount);
        var effect = GetRandomEffectWithBanCheck();

        if (effect == null)
        {
            Debug.WriteLine("EffectManager: No enabled effects available for bits donation");
            EffectStatusChanged?.Invoke(this, $"No enabled effects available for {username}'s {bitsAmount} bits");
            return;
        }

        EffectStatusChanged?.Invoke(this, $"{username} triggered {effect.Name} with {bitsAmount} bits!");

        await ApplyEffect(effect, username, duration, $"{bitsAmount} Bits", bitsAmount: bitsAmount);
    }

    /// <summary>
    /// Gets a random effect while checking if Game Ban should be excluded due to insufficient games
    /// </summary>
    private TwitchEffectConfig? GetRandomEffectWithBanCheck()
    {
        // Get the count of currently unbanned games
        var totalGames = _mainForm?.GetTargetGameNames()?.Count ?? 0;
        var bannedGames = _bannedGameShuffles.Count;
        var unbannedGames = totalGames - bannedGames;

        Debug.WriteLine($"EffectManager: Game availability check - Total: {totalGames}, Banned: {bannedGames}, Unbanned: {unbannedGames}");

        // IMPORTANT: Always get enabled effects, even in test mode
        var enabledEffects = _settings.GetEnabledEffects();
        Debug.WriteLine($"EffectManager: Found {enabledEffects.Count} enabled effects total");

        if (enabledEffects.Count == 0)
        {
            Debug.WriteLine("EffectManager: No enabled effects available!");
            return null;
        }

        // FIXED: The logic was backwards - when MainForm IS available, check for ban restrictions
        // When MainForm is NOT available (test mode), allow all effects
        if (_mainForm != null && totalGames > 0)
        {
            // Real mode with actual games - use ban checking
            Debug.WriteLine("EffectManager: Real mode - using ban check filtering");
            return _settings.GetRandomEnabledEffectWithBanCheck(unbannedGames, _random);
        }
        else
        {
            // Test mode OR no games loaded - allow all enabled effects including Game Ban
            Debug.WriteLine("EffectManager: Test mode or no games - allowing all enabled effects");
            return _settings.GetRandomEnabledEffect(_random);
        }
    }

    /// <summary>
    /// Applies multiple effects with proper spacing for gift subs
    /// </summary>
    private async Task ApplyMultipleEffects(List<TwitchEffectConfig> effects, string username, TimeSpan duration, string trigger, SubTier? subTier = null)
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
                await ApplyEffect(effect, effectUsername, duration, trigger, subTier: subTier);
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

    public async Task ApplyEffect(TwitchEffectConfig effect, string username, TimeSpan duration, string trigger, SubTier? subTier = null, int bitsAmount = 0)
    {
        Debug.WriteLine($"EffectManager.ApplyEffect: Starting {effect.Name} for {username} ({trigger}) - Duration: {duration.TotalSeconds}s");

        // Special handling for Mirror Mode - check if it's already active
        if (effect.Type == TwitchEffectType.MirrorMode && _mirrorModeEndTime.HasValue && _mirrorModeEndTime.Value > DateTime.UtcNow)
        {
            // Add time to existing mirror mode
            var additionalTime = duration;
            _mirrorModeEndTime = _mirrorModeEndTime.Value.Add(additionalTime);

            Debug.WriteLine($"EffectManager: Mirror Mode already active, extending by {additionalTime.TotalSeconds}s to {_mirrorModeEndTime}");

            // Show notification about time extension and extend the actual overlay effect
            try
            {
                var totalRemainingTime = _mirrorModeEndTime.Value - DateTime.UtcNow;
                _overlay?.ShowEffectActivationNotification($"MIRROR MODE +{(int)additionalTime.TotalSeconds}s", username, (int)totalRemainingTime.TotalSeconds);
                _overlay?.ExtendMirrorMode(additionalTime); // Use the new extend method
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EffectManager: Error showing mirror mode extension notification: {ex.Message}");
            }

            return;
        }

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
            Trigger = trigger,
            SubTier = subTier,
            BitsAmount = bitsAmount
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

        // Schedule cleanup - but DON'T clean up ban tracking for Game Ban effects
        if (effect.Type != TwitchEffectType.BlacklistGame)
        {
            _ = Task.Delay(duration).ContinueWith(_ => CleanupEffect(activeEffect));
        }
        else
        {
            // For Game Ban, only clean up the active effect, not the ban tracking
            _ = Task.Delay(duration).ContinueWith(_ => CleanupGameBanEffect(activeEffect));
        }
    }

    private async Task ExecuteEffect(ActiveEffect effect)
    {
        Debug.WriteLine($"EffectManager.ExecuteEffect: Starting execution of {effect.Config.Name} ({effect.Config.Type})");

        if (_overlay == null)
        {
            Debug.WriteLine($"❌ CRITICAL: Cannot execute {effect.Config.Name} - overlay is null!");
            EffectStatusChanged?.Invoke(this, $"❌ CRITICAL: Cannot execute {effect.Config.Name} - overlay is null!");
            return;
        }

        try
        {
            switch (effect.Config.Type)
            {
                case TwitchEffectType.ChaosMode:
                    Debug.WriteLine($"EffectManager: Executing ChaosMode");
                    ApplyChaosMode(effect.Duration);
                    Debug.WriteLine($"✅ ChaosMode execution completed");
                    break;

                case TwitchEffectType.RandomImage:
                    Debug.WriteLine($"EffectManager: Executing RandomImage");
                    ApplyRandomImage(effect.Duration);
                    Debug.WriteLine($"✅ RandomImage execution completed");
                    break;

                case TwitchEffectType.BlacklistGame:
                    Debug.WriteLine($"EffectManager: Executing BlacklistGame");
                    ApplyBlacklistGame(effect);
                    Debug.WriteLine($"✅ BlacklistGame execution completed");
                    break;

                case TwitchEffectType.ColorFilter:
                    Debug.WriteLine($"EffectManager: Executing ColorFilter");
                    ApplyColorFilter(effect.Duration);
                    Debug.WriteLine($"✅ ColorFilter execution completed");
                    break;

                case TwitchEffectType.RandomSound:
                    Debug.WriteLine($"EffectManager: Executing RandomSound");
                    await ApplyRandomSound();
                    Debug.WriteLine($"✅ RandomSound execution completed");
                    break;

                case TwitchEffectType.StaticHUD:
                    Debug.WriteLine($"EffectManager: Executing StaticHUD");
                    await ApplyStaticHUD(effect.Duration);
                    Debug.WriteLine($"✅ StaticHUD execution completed");
                    break;

                case TwitchEffectType.MirrorMode:
                    Debug.WriteLine($"EffectManager: Executing MirrorMode");
                    ApplyMirrorMode(effect.Duration);
                    break;

                case TwitchEffectType.GreenScreen:
                    Debug.WriteLine($"EffectManager: Executing GreenScreen");
                    ApplyGreenScreen(effect.Duration);
                    Debug.WriteLine($"✅ GreenScreen execution completed");
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
            EffectStatusChanged?.Invoke(this, $"Error applying {effect.Config.Name}: {ex.Message}");
        }
    }

    private void ApplyChaosMode(TimeSpan duration)
    {
        // Check if MainForm is available (might be null in test mode)
        if (_mainForm == null)
        {
            Debug.WriteLine("Chaos Shuffling: MainForm is null (test mode) - creating visual test notification");

            // In test mode, show a visual notification that chaos shuffling would be active
            try
            {
                _overlay?.ShowEffectNotification("CHAOS SHUFFLING ACTIVE (Test Mode)\nWould switch games every 5 seconds");
                EffectStatusChanged?.Invoke(this, "Chaos Shuffling test completed - would shuffle every 5 seconds with real games");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chaos Shuffling test notification error: {ex.Message}");
            }
            return;
        }

        // Check if shuffling is active
        if (!_mainForm.IsShuffling)
        {
            Debug.WriteLine("Chaos Shuffling: MainForm is not shuffling - cannot apply chaos mode");
            EffectStatusChanged?.Invoke(this, "Chaos Shuffling requires active shuffling - start the shuffler first");
            return;
        }

        // Store original timer values for restoration
        _originalMinSeconds = _mainForm.MinSeconds;
        _originalMaxSeconds = _mainForm.MaxSeconds;
        _chaosModeEndTime = DateTime.UtcNow.Add(duration);

        Debug.WriteLine($"Chaos Shuffling: Starting chaos mode - changing timer from {_originalMinSeconds}-{_originalMaxSeconds}s to 5s for {duration.TotalSeconds}s");

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
                Debug.WriteLine("Chaos Shuffling: Triggering immediate game switch");
                scheduleMethod.Invoke(_mainForm, new object[] { true }); // immediate = true
                Debug.WriteLine("Chaos Shuffling: Immediate game switch triggered successfully");
            }
            else
            {
                Debug.WriteLine("Chaos Shuffling: Could not find ScheduleNextSwitch method - trying alternative approach");

                // Alternative: Try to get a public method or property that can trigger a switch
                var switchMethod = _mainForm.GetType().GetMethod("SwitchToNextWindow",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (switchMethod != null)
                {
                    Debug.WriteLine("Chaos Shuffling: Using SwitchToNextWindow method for immediate switch");
                    switchMethod.Invoke(_mainForm, null);
                }
                else
                {
                    Debug.WriteLine("Chaos Shuffling: No switch method found - chaos mode will start with next natural switch");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chaos Shuffling: Failed to trigger immediate switch: {ex.Message}");
            Debug.WriteLine("Chaos Shuffling: Chaos mode will start with next natural switch");
        }

        Debug.WriteLine($"Chaos Shuffling: Timer changed from {_originalMinSeconds}-{_originalMaxSeconds}s to 5s for {duration.TotalSeconds}s");
        EffectStatusChanged?.Invoke(this, $"CHAOS SHUFFLING ACTIVE! Games switching every 5 seconds for {duration.TotalSeconds}s");

        // Schedule restoration of original timers with proper tracking
        Task.Delay(duration).ContinueWith(_ =>
        {
            if (_mainForm != null && !_mainForm.IsDisposed && _chaosModeEndTime.HasValue)
            {
                try
                {
                    _mainForm.BeginInvoke(new Action(() =>
                    {
                        // Only restore if this chaos mode instance is still the active one
                        if (_chaosModeEndTime.HasValue && DateTime.UtcNow >= _chaosModeEndTime.Value)
                        {
                            _mainForm.SetTimerRange(_originalMinSeconds, _originalMaxSeconds);
                            _chaosModeEndTime = null;
                            Debug.WriteLine($"Chaos Shuffling ended: Timer restored to {_originalMinSeconds}-{_originalMaxSeconds}s");
                            EffectStatusChanged?.Invoke(this, $"Chaos Shuffling ended - timer restored to {_originalMinSeconds}-{_originalMaxSeconds}s");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Chaos Shuffling cleanup error: {ex.Message}");
                }
            }
        });
    }

    private void ApplyRandomImage(TimeSpan duration)
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
                Path.Combine(System.AppContext.BaseDirectory, _settings.ImagesDirectory)
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
                Debug.WriteLine($"Successfully verified image: {Path.GetFileName(selectedImage)}");
            }
            else
            {
                Debug.WriteLine($"Failed to load image: {Path.GetFileName(candidateImage)}, trying another...");
            }
        }

        if (selectedImage == null)
        {
            Debug.WriteLine($"Failed to load any image after {attempts} attempts. All images may be corrupted or unsupported.");
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

    private void ApplyBlacklistGame(ActiveEffect effect)
    {
        // Get the process name for display (we'll generate a test name if needed)
        string processName;
        int banShuffles = 0;

        // Calculate ban duration in shuffles
        if (effect.SubTier.HasValue)
        {
            banShuffles = _settings.GetBanShufflesForSubTier(effect.SubTier.Value);
        }
        else if (effect.BitsAmount > 0)
        {
            banShuffles = _settings.GetBanShufflesForBits(effect.BitsAmount);
        }
        else
        {
            banShuffles = 3; // Default fallback
        }

        // Check if MainForm is available (might be null in test mode)
        if (_mainForm == null)
        {
            Debug.WriteLine("Game Blacklist: MainForm is null (test mode) - using test game names");

            // For test mode, create fake game names to demonstrate the functionality
            var testGames = new[] {
                "KINGDOM HEARTS FINAL MIX",
                "KINGDOM HEARTS II FINAL MIX",
                "KINGDOM HEARTS Re_Chain of Memories",
                "KINGDOM HEARTS Birth by Sleep FINAL MIX",
                "KINGDOM HEARTS Melody of Memory"
            };
            var randomTestGame = testGames[_random.Next(testGames.Length)];

            // Extract process name for display - in test mode, just use the test game name as the process name
            processName = randomTestGame;

            // Add to banned games tracking for demonstration
            if (_bannedGameShuffles.ContainsKey(randomTestGame))
            {
                _bannedGameShuffles[randomTestGame] += banShuffles;
                Debug.WriteLine($"Extended test ban for {randomTestGame}: now banned for {_bannedGameShuffles[randomTestGame]} shuffles");
                EffectStatusChanged?.Invoke(this, $"{randomTestGame} ban extended by {banShuffles} shuffles (total: {_bannedGameShuffles[randomTestGame]}) by {effect.Username}");
            }
            else
            {
                _bannedGameShuffles[randomTestGame] = banShuffles;
                Debug.WriteLine($"Test banned {randomTestGame} for {banShuffles} shuffles");
                EffectStatusChanged?.Invoke(this, $"{randomTestGame} banned for {banShuffles} shuffles by {effect.Username}");
            }

            // Show ban notification and update countdown overlay with process names for display (test mode)
            try
            {
                _overlay?.ShowGameBanNotification(processName, banShuffles, effect.Username);

                // Convert window titles to process names for display in banned games list (test mode)
                var displayBannedGames = new Dictionary<string, int>();
                foreach (var kvp in _bannedGameShuffles)
                {
                    // In test mode, use the game name directly as it's already a process name
                    displayBannedGames[kvp.Key] = kvp.Value;
                }

                _overlay?.ShowBannedGamesList(displayBannedGames);
                Debug.WriteLine($"Updated test banned games display: {displayBannedGames.Count} games shown with process names");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyBlacklistGame (test): Error showing ban overlay: {ex.Message}");
            }

            return;
        }

        // REAL MODE: Get actual games from the shuffler
        var gameNames = _mainForm.GetTargetGameNames(); // Returns process names from shuffler UI
        Debug.WriteLine($"Game Blacklist: Retrieved {gameNames.Count} unique processes from shuffler: {string.Join(", ", gameNames.Take(3))}{(gameNames.Count > 3 ? "..." : "")}");

        if (gameNames.Count == 0)
        {
            Debug.WriteLine("Game Blacklist: No target games available to blacklist in shuffler");
            EffectStatusChanged?.Invoke(this, $"No games are currently being shuffled - cannot ban games");
            return;
        }

        // CRITICAL FIX: Use the actual process names from the shuffler directly
        // These are the process names that MainForm already uses internally
        var processNames = gameNames; // Use shuffler process names directly

        Debug.WriteLine($"Game Blacklist: Using shuffler process names directly: {string.Join(", ", processNames)}");

        // Get the currently active process FIRST before filtering
        string? currentActiveProcess = null;
        try
        {
            var currentWindow = _mainForm.GetCurrentActiveGameWindow();
            if (currentWindow != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(currentWindow, out var currentPid);
                if (currentPid != 0)
                {
                    using var currentProcess = Process.GetProcessById((int)currentPid);
                    currentActiveProcess = currentProcess.ProcessName;
                    Debug.WriteLine($"Game Blacklist: Currently active process is '{currentActiveProcess}'");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Game Blacklist: Error getting current active process: {ex.Message}");
        }

        // ENHANCED PROTECTION: Exclude currently active game AND already banned games from selection
        var nonBannedGames = processNames.Where(processName =>
            !_bannedGameShuffles.ContainsKey(processName) &&
            processName != currentActiveProcess).ToList();

        Debug.WriteLine($"Game Blacklist: Found {nonBannedGames.Count} unbanned and non-active processes out of {processNames.Count} total");
        Debug.WriteLine($"Game Blacklist: Excluded active process '{currentActiveProcess}' from ban selection");

        if (nonBannedGames.Count <= 1)
        {
            Debug.WriteLine($"Game Blacklist: Protection triggered - only {nonBannedGames.Count} processes available for banning (excluding active game)");
            EffectStatusChanged?.Invoke(this, $"Cannot ban games - only {nonBannedGames.Count} non-active processes available (minimum 1 required)");

            // Show overlay notification about protection
            try
            {
                _overlay?.ShowEffectNotification($"GAME BAN PROTECTION\nCannot ban active game\nOnly {nonBannedGames.Count} other games available");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyBlacklistGame: Error showing protection overlay: {ex.Message}");
            }
            return;
        }

        // IMPROVED RANDOM SELECTION: Use a more robust random selection approach
        // Create a new Random instance with current time to ensure better randomness
        var gameBanRandom = new Random(DateTime.UtcNow.Millisecond + DateTime.UtcNow.Second * 1000);

        // Select a random process that's currently being shuffled AND not banned AND not currently active
        var randomIndex = gameBanRandom.Next(nonBannedGames.Count);
        var randomProcess = nonBannedGames[randomIndex];

        Debug.WriteLine($"Game Blacklist: Selected process '{randomProcess}' from {nonBannedGames.Count} available unbanned non-active processes");
        Debug.WriteLine($"Game Blacklist: Available processes to ban were: {string.Join(", ", nonBannedGames)}");
        Debug.WriteLine($"Game Blacklist: Successfully avoided banning currently active process '{currentActiveProcess}'");

        // ADDITIONAL DEBUG: Show current banned games
        if (_bannedGameShuffles.Count > 0)
        {
            Debug.WriteLine($"Game Blacklist: Currently banned processes: {string.Join(", ", _bannedGameShuffles.Keys)}");
        }

        // CRITICAL FIX: Store the ban using the shuffler process name directly
        // This ensures that GetBannedGameTitles() will find the banned game
        if (_bannedGameShuffles.ContainsKey(randomProcess))
        {
            _bannedGameShuffles[randomProcess] += banShuffles;
            Debug.WriteLine($"Extended ban for process {randomProcess}: now banned for {_bannedGameShuffles[randomProcess]} shuffles");
            EffectStatusChanged?.Invoke(this, $"{randomProcess} ban extended by {banShuffles} shuffles (total: {_bannedGameShuffles[randomProcess]}) by {effect.Username}");
        }
        else
        {
            _bannedGameShuffles[randomProcess] = banShuffles;
            Debug.WriteLine($"CRITICAL: BANNED PROCESS {randomProcess} for {banShuffles} shuffles - stored in _bannedGameShuffles dictionary");
            Debug.WriteLine($"CRITICAL: _bannedGameShuffles now contains {_bannedGameShuffles.Count} entries: [{string.Join(", ", _bannedGameShuffles.Keys)}]");
            EffectStatusChanged?.Invoke(this, $"{randomProcess} banned for {banShuffles} shuffles by {effect.Username}");
        }

        // Use the process name directly for display and overlay
        processName = randomProcess;

        // Show ban notification with process name and shuffle count
        try
        {
            _overlay?.ShowGameBanNotification(processName, banShuffles, effect.Username);

            // Use the banned process names directly for display
            var displayBannedGames = new Dictionary<string, int>(_bannedGameShuffles);

            _overlay?.ShowBannedGamesList(displayBannedGames);
            Debug.WriteLine($"Updated banned games display: {displayBannedGames.Count} processes shown with actual process names");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyBlacklistGame: Error showing ban overlay: {ex.Message}");
        }

        // Apply the ban to MainForm using the process name to find the window title
        if (_mainForm.IsShuffling)
        {
            try
            {
                // Convert process name back to window title for MainForm blacklist
                var windowTitle = _mainForm.GetWindowTitleForProcess(randomProcess);

                // We'll need to modify MainForm to have a shuffle-based blacklist system
                // For now, use a time-based fallback that estimates shuffle duration
                var estimatedShuffleTime = (_mainForm.MinSeconds + _mainForm.MaxSeconds) / 2.0;
                var timeBasedDuration = TimeSpan.FromSeconds(banShuffles * estimatedShuffleTime);

                _mainForm.BlacklistGame(windowTitle, timeBasedDuration);
                Debug.WriteLine($"Applied temporary time-based ban to MainForm for window '{windowTitle}' (process: {randomProcess}) - estimated {timeBasedDuration.TotalMinutes:F1} minutes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyBlacklistGame: Error applying ban to MainForm: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("ApplyBlacklistGame: Shuffler not active - ban applied to effect tracking only");
        }

        // IMMEDIATE SHUFFLE: If the banned process is currently active, trigger immediate shuffle
        if (_mainForm.IsShuffling && currentActiveProcess != null &&
            currentActiveProcess.Equals(randomProcess, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"Game Blacklist: Currently active process '{currentActiveProcess}' was banned - triggering immediate shuffle");

            try
            {
                // CRITICAL FIX: Directly suspend the banned game before triggering shuffle
                Debug.WriteLine($"Game Blacklist: Attempting to suspend banned game '{currentActiveProcess}' immediately");

                // Find the process to suspend it directly
                Debug.WriteLine($"Game Blacklist: Searching for process named '{currentActiveProcess}'");
                var processes = Process.GetProcessesByName(currentActiveProcess);
                Debug.WriteLine($"Game Blacklist: Found {processes.Length} processes with name '{currentActiveProcess}'");

                if (processes.Length > 0)
                {
                    var targetProcess = processes[0];
                    Debug.WriteLine($"Game Blacklist: Found process PID {targetProcess.Id} for '{currentActiveProcess}' - suspending immediately");

                    // Use MainForm's new public suspension method
                    var suspended = _mainForm.SuspendProcessByPid(targetProcess.Id, "PriorityOnly");
                    if (suspended)
                    {
                        Debug.WriteLine($"Game Blacklist: Successfully suspended PID {targetProcess.Id} using SuspendProcessByPid");
                    }
                    else
                    {
                        Debug.WriteLine($"Game Blacklist: Failed to suspend PID {targetProcess.Id}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Game Blacklist: Could not find process '{currentActiveProcess}' to suspend");
                }

                // Trigger immediate shuffle by using reflection to call the schedule method
                var scheduleMethod = _mainForm.GetType().GetMethod("ScheduleNextSwitch",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (scheduleMethod != null)
                {
                    Debug.WriteLine("Game Blacklist: Triggering immediate shuffle via ScheduleNextSwitch(true)");
                    scheduleMethod.Invoke(_mainForm, new object[] { true }); // immediate = true
                    Debug.WriteLine("Game Blacklist: Immediate shuffle triggered successfully - banned game will be properly suspended during switch");
                }
                else
                {
                    Debug.WriteLine("Game Blacklist: ScheduleNextSwitch method not found, using timer method");

                    // Fallback: Set a very short timer and restore after
                    var originalMin = _mainForm.MinSeconds;
                    var originalMax = _mainForm.MaxSeconds;

                    _mainForm.SetTimerRange(1, 1); // Set to 1 second

                    // Schedule restore of original timer after 3 seconds
                    _ = Task.Delay(3000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (_mainForm != null && !_mainForm.IsDisposed)
                            {
                                _mainForm.BeginInvoke(new Action(() =>
                                {
                                    _mainForm.SetTimerRange(originalMin, originalMax);
                                    Debug.WriteLine($"Game Blacklist: Restored original timer range to {originalMin}-{originalMax}s");
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Game Blacklist: Error restoring timer range: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Game Blacklist: Error triggering immediate shuffle: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Extracts a process name from a game window title for display purposes
    /// </summary>
    private string ExtractProcessNameFromGameTitle(string gameTitle)
    {
        // Remove common window decorations and extract meaningful game name
        var processName = gameTitle;

        // Remove common patterns
        var patterns = new[]
        {
            " - Epic Games Launcher",
            " - Steam",
            " - Origin",
            " - Battle.net",
            " - Microsoft Store",
            " - GOG Galaxy",
            " - Ubisoft Connect"
        };

        foreach (var pattern in patterns)
        {
            if (processName.Contains(pattern))
            {
                processName = processName.Replace(pattern, "").Trim();
                break;
            }
        }

        // If the title is very long, try to extract the main game name
        if (processName.Length > 30)
        {
            // Look for common separators and take the first part
            var separators = new[] { " - ", ": ", " | ", " (", " [" };
            foreach (var separator in separators)
            {
                var parts = processName.Split(new[] { separator }, StringSplitOptions.None);
                if (parts.Length > 1 && parts[0].Length >= 8) // Minimum reasonable game name length
                {
                    processName = parts[0].Trim();
                    break;
                }
            }
        }

        // Final cleanup - if still too long, truncate
        if (processName.Length > 25)
        {
            processName = processName.Substring(0, 22) + "...";
        }

        return processName;
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

        // Filter out the last used HUD file to prevent same file twice in a row
        var availableFiles = hudFiles.Where(f => f != _lastHudFile).ToArray();
        if (availableFiles.Length == 0)
        {
            // If we've filtered out all files (only 1 file exists), use the original list
            availableFiles = hudFiles;
        }

        Debug.WriteLine($"Selecting random HUD from {availableFiles.Length} available files (excluding last used):");
        for (int i = 0; i < availableFiles.Length; i++)
        {
            Debug.WriteLine($"  [{i}] {Path.GetFileName(availableFiles[i])}");
        }

        var selectedHUD = availableFiles[_random.Next(availableFiles.Length)];
        _lastHudFile = selectedHUD; // Remember this file to avoid it next time

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
        // Set the mirror mode end time
        _mirrorModeEndTime = DateTime.UtcNow.Add(duration);

        Debug.WriteLine($"Mirror Mode activated until {_mirrorModeEndTime}");

        // Thread-safe overlay call
        try
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _overlay?.ShowMirrorEffect(duration);
                    _overlay?.ShowMirrorCountdown(_mirrorModeEndTime.Value);
                });
            }
            else
            {
                _overlay?.ShowMirrorEffect(duration);
                _overlay?.ShowMirrorCountdown(_mirrorModeEndTime.Value);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyMirrorMode: Error showing overlay: {ex.Message}");
        }

        // Schedule cleanup of mirror mode tracking
        Task.Delay(duration).ContinueWith(_ =>
        {
            _mirrorModeEndTime = null;
            Debug.WriteLine("Mirror Mode ended, cleared tracking");
        });
    }

    private void ApplyGreenScreen(TimeSpan duration)
    {
        Debug.WriteLine($"ApplyGreenScreen: Starting green screen effect for {duration.TotalSeconds} seconds");

        try
        {
            // Get video files from green screen directory
            var videoFiles = GetVideoFiles(_settings.GreenScreenDirectory);

            if (videoFiles.Length == 0)
            {
                Debug.WriteLine($"No supported video files found in {_settings.GreenScreenDirectory} folder");
                return;
            }

            Debug.WriteLine($"ApplyGreenScreen: Found {videoFiles.Length} supported video files in '{_settings.GreenScreenDirectory}' folder");

            // Select a random video file (avoid repeating the last played video)
            var random = new Random();
            string selectedVideo;

            if (videoFiles.Length == 1)
            {
                // Only one video available
                selectedVideo = videoFiles[0];
                Debug.WriteLine("ApplyGreenScreen: Only one video available, using it regardless of repeat");
            }
            else
            {
                // Multiple videos available - avoid repeating the last one
                var lastPlayedPath = _overlay?.GetLastPlayedVideoPath();
                var availableVideos = videoFiles.Where(v => Path.GetFullPath(v) != lastPlayedPath).ToArray();

                if (availableVideos.Length > 0)
                {
                    selectedVideo = availableVideos[random.Next(availableVideos.Length)];
                    Debug.WriteLine($"ApplyGreenScreen: Avoided repeat, selected from {availableVideos.Length} non-repeat options");
                }
                else
                {
                    // Fallback: all videos were the last played (shouldn't happen with >1 video)
                    selectedVideo = videoFiles[random.Next(videoFiles.Length)];
                    Debug.WriteLine("ApplyGreenScreen: Fallback selection (all videos were filtered)");
                }
            }

            var fullPath = Path.GetFullPath(selectedVideo);

            Debug.WriteLine($"ApplyGreenScreen: Selected video: {fullPath}");

            // Show green screen video overlay with chroma key
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _overlay?.ShowGreenScreenVideo(fullPath, duration);
                    });
                }
                else
                {
                    _overlay?.ShowGreenScreenVideo(fullPath, duration);
                }

                Debug.WriteLine($"ApplyGreenScreen: Successfully displayed green screen video: {Path.GetFileName(fullPath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyGreenScreen: Error showing overlay: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyGreenScreen: Error: {ex.Message}");
        }
    }

    private string[] GetVideoFiles(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Debug.WriteLine($"GetVideoFiles: Directory '{directory}' does not exist");
                return Array.Empty<string>();
            }

            var supportedExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm" };
            var videoFiles = new List<string>();

            foreach (var extension in supportedExtensions)
            {
                var files = Directory.GetFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly);
                videoFiles.AddRange(files);
            }

            Debug.WriteLine($"GetVideoFiles: Found {videoFiles.Count} video files in '{directory}'");
            return videoFiles.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetVideoFiles: Error scanning directory '{directory}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Manually re-initialize the overlay if it failed during construction
    /// </summary>
    public bool TryReinitializeOverlay()
    {
        if (_overlay != null)
        {
            Debug.WriteLine("EffectManager: Overlay already initialized");
            return true;
        }

        try
        {
            _overlay = new WpfEffectOverlay(_mainForm);
            Debug.WriteLine("EffectManager: WPF overlay re-initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: Overlay re-initialization failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if overlay is available and functional
    /// </summary>
    public bool IsOverlayAvailable => _overlay != null;

    /// <summary>
    /// Comprehensive diagnostic method to check why effects might not be working
    /// </summary>
    public void DiagnoseEffectIssues()
    {
        Debug.WriteLine("=== EFFECT MANAGER DIAGNOSTIC START ===");

        // Check overlay status
        Debug.WriteLine($"1. Overlay Status: {(_overlay != null ? "Available" : "NULL - NOT AVAILABLE")}");
        if (_overlay == null)
        {
            Debug.WriteLine("   ❌ CRITICAL: Overlay is null - visual effects will not work!");
            EffectStatusChanged?.Invoke(this, "❌ CRITICAL: Overlay is null - visual effects will not work!");
        }

        // Check audio player status
        Debug.WriteLine($"2. Audio Player Status: {(_audioPlayer != null ? "Available" : "NULL - NOT AVAILABLE")}");
        if (_audioPlayer == null)
        {
            Debug.WriteLine("   ❌ CRITICAL: Audio player is null - sound effects will not work!");
            EffectStatusChanged?.Invoke(this, "❌ CRITICAL: Audio player is null - sound effects will not work!");
        }

        // Check enabled effects
        var enabledEffects = _settings.GetEnabledEffects();
        Debug.WriteLine($"3. Enabled Effects Count: {enabledEffects.Count}");
        if (enabledEffects.Count == 0)
        {
            Debug.WriteLine("   ❌ WARNING: No effects are enabled!");
            EffectStatusChanged?.Invoke(this, "❌ WARNING: No effects are enabled!");
        }
        else
        {
            Debug.WriteLine("   ✅ Enabled effects:");
            foreach (var effect in enabledEffects)
            {
                Debug.WriteLine($"     - {effect.Name} (Type: {effect.Type}, Weight: {effect.Weight})");
            }
        }

        // Check active effects
        Debug.WriteLine($"4. Active Effects Count: {_activeEffects.Count}");
        if (_activeEffects.Count > 0)
        {
            Debug.WriteLine("   Active effects:");
            foreach (var effect in _activeEffects)
            {
                var remaining = effect.EndTime - DateTime.UtcNow;
                Debug.WriteLine($"     - {effect.Config.Name} by {effect.Username} (Remaining: {remaining.TotalSeconds:F1}s)");
            }
        }

        // Check effect queue
        Debug.WriteLine($"5. Queued Effects Count: {_effectQueue.Count}");
        if (_effectQueue.Count > 0)
        {
            Debug.WriteLine($"   ⚠️ {_effectQueue.Count} effects waiting in queue");
        }

        // Check settings
        Debug.WriteLine($"6. Effect Settings:");
        Debug.WriteLine($"   - Stack Effects: {StackEffects}");
        Debug.WriteLine($"   - Queue Effects: {QueueEffects}");

        // Test basic functionality
        Debug.WriteLine("7. Testing Basic Functionality:");
        try
        {
            if (_overlay != null)
            {
                Debug.WriteLine("   ✅ Attempting overlay test...");
                _overlay.ShowEffectNotification("🔧 DIAGNOSTIC: Testing overlay functionality");
                Debug.WriteLine("   ✅ Overlay test notification sent");
                EffectStatusChanged?.Invoke(this, "✅ Overlay test successful");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"   ❌ Overlay test failed: {ex.Message}");
            EffectStatusChanged?.Invoke(this, $"❌ Overlay test failed: {ex.Message}");
        }

        Debug.WriteLine("=== EFFECT MANAGER DIAGNOSTIC END ===");
    }

    /// <summary>
    /// Force a complete system health check and recovery attempt
    /// </summary>
    public Task<bool> ForceSystemRecovery()
    {
        Debug.WriteLine("=== FORCE SYSTEM RECOVERY START ===");
        bool recoverySuccessful = false;

        try
        {
            // Step 1: Try to recover overlay
            if (_overlay == null)
            {
                Debug.WriteLine("Step 1: Attempting overlay recovery...");
                if (TryReinitializeOverlay())
                {
                    Debug.WriteLine("✅ Overlay recovered successfully");
                    EffectStatusChanged?.Invoke(this, "✅ Overlay recovered successfully");
                    recoverySuccessful = true;
                }
                else
                {
                    Debug.WriteLine("❌ Overlay recovery failed");
                    EffectStatusChanged?.Invoke(this, "❌ Overlay recovery failed");
                }
            }

            // Step 2: Reload settings
            Debug.WriteLine("Step 2: Reloading settings...");
            ReloadSettingsFromRegistry();

            // Step 3: Clear any stuck effects
            Debug.WriteLine("Step 3: Clearing potentially stuck effects...");
            var stuckEffects = _activeEffects.Where(e => e.EndTime < DateTime.UtcNow).ToList();
            foreach (var effect in stuckEffects)
            {
                Debug.WriteLine($"Removing stuck effect: {effect.Config.Name}");
                _activeEffects.Remove(effect);
            }

            // Step 4: Clear effect queue if it's backing up
            if (_effectQueue.Count > 5)
            {
                Debug.WriteLine($"Step 4: Clearing backed up effect queue ({_effectQueue.Count} effects)");
                _effectQueue.Clear();
            }

            // Step 5: Test functionality
            Debug.WriteLine("Step 5: Testing recovered functionality...");
            if (_overlay != null)
            {
                _overlay.ShowEffectNotification("🔄 RECOVERY: System recovery completed - testing effects");
                TestOverlay();
                recoverySuccessful = true;
            }

            Debug.WriteLine($"=== FORCE SYSTEM RECOVERY END - SUCCESS: {recoverySuccessful} ===");
            return Task.FromResult(recoverySuccessful);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Recovery failed with exception: {ex.Message}");
            EffectStatusChanged?.Invoke(this, $"❌ Recovery failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }
    public void TestOverlay()
    {
        if (_overlay == null)
        {
            Debug.WriteLine("EffectManager.TestOverlay: Overlay is null - cannot test");
            EffectStatusChanged?.Invoke(this, "❌ Overlay not available - visual effects disabled");
            return;
        }

        try
        {
            _overlay.ShowEffectNotification("🧪 Overlay Test - This message confirms visual effects are working!");
            Debug.WriteLine("EffectManager.TestOverlay: Test notification sent successfully");
            EffectStatusChanged?.Invoke(this, "✅ Overlay test successful - visual effects are working");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager.TestOverlay: Error during overlay test: {ex.Message}");
            EffectStatusChanged?.Invoke(this, $"❌ Overlay test failed: {ex.Message}");
        }
    }
    public void ReloadSettingsFromRegistry()
    {
        try
        {
            Debug.WriteLine("EffectManager: Force reloading settings from registry");
            _settings.LoadEffectConfigsFromRegistry();

            // Debug what we loaded
            var enabledEffects = _settings.GetEnabledEffects();
            Debug.WriteLine($"EffectManager: After reload, {enabledEffects.Count} effects are enabled:");
            foreach (var effect in enabledEffects)
            {
                Debug.WriteLine($"  - {effect.Name} (Type: {effect.Type}, Weight: {effect.Weight})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"EffectManager: Error reloading settings: {ex.Message}");
        }
    }
    public void OnGameShuffle()
    {
        Debug.WriteLine($"EffectManager.OnGameShuffle: CALLED - Processing {_bannedGameShuffles.Count} banned games");
        Debug.WriteLine($"EffectManager.OnGameShuffle: Current banned games dictionary contents: [{string.Join(", ", _bannedGameShuffles.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}]");

        if (_bannedGameShuffles.Count == 0)
        {
            Debug.WriteLine("EffectManager.OnGameShuffle: No banned games to process");
            return;
        }

        var gamesToUnban = new List<string>();

        Debug.WriteLine($"EffectManager.OnGameShuffle: Processing {_bannedGameShuffles.Count} banned games");

        foreach (var game in _bannedGameShuffles.Keys.ToList())
        {
            var beforeCount = _bannedGameShuffles[game];
            _bannedGameShuffles[game]--;
            var afterCount = _bannedGameShuffles[game];
            Debug.WriteLine($"Game shuffle: {game} ban reduced from {beforeCount} to {afterCount} shuffles remaining");

            if (_bannedGameShuffles[game] <= 0)
            {
                // Make sure to stop banning this game if the count is 0 or less
                gamesToUnban.Add(game);
            }
        }

        // Remove unbanned games
        foreach (var game in gamesToUnban)
        {
            _bannedGameShuffles.Remove(game);
            Debug.WriteLine($"Game unbanned: {game} - removed from banned list");

            // THREAD-SAFE: Use Invoke to safely update UI from background thread
            try
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.BeginInvoke(new Action(() =>
                    {
                        EffectStatusChanged?.Invoke(this, $"{game} is no longer banned");
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnGameShuffle: Error invoking EffectStatusChanged safely: {ex.Message}");
            }
        }

        // Update banned games overlay with process names for display
        try
        {
            // Use the banned process names directly since we're now storing process names
            var displayBannedGames = new Dictionary<string, int>(_bannedGameShuffles);

            Debug.WriteLine($"EffectManager.OnGameShuffle: Updating overlay with {displayBannedGames.Count} banned processes");
            if (displayBannedGames.Count > 0)
            {
                Debug.WriteLine($"EffectManager.OnGameShuffle: Banned processes to display: {string.Join(", ", displayBannedGames.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
            }

            // THREAD-SAFE: Update overlay safely
            if (_overlay != null)
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _overlay.ShowBannedGamesList(displayBannedGames);
                    }));
                }
                else
                {
                    _overlay.ShowBannedGamesList(displayBannedGames);
                }
            }

            Debug.WriteLine($"Updated banned games overlay: {displayBannedGames.Count} processes shown with actual process names");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnGameShuffle: Error updating banned games overlay: {ex.Message}");
        }

        // NO SUSPENSION NEEDED: Since we prevent the currently active game from being banned,
        // we don't need any suspension logic here. The shuffle filtering will handle exclusion.
        if (_bannedGameShuffles.Count > 0)
        {
            Debug.WriteLine($"OnGameShuffle: Current banned games (excluded from shuffle): [{string.Join(", ", _bannedGameShuffles.Keys)}]");
            Debug.WriteLine($"OnGameShuffle: No suspension needed - banned games are filtered out during shuffle selection");
        }
        Debug.WriteLine($"EffectManager.OnGameShuffle: COMPLETED - {_bannedGameShuffles.Count} banned games remaining after processing");
    }

    /// <summary>
    /// Gets the list of currently banned game process names for filtering during shuffle
    /// </summary>
    public List<string> GetBannedGameTitles()
    {
        Debug.WriteLine($"EffectManager.GetBannedGameTitles: Called - have {_bannedGameShuffles.Count} banned entries");

        if (_bannedGameShuffles.Count == 0)
        {
            Debug.WriteLine("EffectManager.GetBannedGameTitles: No banned games, returning empty list");
            return new List<string>();
        }

        Debug.WriteLine($"EffectManager.GetBannedGameTitles: Current banned processes: [{string.Join(", ", _bannedGameShuffles.Keys)}]");

        // FIXED: Return process names directly for process-based filtering
        // Since MainForm filtering now works with process names, we can return them directly
        var bannedProcessNames = _bannedGameShuffles.Keys.ToList();

        Debug.WriteLine($"EffectManager.GetBannedGameTitles: FINAL RESULT - Returning {bannedProcessNames.Count} banned process names: [{string.Join(", ", bannedProcessNames)}]");
        return bannedProcessNames;
    }

    /// <summary>
    /// Checks if a specific game title is currently banned
    /// </summary>
    public bool IsGameBanned(string gameTitle)
    {
        return _bannedGameShuffles.ContainsKey(gameTitle);
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

    /// <summary>
    /// Gets current banned games and their remaining shuffle counts
    /// </summary>
    public Dictionary<string, int> GetBannedGames() => new(_bannedGameShuffles);

    /// <summary>
    /// Manually clears all banned games - for testing/debugging only
    /// </summary>
    public void ClearBannedGames()
    {
        var clearedCount = _bannedGameShuffles.Count;
        _bannedGameShuffles.Clear();
        _overlay?.ShowBannedGamesList(new Dictionary<string, int>());
        Debug.WriteLine($"MANUALLY CLEARED {clearedCount} banned games");
        EffectStatusChanged?.Invoke(this, $"Manually cleared {clearedCount} banned games");
    }

    /// <summary>
    /// Gets detailed debug information about banned games
    /// </summary>
    public string GetBannedGamesDebugInfo()
    {
        if (_bannedGameShuffles.Count == 0)
        {
            return "No games currently banned";
        }

        var info = $"Currently banned games ({_bannedGameShuffles.Count}):\n";
        foreach (var kvp in _bannedGameShuffles)
        {
            info += $"  - {kvp.Key}: {kvp.Value} shuffles remaining\n";
        }
        return info;
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
    /// Cleans up Game Ban effect without removing the ban tracking
    /// </summary>
    private void CleanupGameBanEffect(ActiveEffect effect)
    {
        _activeEffects.Remove(effect);
        Debug.WriteLine($"EffectManager: Cleaned up Game Ban effect for {effect.Username} ({effect.Trigger}) - keeping ban tracking active");

        // Process next effect in queue
        if (_effectQueue.Count > 0)
        {
            var nextEffect = _effectQueue.Dequeue();
            _ = Task.Run(() => ApplyEffect(nextEffect, "Queued", nextEffect.Duration, "Queue"));
        }
    }

    public void ClearAllEffects()
    {
        // Clear visual effects and ALL effect tracking including banned games
        _overlay?.ClearAllEffects();
        _activeEffects.Clear();
        _effectQueue.Clear();

        // FIXED: Clear banned games tracking when Clear Effects is pressed
        // This restores banned games to rotation and clears the UI
        if (_bannedGameShuffles.Count > 0)
        {
            var clearedGames = new List<string>(_bannedGameShuffles.Keys);
            _bannedGameShuffles.Clear();
            Debug.WriteLine($"Clear Effects: Restored {clearedGames.Count} banned games to rotation: [{string.Join(", ", clearedGames)}]");
            EffectStatusChanged?.Invoke(this, $"Clear Effects: Restored {clearedGames.Count} banned games to rotation");

            // Update the overlay to show no banned games
            try
            {
                _overlay?.ShowBannedGamesList(new Dictionary<string, int>());
                Debug.WriteLine("Clear Effects: Updated overlay to show no banned games");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clear Effects: Error updating banned games overlay: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("Clear Effects: No banned games to restore");
        }

        // Clear mode tracking
        _mirrorModeEndTime = null;
        _lastHudFile = null;

        // CRITICAL FIX: Properly restore timers if chaos mode was active
        if (_chaosModeEndTime.HasValue && _mainForm != null && !_mainForm.IsDisposed)
        {
            try
            {
                if (InvokeRequired)
                {
                    _mainForm.BeginInvoke(new Action(() =>
                    {
                        _mainForm.SetTimerRange(_originalMinSeconds, _originalMaxSeconds);
                        _chaosModeEndTime = null;
                        Debug.WriteLine($"Chaos mode cleanup: Restored timer to {_originalMinSeconds}-{_originalMaxSeconds}s");
                        EffectStatusChanged?.Invoke(this, $"Chaos mode ended - timer restored");
                    }));
                }
                else
                {
                    _mainForm.SetTimerRange(_originalMinSeconds, _originalMaxSeconds);
                    _chaosModeEndTime = null;
                    Debug.WriteLine($"Chaos mode cleanup: Restored timer to {_originalMinSeconds}-{_originalMaxSeconds}s");
                    EffectStatusChanged?.Invoke(this, $"Chaos mode ended - timer restored");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring chaos mode timer: {ex.Message}");
            }
        }

        Debug.WriteLine("Cleared all effects including banned games - games restored to rotation");
        EffectStatusChanged?.Invoke(this, $"All effects cleared - banned games restored to rotation");
    }

    // Add a helper property to check if we're using the correct invocation context
    private bool InvokeRequired
    {
        get
        {
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                return _mainForm.InvokeRequired;
            }
            return false;
        }
    }

    public void Dispose()
    {
        _audioPlayer?.Dispose();
        _overlay?.Dispose();
    }

    /// <summary>
    /// Forces a specific effect for testing purposes
    /// </summary>
    public async Task ApplySpecificEffect(TwitchEffectType effectType, string username, TimeSpan duration, string trigger, SubTier? subTier = null, int bitsAmount = 0)
    {
        var config = _settings.EffectConfigs.GetValueOrDefault(effectType);
        if (config == null)
        {
            Debug.WriteLine($"EffectManager: Effect type {effectType} not found in configuration");
            EffectStatusChanged?.Invoke(this, $"Effect type {effectType} not configured");
            return;
        }

        if (!config.Enabled)
        {
            Debug.WriteLine($"EffectManager: Effect {config.Name} is disabled, enabling temporarily for test");
            // Temporarily enable for test - don't save to registry
            var tempConfig = new TwitchEffectConfig
            {
                Type = config.Type,
                Name = config.Name,
                Duration = config.Duration,
                Enabled = true, // Temporarily enabled
                BitsRequired = config.BitsRequired,
                SubsRequired = config.SubsRequired,
                Weight = config.Weight
            };
            config = tempConfig;
        }

        Debug.WriteLine($"EffectManager: Forcing specific effect {config.Name} for {username}");
        await ApplyEffect(config, username, duration, trigger, subTier, bitsAmount);
    }

    /// <summary>
    /// Forces Game Ban effect specifically - used for testing when Game Ban should always trigger
    /// </summary>
    public async Task ForceGameBanEffect(string username, TimeSpan duration, string trigger, SubTier? subTier = null, int bitsAmount = 0)
    {
        Debug.WriteLine($"EffectManager: FORCING GAME BAN EFFECT for {username}");
        await ApplySpecificEffect(TwitchEffectType.BlacklistGame, username, duration, trigger, subTier, bitsAmount);
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
    public SubTier? SubTier { get; set; } = null; // Subscription tier if triggered by sub
    public int BitsAmount { get; set; } = 0; // Bits amount if triggered by bits

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
