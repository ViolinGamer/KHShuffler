using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

namespace BetterGameShuffler.TwitchIntegration;

public class EffectTestModeForm : Form
{
    private readonly EffectManager _effectManager;
    private readonly TwitchEffectSettings _settings = new();
    
    private readonly Button _chaosButton = new() { Text = "Test Chaos Mode", Size = new Size(150, 40) };
    private readonly Button _timerDecreaseButton = new() { Text = "Test Speed Boost", Size = new Size(150, 40) };
    private readonly Button _hideHudButton = new() { Text = "Test HUD Hide", Size = new Size(150, 40) };
    private readonly Button _randomImageButton = new() { Text = "Test Random Image", Size = new Size(150, 40) };
    private readonly Button _blacklistButton = new() { Text = "Test Game Ban", Size = new Size(150, 40) };
    private readonly Button _colorFilterButton = new() { Text = "Test Color Filter", Size = new Size(150, 40) };
    private readonly Button _randomSoundButton = new() { Text = "Test Sound Effect", Size = new Size(150, 40) };
    private readonly Button _staticHudButton = new() { Text = "Test HUD Overlay", Size = new Size(150, 40) };
    private readonly Button _blurFilterButton = new() { Text = "Test Blur Filter", Size = new Size(150, 40) };
    private readonly Button _clearAllButton = new() { Text = "Clear All Effects", Size = new Size(150, 40), BackColor = Color.LightCoral };
    private readonly Button _scanFormatsButton = new() { Text = "Scan Image Formats", Size = new Size(150, 40), BackColor = Color.LightGreen };
    
    private readonly NumericUpDown _durationSeconds = new() { Minimum = 1, Maximum = 300, Value = 15, Size = new Size(80, 25) };
    private readonly CheckBox _stackEffects = new() { Text = "Stack Effects", Checked = true, AutoSize = true };
    private readonly CheckBox _queueEffects = new() { Text = "Queue Effects", AutoSize = true };
    
    // Directory Settings Controls
    private readonly TextBox _imagesDirectoryTextBox = new() { Size = new Size(200, 25), Text = "images" };
    private readonly TextBox _soundsDirectoryTextBox = new() { Size = new Size(200, 25), Text = "sounds" };
    private readonly TextBox _hudDirectoryTextBox = new() { Size = new Size(200, 25), Text = "hud" };
    private readonly Button _browseImagesButton = new() { Text = "Browse...", Size = new Size(75, 25) };
    private readonly Button _browseSoundsButton = new() { Text = "Browse...", Size = new Size(75, 25) };
    private readonly Button _browseHudButton = new() { Text = "Browse...", Size = new Size(75, 25) };
    private readonly Button _resetDirectoriesButton = new() { Text = "Reset Defaults", Size = new Size(100, 25), BackColor = Color.LightBlue };
    private readonly Button _saveSettingsButton = new() { Text = "?? Save Settings", Size = new Size(100, 25), BackColor = Color.LightGreen };
    
    private readonly Label _statusLabel = new() { Text = "Effect Test Mode - Configure directories and test effects", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly TextBox _logTextBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Size = new Size(500, 150) };
    
    public EffectTestModeForm(MainForm mainForm)
    {
        _effectManager = new EffectManager(mainForm, _settings);
        InitializeForm();
        SetupEventHandlers();
        LoadDirectorySettings();
        LogMessage("?? Test mode initialized. Ready to test effects!");
        LogMessage("?? Note: Directory settings are automatically saved to Windows Registry");
        LogMessage("?? Note: Animated WebP requires Windows 10 (1903+) or Windows 11");
    }
    
    private void InitializeForm()
    {
        Text = "Effect Test Mode & Settings";
        Size = new Size(700, 800);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 12,
            Padding = new Padding(10)
        };
        
        // Add title
        mainPanel.Controls.Add(_statusLabel);
        mainPanel.SetColumnSpan(_statusLabel, 3);
        
