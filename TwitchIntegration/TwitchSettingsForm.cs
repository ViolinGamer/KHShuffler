using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

namespace BetterGameShuffler.TwitchIntegration;

public class TwitchSettingsForm : Form
{
    private readonly TwitchEffectSettings _settings;
    private readonly Dictionary<TwitchEffectType, CheckBox> _effectCheckboxes = new();
    private readonly TwitchClient _twitchClient;
    
    // Authentication Controls
    private readonly GroupBox _authGroup = new() { Text = "Twitch Authentication", Size = new Size(350, 280) }; // Increased from 260 to 280
    private readonly CheckBox _enableTwitchIntegration = new() { Text = "Enable Twitch Integration", AutoSize = true };
    private readonly TextBox _channelNameTextBox = new() { Size = new Size(200, 25), PlaceholderText = "Your Twitch channel name" };
    private readonly TextBox _clientIdTextBox = new() { Size = new Size(200, 25), PlaceholderText = "Your Twitch App Client ID" };
    private readonly TextBox _clientSecretTextBox = new() { Size = new Size(200, 25), PlaceholderText = "Click 'New Secret' in your Twitch app", UseSystemPasswordChar = true };
    private readonly Button _authenticateButton = new() { Text = "Connect to Twitch", Size = new Size(120, 30) };
    private readonly Label _authStatusLabel = new() { Text = "Not connected", AutoSize = true, ForeColor = Color.Red };
    private readonly Button _createAppButton = new() { Text = "Create Twitch App", Size = new Size(150, 30), BackColor = Color.LightGreen };
    private readonly Button _copyOAuthUrlButton = new() { Text = "Copy OAuth URL", Size = new Size(120, 25), BackColor = Color.LightYellow };
    
    // Effect Selection Controls
    private readonly GroupBox _effectsGroup = new() { Text = "Select Effects to Enable", Size = new Size(350, 250) };
    private readonly Button _selectAllButton = new() { Text = "Select All", Size = new Size(80, 25) };
    private readonly Button _selectNoneButton = new() { Text = "Select None", Size = new Size(80, 25) };
    
    // Duration Settings Controls
    private readonly GroupBox _durationGroup = new() { Text = "Effect Duration Settings", Size = new Size(350, 200) };
    private readonly NumericUpDown _tier1Duration = new() { Minimum = 1, Maximum = 300, Value = 15, Size = new Size(60, 25) };
    private readonly NumericUpDown _tier2Duration = new() { Minimum = 1, Maximum = 300, Value = 20, Size = new Size(60, 25) };
    private readonly NumericUpDown _tier3Duration = new() { Minimum = 1, Maximum = 300, Value = 25, Size = new Size(60, 25) };
    private readonly NumericUpDown _primeDuration = new() { Minimum = 1, Maximum = 300, Value = 15, Size = new Size(60, 25) };
    private readonly NumericUpDown _bitsPerSecond = new() { Minimum = 1, Maximum = 1000, Value = 25, Size = new Size(60, 25) };
    
    // Multi-Effect Settings
    private readonly GroupBox _multiEffectGroup = new() { Text = "Multi-Effect Settings", Size = new Size(350, 100) };
    private readonly NumericUpDown _effectDelay = new() { Minimum = 100, Maximum = 5000, Value = 500, Size = new Size(60, 25) };
    private readonly NumericUpDown _maxEffects = new() { Minimum = 1, Maximum = 1000, Value = 5, Size = new Size(60, 25) }; // Increased from 20 to 1000
    
    // Testing Controls
    private readonly GroupBox _testingGroup = new() { Text = "Test Twitch Events", Size = new Size(350, 180) }; // Increased from 150 to 180
    private readonly NumericUpDown _testGiftCount = new() { Minimum = 1, Maximum = 100, Value = 5, Size = new Size(60, 25) };
    private readonly NumericUpDown _testBitsAmount = new() { Minimum = 1, Maximum = 10000, Value = 100, Size = new Size(60, 25) };
    private readonly ComboBox _testSubTier = new() { Size = new Size(100, 25) };
    private readonly Button _testGiftSubButton = new() { Text = "Test Gift Subs", Size = new Size(100, 30) };
    private readonly Button _testBitsButton = new() { Text = "Test Bits", Size = new Size(100, 30) };
    private readonly Button _testSingleSubButton = new() { Text = "Test Single Sub", Size = new Size(100, 30) };
    
    // Action Buttons
    private readonly Button _saveButton = new() { Text = "Save Settings", Size = new Size(120, 35), BackColor = Color.LightGreen };
    private readonly Button _cancelButton = new() { Text = "Cancel", Size = new Size(120, 35), BackColor = Color.LightCoral };
    
    private readonly Label _statusLabel = new() { Text = "Configure your Twitch integration settings", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    
    public event EventHandler<TwitchEventArgs>? TestEventTriggered;
    
    public TwitchSettingsForm(TwitchEffectSettings settings)
    {
        _settings = settings;
        _twitchClient = new TwitchClient();
        
        InitializeForm();
        SetupControls();
        LoadSettings();
        SetupEventHandlers();
        
        // Subscribe to TwitchClient authentication events
        _twitchClient.AuthenticationChanged += OnTwitchAuthenticationChanged;
    }
    
    private void InitializeForm()
    {
        Text = "Twitch Integration Settings";
        Size = new Size(750, 780); // Increased height from 750 to 780
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };
        
        // Add title
        mainPanel.Controls.Add(_statusLabel);
        mainPanel.SetColumnSpan(_statusLabel, 2);
        
        // Left column: Authentication and Effects
        var leftPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Size = new Size(350, 630) }; // Increased from 600 to 630
        leftPanel.Controls.AddRange(new Control[] { _authGroup, _effectsGroup });
        