        // Directory Settings Section
        var directorySectionLabel = new Label { Text = "Directory Settings:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        mainPanel.Controls.Add(directorySectionLabel);
        mainPanel.SetColumnSpan(directorySectionLabel, 3);
        
        // Images Directory
        mainPanel.Controls.Add(new Label { Text = "Images Directory:", AutoSize = true });
        mainPanel.Controls.Add(_imagesDirectoryTextBox);
        mainPanel.Controls.Add(_browseImagesButton);
        
        // Sounds Directory
        mainPanel.Controls.Add(new Label { Text = "Sounds Directory:", AutoSize = true });
        mainPanel.Controls.Add(_soundsDirectoryTextBox);
        mainPanel.Controls.Add(_browseSoundsButton);
        
        // HUD Directory
        mainPanel.Controls.Add(new Label { Text = "HUD Directory:", AutoSize = true });
        mainPanel.Controls.Add(_hudDirectoryTextBox);
        mainPanel.Controls.Add(_browseHudButton);
        
        // Reset button
        mainPanel.Controls.Add(new Label()); // Spacer
        mainPanel.Controls.Add(_resetDirectoriesButton);
        mainPanel.Controls.Add(_saveSettingsButton);
        
        // Effect Settings Section
        var effectSectionLabel = new Label { Text = "Effect Settings:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        mainPanel.Controls.Add(effectSectionLabel);
        mainPanel.SetColumnSpan(effectSectionLabel, 3);
        
        // Add duration control
        var durationPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        durationPanel.Controls.AddRange(new Control[] 
        { 
            new Label { Text = "Duration (seconds):", AutoSize = true },
            _durationSeconds
        });
        
        // Add effect mode controls
        var modePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        modePanel.Controls.AddRange(new Control[] { _stackEffects, _queueEffects });
        
        mainPanel.Controls.Add(durationPanel);
        mainPanel.SetColumnSpan(durationPanel, 3);
        mainPanel.Controls.Add(modePanel);
        mainPanel.SetColumnSpan(modePanel, 3);
        
        // Test Buttons Section
        var testSectionLabel = new Label { Text = "Test Effects:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        mainPanel.Controls.Add(testSectionLabel);
        mainPanel.SetColumnSpan(testSectionLabel, 3);
        
        // Create a flow panel for buttons
        var buttonPanel = new FlowLayoutPanel 
        { 
            FlowDirection = FlowDirection.LeftToRight, 
            Size = new Size(650, 200),
            WrapContents = true
        };
        
        buttonPanel.Controls.AddRange(new Control[] 
        {
            _chaosButton, _timerDecreaseButton, _hideHudButton,
            _randomImageButton, _blacklistButton, _colorFilterButton,
            _randomSoundButton, _staticHudButton, _blurFilterButton,
            _clearAllButton, _scanFormatsButton
        });
        
        mainPanel.Controls.Add(buttonPanel);
        mainPanel.SetColumnSpan(buttonPanel, 3);
        
        // Add log area
        var logLabel = new Label { Text = "Test Log:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        mainPanel.Controls.Add(logLabel);
        mainPanel.SetColumnSpan(logLabel, 3);
        
        mainPanel.Controls.Add(_logTextBox);
        mainPanel.SetColumnSpan(_logTextBox, 3);
        
        Controls.Add(mainPanel);
    }
    
    private void SetupEventHandlers()
    {
        _chaosButton.Click += async (_, __) => await TestEffect(TwitchEffectType.ChaosMode);
        _timerDecreaseButton.Click += async (_, __) => await TestEffect(TwitchEffectType.TimerDecrease);
        _hideHudButton.Click += async (_, __) => await TestEffect(TwitchEffectType.HideHUD);
        _randomImageButton.Click += async (_, __) => await TestEffect(TwitchEffectType.RandomImage);
        _blacklistButton.Click += async (_, __) => await TestEffect(TwitchEffectType.BlacklistGame);
        _colorFilterButton.Click += async (_, __) => await TestEffect(TwitchEffectType.ColorFilter);
        _randomSoundButton.Click += async (_, __) => await TestEffect(TwitchEffectType.RandomSound);
        _staticHudButton.Click += async (_, __) => await TestEffect(TwitchEffectType.StaticHUD);
        _blurFilterButton.Click += async (_, __) => await TestEffect(TwitchEffectType.BlurFilter);
        _clearAllButton.Click += (_, __) => ClearAllEffects();
        _scanFormatsButton.Click += (_, __) => LogImageFormats();
        
        _stackEffects.CheckedChanged += (_, __) => UpdateEffectSettings();
        _queueEffects.CheckedChanged += (_, __) => UpdateEffectSettings();
        
        // Directory event handlers
        _browseImagesButton.Click += (_, __) => BrowseForDirectory(_imagesDirectoryTextBox, "Select Images Directory");
        _browseSoundsButton.Click += (_, __) => BrowseForDirectory(_soundsDirectoryTextBox, "Select Sounds Directory");
        _browseHudButton.Click += (_, __) => BrowseForDirectory(_hudDirectoryTextBox, "Select HUD Directory");
        _resetDirectoriesButton.Click += (_, __) => ResetDirectoriesToDefaults();
        _saveSettingsButton.Click += (_, __) => SaveSettingsManually();
        
        // Directory text changed handlers - Auto-save when text changes
        _imagesDirectoryTextBox.TextChanged += (_, __) => UpdateDirectorySettings();
        _soundsDirectoryTextBox.TextChanged += (_, __) => UpdateDirectorySettings();
        _hudDirectoryTextBox.TextChanged += (_, __) => UpdateDirectorySettings();
    }
    
    private void LoadDirectorySettings()
    {
        Debug.WriteLine("=== LoadDirectorySettings START ===");
        
        // Load from persistent storage and display the ACTUAL saved values
        var savedImages = _settings.ImagesDirectory;
        var savedSounds = _settings.SoundsDirectory;
        var savedHud = _settings.HudDirectory;
        
        Debug.WriteLine($"Loading saved values from Registry:");
        Debug.WriteLine($"  Images: '{savedImages}'");
        Debug.WriteLine($"  Sounds: '{savedSounds}'");
        Debug.WriteLine($"  HUD: '{savedHud}'");
        
        // Set the textboxes to show the actual saved values
        _imagesDirectoryTextBox.Text = savedImages;
        _soundsDirectoryTextBox.Text = savedSounds;
        _hudDirectoryTextBox.Text = savedHud;
        
        Debug.WriteLine($"Set textbox values:");
        Debug.WriteLine($"  Images TextBox: '{_imagesDirectoryTextBox.Text}'");
        Debug.WriteLine($"  Sounds TextBox: '{_soundsDirectoryTextBox.Text}'");
        Debug.WriteLine($"  HUD TextBox: '{_hudDirectoryTextBox.Text}'");
        
        LogMessage($"?? Loaded saved directory settings:");
        LogMessage($"  Images: '{savedImages}'");
        LogMessage($"  Sounds: '{savedSounds}'");
        LogMessage($"  HUD: '{savedHud}'");
        
        Debug.WriteLine("=== LoadDirectorySettings END ===");
    }
    
    private void UpdateDirectorySettings()
    {
        Debug.WriteLine("=== UpdateDirectorySettings START ===");
        Debug.WriteLine($"Form TextBox Values:");
        Debug.WriteLine($"  Images: '{_imagesDirectoryTextBox.Text}'");
        Debug.WriteLine($"  Sounds: '{_soundsDirectoryTextBox.Text}'");
        Debug.WriteLine($"  HUD: '{_hudDirectoryTextBox.Text}'");
        
        // Save to persistent storage automatically
        Debug.WriteLine("Setting _settings properties...");
        _settings.ImagesDirectory = _imagesDirectoryTextBox.Text;
        _settings.SoundsDirectory = _soundsDirectoryTextBox.Text;
        _settings.HudDirectory = _hudDirectoryTextBox.Text;
        
        Debug.WriteLine("Reading back _settings properties...");
        Debug.WriteLine($"  Images: '{_settings.ImagesDirectory}'");
        Debug.WriteLine($"  Sounds: '{_settings.SoundsDirectory}'");
        Debug.WriteLine($"  HUD: '{_settings.HudDirectory}'");
        
        // Ensure directories exist
        _settings.EnsureDirectoriesExist();
        
        LogMessage($"?? Directory settings saved: Images='{_settings.ImagesDirectory}', Sounds='{_settings.SoundsDirectory}', HUD='{_settings.HudDirectory}'");
        Debug.WriteLine("=== UpdateDirectorySettings END ===");
    }
    
    private void BrowseForDirectory(TextBox textBox, string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            SelectedPath = Directory.Exists(textBox.Text) ? Path.GetFullPath(textBox.Text) : Environment.CurrentDirectory,
            ShowNewFolderButton = true
        };
        
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
            LogMessage($"Selected directory: {dialog.SelectedPath}");
        }
    }
    
    private void ResetDirectoriesToDefaults()
    {
        _imagesDirectoryTextBox.Text = "images";
        _soundsDirectoryTextBox.Text = "sounds";
        _hudDirectoryTextBox.Text = "hud";
        LogMessage("?? Directory settings reset to defaults and saved");
    }
    
    private void SaveSettingsManually()
    {
        try
        {
            Debug.WriteLine("=== MANUAL SAVE SETTINGS START ===");
            
            // Force update settings (in case auto-save didn't trigger)
            UpdateDirectorySettings();
            
            // Verify what's actually in the Registry
            Debug.WriteLine("=== REGISTRY VERIFICATION ===");
            try
            {
                var regImages = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\BetterGameShuffler", "TwitchEffects_ImagesDirectory", "NOT_FOUND");
                var regSounds = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\BetterGameShuffler", "TwitchEffects_SoundsDirectory", "NOT_FOUND");
                var regHud = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\BetterGameShuffler", "TwitchEffects_HudDirectory", "NOT_FOUND");
                
                Debug.WriteLine($"Registry - Images: {regImages}");
                Debug.WriteLine($"Registry - Sounds: {regSounds}");
                Debug.WriteLine($"Registry - HUD: {regHud}");
            }
            catch (Exception regEx)
            {
                Debug.WriteLine($"Registry verification error: {regEx.Message}");
            }
            
            LogMessage("? Settings manually saved to Windows Registry");
            LogMessage($"?? Registry location: HKEY_CURRENT_USER\\Software\\BetterGameShuffler");
            LogMessage($"?? Check Debug Output for detailed Registry verification");
            Debug.WriteLine("=== MANUAL SAVE SETTINGS END ===");
        }
        catch (Exception ex)
        {
            LogMessage($"? Error saving settings: {ex.Message}");
            Debug.WriteLine($"Manual save error: {ex}");
        }
    }
    
    private async System.Threading.Tasks.Task TestEffect(TwitchEffectType effectType)
    {
        var config = _settings.EffectConfigs[effectType];
        var duration = TimeSpan.FromSeconds((double)_durationSeconds.Value);
        
        LogMessage($"Testing {config.Name} for {duration.TotalSeconds}s...");
        
        try
        {
            await _effectManager.ApplyEffect(config, "TestUser", duration);
            LogMessage($"? {config.Name} applied successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"? Error applying {config.Name}: {ex.Message}");
        }
    }
    
    private void ClearAllEffects()
    {
        try
        {
            _effectManager.ClearAllEffects();
            LogMessage("? All effects cleared successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"? Error clearing effects: {ex.Message}");
        }
    }
    
    private void UpdateEffectSettings()
    {
        _effectManager.StackEffects = _stackEffects.Checked;
        _effectManager.QueueEffects = _queueEffects.Checked;
        
        // Ensure only one mode is active
        if (_stackEffects.Checked && _queueEffects.Checked)
        {
            // Determine which checkbox was just changed by checking which one triggered this event
            var stackEffectsChanged = _stackEffects.Checked != _effectManager.StackEffects;
            if (stackEffectsChanged)
                _queueEffects.Checked = false;
            else
                _stackEffects.Checked = false;
        }
        
        LogMessage($"Effect mode updated: Stack={_effectManager.StackEffects}, Queue={_effectManager.QueueEffects}");
    }
    
    private void LogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        _logTextBox.AppendText(logEntry + Environment.NewLine);
        _logTextBox.SelectionStart = _logTextBox.Text.Length;
        _logTextBox.ScrollToCaret();
    }
    
    private void LogImageFormats()
    {
        try
        {
            LogMessage("=== Scanning for supported image formats ===");
            
            var directories = new[] { _settings.ImagesDirectory, _settings.HudDirectory };
            var totalFiles = 0;
            var formatCounts = new Dictionary<string, int>();
            
            foreach (var dir in directories)
            {
                LogMessage($"Checking directory: {Path.GetFullPath(dir)}");
                
                if (!Directory.Exists(dir)) 
                {
                    LogMessage($"  Directory does not exist, creating it...");
                    Directory.CreateDirectory(dir);
                    continue;
                }
                
                var files = ImageLoader.GetSupportedImageFiles(dir);
                LogMessage($"{dir}/ folder: {files.Length} files found");
                
                foreach (var file in files)
                {
                    var formatInfo = ImageLoader.GetImageFormatInfo(file);
                    var fileName = Path.GetFileName(file);
                    LogMessage($"  - {fileName}: {formatInfo}");
                    
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    formatCounts[extension] = formatCounts.GetValueOrDefault(extension, 0) + 1;
                    totalFiles++;
                }
            }
            
            LogMessage($"=== Summary: {totalFiles} total image files ===");
            foreach (var kvp in formatCounts.OrderBy(k => k.Key))
            {
                LogMessage($"  {kvp.Key}: {kvp.Value} files");
            }
            
            if (totalFiles == 0)
            {
                LogMessage("No image files found. Add images to your configured directories to test!");
                LogMessage($"Current directories: Images='{Path.GetFullPath(_settings.ImagesDirectory)}', HUD='{Path.GetFullPath(_settings.HudDirectory)}'");
                LogMessage("");
                LogMessage("?? WEBP TROUBLESHOOTING TIPS:");
                LogMessage("• WebP files require Windows 10 (1903+) or Windows 11");
                LogMessage("• Large WebP files may cause OutOfMemoryException");
                LogMessage("• Try adding some PNG or JPG files as fallbacks");
                LogMessage("• Check if WebP codec is installed from Microsoft Store");
                LogMessage("");
                LogMessage("?? CREATING TEST FALLBACK IMAGES...");
                CreateTestFallbackImages();
            }
            else
            {
                // Count WebP vs other formats
                var webpCount = formatCounts.GetValueOrDefault(".webp", 0);
                var otherCount = totalFiles - webpCount;
                
                if (webpCount > 0 && otherCount == 0)
                {
                    LogMessage("");
                    LogMessage("??  WARNING: Only WebP files detected!");
                    LogMessage("• Consider adding PNG/JPG files as fallbacks");
                    LogMessage("• WebP support varies by Windows version");
                    LogMessage("");
                    LogMessage("?? CREATING TEST FALLBACK IMAGES...");
                    CreateTestFallbackImages();
                }
                else if (webpCount > 0)
                {
                    LogMessage("");
                    LogMessage($"? Good mix: {webpCount} WebP files + {otherCount} other formats");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error scanning image formats: {ex.Message}");
        }
    }
    
    private void CreateTestFallbackImages()
    {
        try
        {
            var imagesDir = Path.GetFullPath(_settings.ImagesDirectory);
            
            // Create a simple test PNG file
            var testImagePath = Path.Combine(imagesDir, "test_fallback.png");
            if (!File.Exists(testImagePath))
            {
                using var bitmap = new Bitmap(200, 200);
                using var graphics = Graphics.FromImage(bitmap);
                
                // Create a colorful test image
                graphics.Clear(Color.FromArgb(255, 100, 150, 255));
                using var brush = new SolidBrush(Color.White);
                using var font = new Font("Arial", 16, FontStyle.Bold);
                graphics.DrawString("TEST\nIMAGE", font, brush, new PointF(60, 80));
                
                bitmap.Save(testImagePath, System.Drawing.Imaging.ImageFormat.Png);
                LogMessage($"? Created test fallback image: {Path.GetFileName(testImagePath)}");
            }
            
            // Create another test image with different color
            var testImagePath2 = Path.Combine(imagesDir, "test_fallback2.png");
            if (!File.Exists(testImagePath2))
            {
                using var bitmap = new Bitmap(200, 200);
                using var graphics = Graphics.FromImage(bitmap);
                
                // Create a different colorful test image
                graphics.Clear(Color.FromArgb(255, 255, 100, 100));
                using var brush = new SolidBrush(Color.White);
                using var font = new Font("Arial", 16, FontStyle.Bold);
                graphics.DrawString("TEST\nIMAGE 2", font, brush, new PointF(45, 80));
                
                bitmap.Save(testImagePath2, System.Drawing.Imaging.ImageFormat.Png);
                LogMessage($"? Created test fallback image: {Path.GetFileName(testImagePath2)}");
            }
            
            LogMessage("? Fallback images created successfully!");
            LogMessage("• These PNG files should work while WebP issues are resolved");
            LogMessage("• Try the Random Image effect again");
        }
        catch (Exception ex)
        {
            LogMessage($"? Error creating fallback images: {ex.Message}");
        }
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Ensure settings are saved on close
        try
        {
            UpdateDirectorySettings();
            LogMessage("?? Final settings save completed");
        }
        catch (Exception ex)
        {
            LogMessage($"?? Warning: Could not save settings on close: {ex.Message}");
        }
        
        LogMessage("?? Test mode closing...");
        base.OnFormClosing(e);
    }
}