        // Right column: Duration settings and Testing
        var rightPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Size = new Size(350, 630) }; // Increased from 600 to 630
        rightPanel.Controls.AddRange(new Control[] { _durationGroup, _multiEffectGroup, _testingGroup });
        
        mainPanel.Controls.Add(leftPanel);
        mainPanel.Controls.Add(rightPanel);
        
        // Bottom buttons
        var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Size = new Size(700, 50) };
        buttonPanel.Controls.AddRange(new Control[] { _saveButton, _cancelButton });
        
        mainPanel.Controls.Add(buttonPanel);
        mainPanel.SetColumnSpan(buttonPanel, 2);
        
        Controls.Add(mainPanel);
    }
    
    private void SetupControls()
    {
        SetupAuthenticationGroup();
        SetupEffectsGroup();
        SetupDurationGroup();
        SetupMultiEffectGroup();
        SetupTestingGroup();
    }
    
    private void SetupAuthenticationGroup()
    {
        var authLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 }; // Increased to 10 rows
        
        authLayout.Controls.Add(_enableTwitchIntegration);
        authLayout.SetColumnSpan(_enableTwitchIntegration, 2);
        
        // Step 1: Create App
        var stepLabel = new Label { Text = "Step 1: Create Twitch App", AutoSize = true, ForeColor = Color.Blue, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        authLayout.Controls.Add(stepLabel);
        authLayout.SetColumnSpan(stepLabel, 2);
        
        authLayout.Controls.Add(_createAppButton);
        var instructionLabel = new Label { Text = "Opens dev.twitch.tv console", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) };
        authLayout.Controls.Add(instructionLabel);
        
        authLayout.Controls.Add(_copyOAuthUrlButton);
        var copyInstructionLabel = new Label { Text = "Copies redirect URL to clipboard", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) };
        authLayout.Controls.Add(copyInstructionLabel);
        
        // Step 2: Enter Credentials
        var stepLabel2 = new Label { Text = "Step 2: Enter Credentials", AutoSize = true, ForeColor = Color.Blue, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        authLayout.Controls.Add(stepLabel2);
        authLayout.SetColumnSpan(stepLabel2, 2);
        
        authLayout.Controls.Add(new Label { Text = "Channel Name:", AutoSize = true });
        authLayout.Controls.Add(_channelNameTextBox);
        
        authLayout.Controls.Add(new Label { Text = "Client ID:", AutoSize = true });
        authLayout.Controls.Add(_clientIdTextBox);
        
        authLayout.Controls.Add(new Label { Text = "Client Secret:", AutoSize = true });
        authLayout.Controls.Add(_clientSecretTextBox);
        
        // Step 3: Connect
        var stepLabel3 = new Label { Text = "Step 3: Connect", AutoSize = true, ForeColor = Color.Blue, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        authLayout.Controls.Add(stepLabel3);
        authLayout.SetColumnSpan(stepLabel3, 2);
        
        authLayout.Controls.Add(_authenticateButton);
        authLayout.Controls.Add(_authStatusLabel);
        
        _authGroup.Controls.Add(authLayout);
    }
    
    private void SetupEffectsGroup()
    {
        var effectsLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill };
        
        // Selection buttons
        var selectionPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight };
        selectionPanel.Controls.AddRange(new Control[] { _selectAllButton, _selectNoneButton });
        effectsLayout.Controls.Add(selectionPanel);
        
        // Effect checkboxes
        foreach (var effectConfig in _settings.EffectConfigs.Values.OrderBy(e => e.Name))
        {
            var checkbox = new CheckBox
            {
                Text = effectConfig.Name,
                Checked = effectConfig.Enabled,
                AutoSize = true,
                Tag = effectConfig.Type
            };
            
            _effectCheckboxes[effectConfig.Type] = checkbox;
            effectsLayout.Controls.Add(checkbox);
        }
        
        _effectsGroup.Controls.Add(effectsLayout);
    }
    
    private void SetupDurationGroup()
    {
        var durationLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        
        durationLayout.Controls.Add(new Label { Text = "Tier 1 Sub Duration (seconds):", AutoSize = true });
        durationLayout.Controls.Add(_tier1Duration);
        
        durationLayout.Controls.Add(new Label { Text = "Tier 2 Sub Duration (seconds):", AutoSize = true });
        durationLayout.Controls.Add(_tier2Duration);
        
        durationLayout.Controls.Add(new Label { Text = "Tier 3 Sub Duration (seconds):", AutoSize = true });
        durationLayout.Controls.Add(_tier3Duration);
        
        durationLayout.Controls.Add(new Label { Text = "Prime Sub Duration (seconds):", AutoSize = true });
        durationLayout.Controls.Add(_primeDuration);
        
        durationLayout.Controls.Add(new Label { Text = "Bits per second of effect:", AutoSize = true });
        durationLayout.Controls.Add(_bitsPerSecond);
        
        _durationGroup.Controls.Add(durationLayout);
    }
    
    private void SetupMultiEffectGroup()
    {
        var multiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        
        multiLayout.Controls.Add(new Label { Text = "Delay between effects (ms):", AutoSize = true });
        multiLayout.Controls.Add(_effectDelay);
        
        multiLayout.Controls.Add(new Label { Text = "Max effects per event:", AutoSize = true });
        multiLayout.Controls.Add(_maxEffects);
        
        _multiEffectGroup.Controls.Add(multiLayout);
    }
    
    private void SetupTestingGroup()
    {
        var testLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        
        // Sub tier dropdown
        _testSubTier.Items.AddRange(new[] { "Tier 1", "Tier 2", "Tier 3", "Prime" });
        _testSubTier.SelectedIndex = 0;
        
        testLayout.Controls.Add(new Label { Text = "Gift Sub Count:", AutoSize = true });
        testLayout.Controls.Add(_testGiftCount);
        
        testLayout.Controls.Add(new Label { Text = "Sub Tier:", AutoSize = true });
        testLayout.Controls.Add(_testSubTier);
        
        testLayout.Controls.Add(_testGiftSubButton);
        testLayout.Controls.Add(_testSingleSubButton);
        
        testLayout.Controls.Add(new Label { Text = "Bits Amount:", AutoSize = true });
        testLayout.Controls.Add(_testBitsAmount);
        
        testLayout.Controls.Add(_testBitsButton);
        testLayout.Controls.Add(new Label()); // Spacer
        
        _testingGroup.Controls.Add(testLayout);
    }
    
    private void SetupEventHandlers()
    {
        _enableTwitchIntegration.CheckedChanged += (_, __) => UpdateTwitchIntegrationState();
        _createAppButton.Click += (_, __) => OpenTwitchDeveloperConsole();
        _copyOAuthUrlButton.Click += (_, __) => CopyOAuthUrlToClipboard();
        _authenticateButton.Click += async (_, __) => await HandleAuthentication();
        
        // Update credentials when text changes
        _clientIdTextBox.TextChanged += (_, __) => UpdateTwitchCredentials();
        _clientSecretTextBox.TextChanged += (_, __) => UpdateTwitchCredentials();
        
        // Auto-save when settings change
        _enableTwitchIntegration.CheckedChanged += (_, __) => AutoSaveSettings();
        _channelNameTextBox.TextChanged += (_, __) => AutoSaveSettings();
        _clientIdTextBox.TextChanged += (_, __) => AutoSaveSettings();
        _clientSecretTextBox.TextChanged += (_, __) => AutoSaveSettings();
        
        _tier1Duration.ValueChanged += (_, __) => AutoSaveSettings();
        _tier2Duration.ValueChanged += (_, __) => AutoSaveSettings();
        _tier3Duration.ValueChanged += (_, __) => AutoSaveSettings();
        _primeDuration.ValueChanged += (_, __) => AutoSaveSettings();
        _bitsPerSecond.ValueChanged += (_, __) => AutoSaveSettings();
        
        _effectDelay.ValueChanged += (_, __) => AutoSaveSettings();
        _maxEffects.ValueChanged += (_, __) => AutoSaveSettings();
        
        // Auto-save when effect checkboxes change
        foreach (var checkbox in _effectCheckboxes.Values)
        {
            checkbox.CheckedChanged += (_, __) => AutoSaveSettings();
        }
        
        _selectAllButton.Click += (_, __) => SetAllEffects(true);
        _selectNoneButton.Click += (_, __) => SetAllEffects(false);
        
        _testGiftSubButton.Click += (_, __) => TestGiftSubs();
        _testSingleSubButton.Click += (_, __) => TestSingleSub();
        _testBitsButton.Click += (_, __) => TestBits();
        
        _saveButton.Click += (_, __) => SaveSettings();
        _cancelButton.Click += (_, __) => Close();
    }
    
    private void AutoSaveSettings()
    {
        try
        {
            // Save settings immediately when they change
            _settings.TwitchIntegrationEnabled = _enableTwitchIntegration.Checked;
            _settings.TwitchChannelName = _channelNameTextBox.Text.Trim();
            _settings.TwitchClientId = _clientIdTextBox.Text.Trim();
            _settings.TwitchClientSecret = _clientSecretTextBox.Text.Trim();
            
            _settings.Tier1SubDuration = (int)_tier1Duration.Value;
            _settings.Tier2SubDuration = (int)_tier2Duration.Value;
            _settings.Tier3SubDuration = (int)_tier3Duration.Value;
            _settings.PrimeSubDuration = (int)_primeDuration.Value;
            _settings.BitsPerSecond = (int)_bitsPerSecond.Value;
            
            _settings.MultiEffectDelayMs = (int)_effectDelay.Value;
            _settings.MaxSimultaneousEffects = (int)_maxEffects.Value;
            
            // Save effect enabled states
            foreach (var kvp in _effectCheckboxes)
            {
                if (_settings.EffectConfigs.ContainsKey(kvp.Key))
                {
                    _settings.EffectConfigs[kvp.Key].Enabled = kvp.Value.Checked;
                }
            }
            
            Debug.WriteLine("TwitchSettingsForm: Auto-saved settings to registry");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsForm: Auto-save error: {ex.Message}");
        }
    }
    
    private void OpenTwitchDeveloperConsole()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://dev.twitch.tv/console",
                UseShellExecute = true
            });
            
            var instructions = "?? Opened Twitch Developer Console!\n\n" +
                             "?? **Create New Application with these settings:**\n" +
                             "• **Name**: KHShuffler-YourUsername\n" +
                             "• **OAuth Redirect URLs**: http://localhost:3000/auth/callback\n" +
                             "• **Category**: Game Integration\n\n" +
                             "?? **Tip**: Use the '?? Copy OAuth URL' button below to copy the redirect URL!\n\n" +
                             "?? **After creating the app:**\n" +
                             "1. **Copy the Client ID** (long string of letters/numbers)\n" +
                             "2. **Click 'New Secret'** to generate Client Secret\n" +
                             "3. **Copy the Client Secret** immediately (you can't see it again!)\n" +
                             "4. **Paste both** into the fields below";
            
            MessageBox.Show(instructions, "Twitch App Creation Guide", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser. Please manually visit:\nhttps://dev.twitch.tv/console\n\nError: {ex.Message}",
                          "Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    
    private void CopyOAuthUrlToClipboard()
    {
        try
        {
            var oauthUrl = "http://localhost:3000/auth/callback";
            Clipboard.SetText(oauthUrl);
            
            MessageBox.Show($"? OAuth Redirect URL copied to clipboard!\n\n" +
                          $"?? Copied: {oauthUrl}\n\n" +
                          "?? Paste this into the 'OAuth Redirect URLs' field when creating your Twitch app.",
                          "URL Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"? Could not copy to clipboard: {ex.Message}\n\n" +
                          "?? Please manually copy this URL:\n" +
                          "http://localhost:3000/auth/callback",
                          "Copy Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    
    private void UpdateTwitchCredentials()
    {
        // Update the TwitchClient with new credentials
        _twitchClient.SetCredentials(_clientIdTextBox.Text, _clientSecretTextBox.Text);
        
        // Update authentication status based on credential validity
        UpdateAuthenticationStatus();
    }
    
    private void LoadSettings()
    {
        _enableTwitchIntegration.Checked = _settings.TwitchIntegrationEnabled;
        _channelNameTextBox.Text = _settings.TwitchChannelName;
        _clientIdTextBox.Text = _settings.TwitchClientId;
        _clientSecretTextBox.Text = _settings.TwitchClientSecret;
        
        _tier1Duration.Value = _settings.Tier1SubDuration;
        _tier2Duration.Value = _settings.Tier2SubDuration;
        _tier3Duration.Value = _settings.Tier3SubDuration;
        _primeDuration.Value = _settings.PrimeSubDuration;
        _bitsPerSecond.Value = _settings.BitsPerSecond;
        
        _effectDelay.Value = _settings.MultiEffectDelayMs;
        _maxEffects.Value = _settings.MaxSimultaneousEffects;
        
        // Set credentials in TwitchClient
        _twitchClient.SetCredentials(_settings.TwitchClientId, _settings.TwitchClientSecret);
        
        // Load authentication state from settings
        if (_settings.IsAuthenticated && !string.IsNullOrEmpty(_settings.TwitchAccessToken))
        {
            _twitchClient.AccessToken = _settings.TwitchAccessToken;
            _twitchClient.Username = _settings.TwitchChannelName;
            // We'll validate this token when the form loads
            _ = ValidateExistingToken();
        }
        
        UpdateAuthenticationStatus();
        UpdateTwitchIntegrationState();
    }
    
    private async System.Threading.Tasks.Task ValidateExistingToken()
    {
        try
        {
            var isValid = await _twitchClient.ValidateTokenAsync();
            if (!isValid)
            {
                // Token is invalid, clear it
                _settings.IsAuthenticated = false;
                _settings.TwitchAccessToken = "";
                UpdateAuthenticationStatus();
            }
            else
            {
                // Token is valid, get username if we don't have it
                if (string.IsNullOrEmpty(_twitchClient.Username))
                {
                    // We'd need to call GetUserInfo here if TwitchClient supported it
                    _twitchClient.Username = _settings.TwitchChannelName;
                }
                UpdateAuthenticationStatus();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsForm: Token validation error: {ex.Message}");
            _settings.IsAuthenticated = false;
            _settings.TwitchAccessToken = "";
            UpdateAuthenticationStatus();
        }
    }
    
    private void UpdateTwitchIntegrationState()
    {
        var enabled = _enableTwitchIntegration.Checked;
        
        _channelNameTextBox.Enabled = enabled;
        _clientIdTextBox.Enabled = enabled;
        _clientSecretTextBox.Enabled = enabled;
        _createAppButton.Enabled = enabled;
        _copyOAuthUrlButton.Enabled = enabled;
        _authenticateButton.Enabled = enabled;
        _testGiftSubButton.Enabled = enabled;
        _testSingleSubButton.Enabled = enabled;
        _testBitsButton.Enabled = enabled;
    }
    
    private void UpdateAuthenticationStatus()
    {
        if (_twitchClient.IsAuthenticated)
        {
            _authStatusLabel.Text = $"? Connected as {_twitchClient.Username ?? _settings.TwitchChannelName}";
            _authStatusLabel.ForeColor = Color.Green;
            _authenticateButton.Text = "Disconnect";
            _settings.IsAuthenticated = true;
        }
        else
        {
            if (!_twitchClient.IsClientConfigured())
            {
                var clientId = _twitchClient.GetClientId();
                if (string.IsNullOrEmpty(clientId))
                {
                    _authStatusLabel.Text = "?? Enter your Twitch app credentials above";
                    _authStatusLabel.ForeColor = Color.Orange;
                    _authenticateButton.Text = "Need Credentials";
                    _authenticateButton.Enabled = false;
                }
                else
                {
                    _authStatusLabel.Text = "?? Invalid credentials - check Client ID and Secret";
                    _authStatusLabel.ForeColor = Color.Orange;
                    _authenticateButton.Text = "Fix Credentials";
                    _authenticateButton.Enabled = false;
                }
            }
            else
            {
                _authStatusLabel.Text = "?? Ready to connect";
                _authStatusLabel.ForeColor = Color.Blue;
                _authenticateButton.Text = "Connect to Twitch";
                _authenticateButton.Enabled = true;
            }
            _settings.IsAuthenticated = false;
        }
    }
    
    private async System.Threading.Tasks.Task HandleAuthentication()
    {
        if (_twitchClient.IsAuthenticated)
        {
            // Disconnect
            _authenticateButton.Enabled = false;
            _authenticateButton.Text = "Disconnecting...";
            
            try
            {
                await _twitchClient.DisconnectAsync();
                _settings.TwitchAccessToken = "";
                _settings.IsAuthenticated = false;
                
                MessageBox.Show("Successfully disconnected from Twitch.", "Twitch Integration", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disconnecting: {ex.Message}", "Disconnect Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticateButton.Enabled = true;
                UpdateAuthenticationStatus();
            }
        }
        else
        {
            // Connect - credentials are already set via UpdateTwitchCredentials()
            if (string.IsNullOrWhiteSpace(_channelNameTextBox.Text))
            {
                MessageBox.Show("Please enter your Twitch channel name first.", "Channel Name Required", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (!_twitchClient.IsClientConfigured())
            {
                MessageBox.Show("Please enter your Twitch App Client ID and Client Secret first.\n\n" +
                              "If you don't have them yet, click 'Create Twitch App' to get started!", 
                              "Credentials Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            _authenticateButton.Enabled = false;
            _authenticateButton.Text = "Connecting...";
            
            try
            {
                var success = await _twitchClient.AuthenticateAsync();
                
                if (success)
                {
                    _settings.TwitchChannelName = _twitchClient.Username ?? _channelNameTextBox.Text;
                    _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
                    _settings.IsAuthenticated = true;
                    
                    MessageBox.Show($"Successfully connected to Twitch as '{_settings.TwitchChannelName}'!\n\n" +
                                  "?? Your KHShuffler is now ready for Twitch integration!", 
                        "Authentication Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                // Don't show failure message here - AuthenticateAsync already handles error display
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication error: {ex.Message}", "Authentication Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticateButton.Enabled = true;
                UpdateAuthenticationStatus();
            }
        }
    }
    
    private void OnTwitchAuthenticationChanged(object? sender, TwitchAuthEventArgs e)
    {
        // Update UI on authentication state changes
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnTwitchAuthenticationChanged(sender, e)));
            return;
        }
        
        UpdateAuthenticationStatus();
        
        if (!string.IsNullOrEmpty(e.Message))
        {
            // Could show a status message or log it
            Debug.WriteLine($"TwitchSettingsForm: Auth status changed: {e.Message}");
        }
    }
    
    private void SetAllEffects(bool enabled)
    {
        foreach (var checkbox in _effectCheckboxes.Values)
        {
            checkbox.Checked = enabled;
        }
        
        // Trigger auto-save after setting all effects
        AutoSaveSettings();
    }
    
    private SubTier GetSelectedSubTier()
    {
        return _testSubTier.SelectedIndex switch
        {
            0 => SubTier.Tier1,
            1 => SubTier.Tier2,
            2 => SubTier.Tier3,
            3 => SubTier.Prime,
            _ => SubTier.Tier1
        };
    }
    
    private void TestGiftSubs()
    {
        var eventArgs = new TwitchEventArgs
        {
            Username = "TestUser",
            Message = $"Simulated {_testGiftCount.Value} gift subs",
            GiftCount = (int)_testGiftCount.Value,
            SubTier = GetSelectedSubTier(),
            Bits = 0, // Explicitly set bits to 0 for subscription events
            Timestamp = DateTime.UtcNow
        };
        
        TestEventTriggered?.Invoke(this, eventArgs);
        
        MessageBox.Show($"Simulated {_testGiftCount.Value} x {_testSubTier.Text} gift subs!", 
            "Test Event", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void TestSingleSub()
    {
        var eventArgs = new TwitchEventArgs
        {
            Username = "TestSubscriber",
            Message = $"Simulated {_testSubTier.Text} subscription",
            GiftCount = 1,
            SubTier = GetSelectedSubTier(),
            Bits = 0, // Explicitly set bits to 0 for subscription events
            Timestamp = DateTime.UtcNow
        };
        
        TestEventTriggered?.Invoke(this, eventArgs);
        
        MessageBox.Show($"Simulated single {_testSubTier.Text} subscription!", 
            "Test Event", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void TestBits()
    {
        var eventArgs = new TwitchEventArgs
        {
            Username = "TestChatter",
            Message = $"Simulated {_testBitsAmount.Value} bits donation",
            Bits = (int)_testBitsAmount.Value,
            GiftCount = 0, // Explicitly set gift count to 0 for bits events
            Timestamp = DateTime.UtcNow
        };
        
        TestEventTriggered?.Invoke(this, eventArgs);
        
        MessageBox.Show($"Simulated {_testBitsAmount.Value} bits donation!", 
            "Test Event", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private void SaveSettings()
    {
        try
        {
            // Save basic settings
            _settings.TwitchIntegrationEnabled = _enableTwitchIntegration.Checked;
            _settings.TwitchChannelName = _channelNameTextBox.Text.Trim();
            _settings.TwitchClientId = _clientIdTextBox.Text.Trim();
            _settings.TwitchClientSecret = _clientSecretTextBox.Text.Trim();
            
            // Save duration settings
            _settings.Tier1SubDuration = (int)_tier1Duration.Value;
            _settings.Tier2SubDuration = (int)_tier2Duration.Value;
            _settings.Tier3SubDuration = (int)_tier3Duration.Value;
            _settings.PrimeSubDuration = (int)_primeDuration.Value;
            _settings.BitsPerSecond = (int)_bitsPerSecond.Value;
            
            // Save multi-effect settings
            _settings.MultiEffectDelayMs = (int)_effectDelay.Value;
            _settings.MaxSimultaneousEffects = (int)_maxEffects.Value;
            
            // Save effect enabled states
            foreach (var kvp in _effectCheckboxes)
            {
                if (_settings.EffectConfigs.ContainsKey(kvp.Key))
                {
                    _settings.EffectConfigs[kvp.Key].Enabled = kvp.Value.Checked;
                }
            }
            
            // Save authentication info
            _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
            _settings.IsAuthenticated = _twitchClient.IsAuthenticated;
            
            // Force registry save by calling the property setters again
            _settings.SaveAllSettings();
            
            MessageBox.Show("Twitch integration settings saved successfully!\n\n" +
						  "? Your credentials are stored securely\n" +
						  "? Settings will persist between sessions", 
                "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", 
                "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Unsubscribe from events
        if (_twitchClient != null)
        {
            _twitchClient.AuthenticationChanged -= OnTwitchAuthenticationChanged;
        }
        
        base.OnFormClosing(e);
    }
    
    public new void Dispose()
    {
        _twitchClient?.Dispose();
        base.Dispose();
    }
}