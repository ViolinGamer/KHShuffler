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

    // Game Ban Duration Settings Controls
    private readonly GroupBox _gameBanGroup = new() { Text = "Game Ban Duration Settings", Size = new Size(350, 180) };
    private readonly NumericUpDown _banTier1Shuffles = new() { Minimum = 1, Maximum = 50, Value = 3, Size = new Size(60, 25) };
    private readonly NumericUpDown _banTier2Shuffles = new() { Minimum = 1, Maximum = 50, Value = 4, Size = new Size(60, 25) };
    private readonly NumericUpDown _banTier3Shuffles = new() { Minimum = 1, Maximum = 50, Value = 5, Size = new Size(60, 25) };
    private readonly NumericUpDown _banPrimeShuffles = new() { Minimum = 1, Maximum = 50, Value = 3, Size = new Size(60, 25) };
    private readonly NumericUpDown _banBitsPer100 = new() { Minimum = 1, Maximum = 20, Value = 2, Size = new Size(60, 25) };

    // Multi-Effect Settings
    private readonly GroupBox _multiEffectGroup = new() { Text = "Multi-Effect Settings", Size = new Size(350, 100) };
    private readonly NumericUpDown _effectDelay = new() { Minimum = 100, Maximum = 5000, Value = 500, Size = new Size(60, 25) };
    private readonly NumericUpDown _maxEffects = new() { Minimum = 1, Maximum = 1000, Value = 5, Size = new Size(60, 25) }; // Increased from 20 to 1000

    // Testing Controls
    private readonly GroupBox _testingGroup = new() { Text = "Test Twitch Events", Size = new Size(350, 210) }; // Increased from 180 to 210
    private readonly NumericUpDown _testGiftCount = new() { Minimum = 1, Maximum = 100, Value = 5, Size = new Size(60, 25) };
    private readonly NumericUpDown _testBitsAmount = new() { Minimum = 1, Maximum = 10000, Value = 100, Size = new Size(60, 25) };
    private readonly ComboBox _testSubTier = new() { Size = new Size(100, 25) };
    private readonly Button _testGiftSubButton = new() { Text = "Test Gift Subs", Size = new Size(100, 30) };
    private readonly Button _testBitsButton = new() { Text = "Test Bits", Size = new Size(100, 30) };
    private readonly Button _testSingleSubButton = new() { Text = "Test Single Sub", Size = new Size(100, 30) };
    private readonly Button _testGameBanButton = new() { Text = "Test Game Ban", Size = new Size(100, 30), BackColor = Color.LightCoral };

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

        // CRITICAL FIX: Start proactive token refresh to prevent daily expiration
        StartProactiveTokenRefresh();
    }

    private void InitializeForm()
    {
        Text = "Twitch Integration Settings";
        Size = new Size(750, 860); // Increased height from 780 to 860 for game ban settings
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
        var leftPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Size = new Size(350, 710) }; // Increased from 630 to 710
        leftPanel.Controls.AddRange(new Control[] { _authGroup, _effectsGroup });

        // Right column: Duration settings and Testing
        var rightPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Size = new Size(350, 710) }; // Increased from 630 to 710
        rightPanel.Controls.AddRange(new Control[] { _durationGroup, _gameBanGroup, _multiEffectGroup, _testingGroup });

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
        SetupGameBanGroup();
        SetupMultiEffectGroup();
        SetupTestingGroup();
    }

    private void SetupAuthenticationGroup()
    {
        var authLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 }; // Increasing to 10 rows

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

    private void SetupGameBanGroup()
    {
        var gameBanLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };

        gameBanLayout.Controls.Add(new Label { Text = "Tier 1 Sub Ban (shuffles):", AutoSize = true });
        gameBanLayout.Controls.Add(_banTier1Shuffles);

        gameBanLayout.Controls.Add(new Label { Text = "Tier 2 Sub Ban (shuffles):", AutoSize = true });
        gameBanLayout.Controls.Add(_banTier2Shuffles);

        gameBanLayout.Controls.Add(new Label { Text = "Tier 3 Sub Ban (shuffles):", AutoSize = true });
        gameBanLayout.Controls.Add(_banTier3Shuffles);

        gameBanLayout.Controls.Add(new Label { Text = "Prime Sub Ban (shuffles):", AutoSize = true });
        gameBanLayout.Controls.Add(_banPrimeShuffles);

        gameBanLayout.Controls.Add(new Label { Text = "Bits per 100 ban (shuffles):", AutoSize = true });
        gameBanLayout.Controls.Add(_banBitsPer100);

        var infoLabel = new Label
        {
            Text = "Game Ban prevents a game from appearing\nduring shuffling for the specified number\nof game switches.",
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8)
        };
        gameBanLayout.Controls.Add(infoLabel);
        gameBanLayout.SetColumnSpan(infoLabel, 2);

        _gameBanGroup.Controls.Add(gameBanLayout);
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
        var testLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7 }; // Increased from 6 to 7

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
        testLayout.Controls.Add(_testGameBanButton); // Add the Game Ban test button

        testLayout.Controls.Add(new Label()); // Spacer
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

        _banTier1Shuffles.ValueChanged += (_, __) => AutoSaveSettings();
        _banTier2Shuffles.ValueChanged += (_, __) => AutoSaveSettings();
        _banTier3Shuffles.ValueChanged += (_, __) => AutoSaveSettings();
        _banPrimeShuffles.ValueChanged += (_, __) => AutoSaveSettings();
        _banBitsPer100.ValueChanged += (_, __) => AutoSaveSettings();

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
        _testGameBanButton.Click += (_, __) => TestGameBan();

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

            _settings.BanGameShufflesTier1 = (int)_banTier1Shuffles.Value;
            _settings.BanGameShufflesTier2 = (int)_banTier2Shuffles.Value;
            _settings.BanGameShufflesTier3 = (int)_banTier3Shuffles.Value;
            _settings.BanGameShufflesPrime = (int)_banPrimeShuffles.Value;
            _settings.BanGameShufflesPer100Bits = (int)_banBitsPer100.Value;

            _settings.MultiEffectDelayMs = (int)_effectDelay.Value;
            _settings.MaxSimultaneousEffects = (int)_maxEffects.Value;

            // FIXED: Save effect enabled states by triggering the Enabled setter properly
            foreach (var kvp in _effectCheckboxes)
            {
                if (_settings.EffectConfigs.ContainsKey(kvp.Key))
                {
                    // Explicitly trigger the setter to ensure Registry save
                    _settings.EffectConfigs[kvp.Key].Enabled = kvp.Value.Checked;
                    Debug.WriteLine($"AutoSave: Set {kvp.Key}.Enabled = {kvp.Value.Checked}");
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

            var instructions = "Opened Twitch Developer Console!\n\n" +
                             "**Create New Application with these settings:**\n" +
                             "**Name**: KHShuffler-YourUsername\n" +
                             "**OAuth Redirect URLs**: http://localhost:3000/auth/callback\n" +
                             "**Category**: Game Integration\n\n" +
                             "**Tip**: Use the 'Copy OAuth URL' button below to copy the redirect URL!\n\n" +
                             "**After creating the app:**\n" +
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

            MessageBox.Show($"OAuth Redirect URL copied to clipboard!\n\n" +
                          $"Copied: {oauthUrl}\n\n" +
                          "Paste this into the 'OAuth Redirect URLs' field when creating your Twitch app.",
                          "URL Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not copy to clipboard: {ex.Message}\n\n" +
                          "Please manually copy this URL:\n" +
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

        _banTier1Shuffles.Value = _settings.BanGameShufflesTier1;
        _banTier2Shuffles.Value = _settings.BanGameShufflesTier2;
        _banTier3Shuffles.Value = _settings.BanGameShufflesTier3;
        _banPrimeShuffles.Value = _settings.BanGameShufflesPrime;
        _banBitsPer100.Value = _settings.BanGameShufflesPer100Bits;

        _effectDelay.Value = _settings.MultiEffectDelayMs;
        _maxEffects.Value = _settings.MaxSimultaneousEffects;

        // FIXED: Load effect enabled states properly from the saved registry values
        foreach (var kvp in _effectCheckboxes)
        {
            if (_settings.EffectConfigs.ContainsKey(kvp.Key))
            {
                var savedEnabled = _settings.EffectConfigs[kvp.Key].Enabled;
                kvp.Value.Checked = savedEnabled;
                Debug.WriteLine($"LoadSettings: Loaded {kvp.Key}.Enabled = {savedEnabled} -> Checkbox.Checked = {kvp.Value.Checked}");
            }
        }

        // Set credentials in TwitchClient
        _twitchClient.SetCredentials(_settings.TwitchClientId, _settings.TwitchClientSecret);

        // Load authentication state from settings
        Debug.WriteLine("TwitchSettingsForm: [PERSISTENCE-DEBUG] Loading authentication from registry...");
        Debug.WriteLine($"TwitchSettingsForm: [PERSISTENCE-DEBUG] IsAuthenticated: {_settings.IsAuthenticated}");
        Debug.WriteLine($"TwitchSettingsForm: [PERSISTENCE-DEBUG] AccessToken exists: {!string.IsNullOrEmpty(_settings.TwitchAccessToken)}");
        Debug.WriteLine($"TwitchSettingsForm: [PERSISTENCE-DEBUG] RefreshToken exists: {!string.IsNullOrEmpty(_settings.TwitchRefreshToken)}");
        Debug.WriteLine($"TwitchSettingsForm: [PERSISTENCE-DEBUG] Channel name: '{_settings.TwitchChannelName}'");

        if (_settings.IsAuthenticated && !string.IsNullOrEmpty(_settings.TwitchAccessToken))
        {
            Debug.WriteLine("TwitchSettingsForm: [PERSISTENCE-DEBUG] Loading tokens into TwitchClient...");
            _twitchClient.AccessToken = _settings.TwitchAccessToken;
            _twitchClient.RefreshToken = _settings.TwitchRefreshToken;
            _twitchClient.Username = _settings.TwitchChannelName;

            Debug.WriteLine($"TwitchSettingsForm: [PERSISTENCE-DEBUG] TwitchClient.IsAuthenticated: {_twitchClient.IsAuthenticated}");

            // We'll validate this token when the form loads
            _ = ValidateExistingToken();
        }
        else
        {
            Debug.WriteLine("TwitchSettingsForm: [PERSISTENCE-DEBUG] No authentication data found or IsAuthenticated is false");
        }

        UpdateAuthenticationStatus();
        UpdateTwitchIntegrationState();
    }

    private async System.Threading.Tasks.Task ValidateExistingToken()
    {
        try
        {
            Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] STARTING AGGRESSIVE TOKEN VALIDATION");
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Current time: {DateTime.Now}");
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Access token length: {_twitchClient.AccessToken?.Length ?? 0}");
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Refresh token length: {_twitchClient.RefreshToken?.Length ?? 0}");
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Settings IsAuthenticated: {_settings.IsAuthenticated}");

            // ENHANCED STARTUP STRATEGY: Always try to refresh tokens on startup
            // This handles cases where:
            // 1. Program was closed overnight and tokens expired
            // 2. Tokens are valid but close to expiration 
            // 3. Previous refresh attempts failed

            bool shouldRefresh = false;
            string refreshReason = "";

            if (string.IsNullOrEmpty(_twitchClient.AccessToken))
            {
                Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] No access token - skipping validation");
                return;
            }

            if (string.IsNullOrEmpty(_twitchClient.RefreshToken))
            {
                Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] No refresh token - cannot refresh, validating existing token");
                var isValid = await _twitchClient.ValidateTokenAsync();
                Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Token validation result: {isValid}");

                if (!isValid)
                {
                    Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Token invalid and no refresh token available");
                    // Clear invalid authentication
                    _settings.TwitchAccessToken = "";
                    _settings.IsAuthenticated = false;
                    UpdateAuthenticationStatus();
                }
                return;
            }

            // Strategy: Always attempt refresh on startup for maximum reliability
            // This ensures we start with the freshest possible tokens
            shouldRefresh = true;
            refreshReason = "Proactive startup refresh for maximum reliability";

            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Refresh decision: {shouldRefresh}");
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Reason: {refreshReason}");

            if (shouldRefresh)
            {
                Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Token invalid, attempting refresh...");

                // Try to refresh the token if we have a refresh token
                if (!string.IsNullOrEmpty(_twitchClient.RefreshToken))
                {
                    Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Refresh token available, length: {_twitchClient.RefreshToken.Length}");
                    Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Attempting token refresh...");

                    var refreshSuccess = await _twitchClient.RefreshTokenAsync();
                    Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Token refresh result: {refreshSuccess}");

                    if (refreshSuccess)
                    {
                        Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Token refresh successful!");
                        Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] New access token length: {_twitchClient.AccessToken?.Length ?? 0}");
                        Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] New refresh token length: {_twitchClient.RefreshToken?.Length ?? 0}");

                        // Save the new tokens
                        _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
                        _settings.TwitchRefreshToken = _twitchClient.RefreshToken ?? "";
                        _settings.IsAuthenticated = true;

                        Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Tokens saved to registry successfully");

                        // Show success message to user
                        MessageBox.Show("✅ Twitch authentication refreshed successfully!\n\n" +
                                      "Your tokens have been automatically renewed and saved.\n" +
                                      "You can now connect to live events without restarting the application.",
                                      "Authentication Refreshed",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);

                        UpdateAuthenticationStatus();
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Token refresh failed");

                        // Enhanced error message with troubleshooting steps
                        var result = MessageBox.Show("Twitch authentication has expired and could not be automatically renewed.\n\n" +
                                      "Common causes:\n" +
                                      "• Tokens expired (usually after 4 hours of inactivity)\n" +
                                      "• Network connectivity issues\n" +
                                      "• Twitch API changes or maintenance\n" +
                                      "• Refresh token invalidated by Twitch\n\n" +
                                      "Click 'Yes' to clear old authentication and reconnect, or 'No' to continue without Twitch features.",
                                      "Twitch Re-authentication Required",
                                      MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                        if (result == DialogResult.Yes)
                        {
                            Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] User chose to clear authentication");
                            _settings.TwitchAccessToken = "";
                            _settings.TwitchRefreshToken = "";
                            _settings.IsAuthenticated = false;
                            _twitchClient.AccessToken = null;
                            _twitchClient.RefreshToken = null;
                            UpdateAuthenticationStatus();
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] No refresh token available");

                    // Show message that user needs to reconnect
                    MessageBox.Show("Twitch authentication has expired.\n\n" +
                                  "Please reconnect to Twitch to continue using subscription and bits effects.",
                                  "Twitch Re-authentication Required",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Token is invalid and refresh failed, clear it
                _settings.IsAuthenticated = false;
                _settings.TwitchAccessToken = "";
                _settings.TwitchRefreshToken = "";
                UpdateAuthenticationStatus();
            }
            else
            {
                Debug.WriteLine("TwitchSettingsForm: [TOKEN-STARTUP] Token validation successful");

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
            Debug.WriteLine($"TwitchSettingsForm: [TOKEN-STARTUP] Token validation error: {ex.Message}");
            _settings.IsAuthenticated = false;
            _settings.TwitchAccessToken = "";
            _settings.TwitchRefreshToken = "";
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
        _testGameBanButton.Enabled = enabled;
    }

    private void UpdateAuthenticationStatus()
    {
        if (_twitchClient.IsAuthenticated)
        {
            _authStatusLabel.Text = $"Connected as {_twitchClient.Username ?? _settings.TwitchChannelName}";
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
                    _authStatusLabel.Text = "Enter your Twitch app credentials above";
                    _authStatusLabel.ForeColor = Color.Orange;
                    _authenticateButton.Text = "Need Credentials";
                    _authenticateButton.Enabled = false;
                }
                else
                {
                    _authStatusLabel.Text = "Invalid credentials - check Client ID and Secret";
                    _authStatusLabel.ForeColor = Color.Orange;
                    _authenticateButton.Text = "Fix Credentials";
                    _authenticateButton.Enabled = false;
                }
            }
            else
            {
                _authStatusLabel.Text = "Ready to connect";
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
                                  "Your KHShuffler is now ready for Twitch integration!",
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

        Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Authentication event: {e.Message}");
        Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Is authenticated: {e.IsAuthenticated}");
        Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Username: {e.Username}");

        // CRITICAL FIX: Save tokens when authentication changes (especially after token refresh)
        if (e.IsAuthenticated && _twitchClient != null)
        {
            Debug.WriteLine("TwitchSettingsForm: [AUTH-CHANGED] Saving updated authentication tokens...");

            // Save the current tokens from TwitchClient to settings registry
            var oldAccessToken = _settings.TwitchAccessToken;
            var oldRefreshToken = _settings.TwitchRefreshToken;

            _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
            _settings.TwitchRefreshToken = _twitchClient.RefreshToken ?? "";
            _settings.IsAuthenticated = true;
            _settings.TwitchChannelName = e.Username;

            // Log token update details
            Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Access token updated: {oldAccessToken != _settings.TwitchAccessToken}");
            Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Refresh token updated: {oldRefreshToken != _settings.TwitchRefreshToken}");
            Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] New access token length: {_settings.TwitchAccessToken?.Length ?? 0}");
            Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] New refresh token length: {_settings.TwitchRefreshToken?.Length ?? 0}");

            // CRITICAL: Try to reconnect EventSub if this was a token refresh
            if (e.Message?.Contains("refreshed") == true || e.Message?.Contains("Token refreshed") == true)
            {
                Debug.WriteLine("TwitchSettingsForm: [AUTH-CHANGED] Token was refreshed - attempting EventSub reconnection...");

                // Notify the main form or effect test mode about token refresh
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000); // Brief delay to let settings save
                        Debug.WriteLine("TwitchSettingsForm: [AUTH-CHANGED] Token refresh complete - EventSub should reconnect automatically");

                        // The EffectTestModeForm should handle reconnection when it detects token changes
                        // We'll add a mechanism to notify active EventSub connections
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Error in post-refresh tasks: {ex.Message}");
                    }
                });
            }
        }
        else if (!e.IsAuthenticated)
        {
            Debug.WriteLine("TwitchSettingsForm: [AUTH-CHANGED] Authentication lost, clearing tokens...");
            _settings.TwitchAccessToken = "";
            _settings.TwitchRefreshToken = "";
            _settings.IsAuthenticated = false;
        }

        UpdateAuthenticationStatus();

        if (!string.IsNullOrEmpty(e.Message))
        {
            Debug.WriteLine($"TwitchSettingsForm: [AUTH-CHANGED] Status message: {e.Message}");
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

    private void TestGameBan()
    {
        // Create a custom event that forces a Game Ban effect
        var gamebanEventArgs = new TwitchEventArgs
        {
            Username = "TestBanner",
            Message = "Forced Game Ban test",
            GiftCount = 1,
            SubTier = GetSelectedSubTier(),
            Bits = 0,
            Timestamp = DateTime.UtcNow
        };

        // Trigger the event but also show a specific message
        TestEventTriggered?.Invoke(this, gamebanEventArgs);

        var banShuffles = _settings.GetBanShufflesForSubTier(GetSelectedSubTier());
        MessageBox.Show($"Game Ban Test Triggered!\n\n" +
                       $"Effect: GAME BAN\n" +
                       $"Trigger: {GetSelectedSubTier()} Subscription\n" +
                       $"Ban Duration: {banShuffles} shuffles\n" +
                       $"User: TestBanner\n\n" +
                       $"Check the overlay for:\n" +
                       $"Game ban notification (center screen)\n" +
                       $"Banned games countdown (bottom right)\n\n" +
                       $"Note: In test mode, fake game names are used for demonstration.",
            "Game Ban Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            // Save game ban settings
            _settings.BanGameShufflesTier1 = (int)_banTier1Shuffles.Value;
            _settings.BanGameShufflesTier2 = (int)_banTier2Shuffles.Value;
            _settings.BanGameShufflesTier3 = (int)_banTier3Shuffles.Value;
            _settings.BanGameShufflesPrime = (int)_banPrimeShuffles.Value;
            _settings.BanGameShufflesPer100Bits = (int)_banBitsPer100.Value;

            // Save multi-effect settings
            _settings.MultiEffectDelayMs = (int)_effectDelay.Value;
            _settings.MaxSimultaneousEffects = (int)_maxEffects.Value;

            // FIXED: Save effect enabled states by properly triggering the Enabled setter
            foreach (var kvp in _effectCheckboxes)
            {
                if (_settings.EffectConfigs.ContainsKey(kvp.Key))
                {
                    // Explicitly trigger the setter to ensure Registry save
                    var newValue = kvp.Value.Checked;
                    _settings.EffectConfigs[kvp.Key].Enabled = newValue;
                    Debug.WriteLine($"SaveSettings: Set {kvp.Key}.Enabled = {newValue}");
                }
            }

            // Save authentication info
            _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
            _settings.TwitchRefreshToken = _twitchClient.RefreshToken ?? "";
            _settings.IsAuthenticated = _twitchClient.IsAuthenticated;

            // Force registry save by calling the property setters again
            _settings.SaveAllSettings();

            Debug.WriteLine("TwitchSettingsForm: Manual save completed with effect enabled states");

            MessageBox.Show("Twitch integration settings saved successfully!\n\n" +
                          "Your credentials are stored securely\n" +
                          "Settings will persist between sessions\n" +
                          "Effect selections are saved",
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

    /// <summary>
    /// Starts a background timer to proactively refresh tokens before they expire
    /// This prevents the daily authentication issues by refreshing tokens every 3 hours
    /// </summary>
    private void StartProactiveTokenRefresh()
    {
        try
        {
            Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Starting proactive token refresh timer...");

            // Create a timer that fires every 2 hours (7,200,000 ms)
            // With aggressive startup refresh, we can use shorter intervals for ongoing sessions
            // This ensures tokens stay fresh during long streaming sessions
            var refreshTimer = new System.Timers.Timer(2 * 60 * 60 * 1000); // 2 hours in milliseconds
            refreshTimer.Elapsed += async (sender, e) => await ProactiveTokenRefresh();
            refreshTimer.AutoReset = true;
            refreshTimer.Start();

            Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Timer started - will refresh tokens every 2 hours");

            // Run a secondary check after 5 minutes for ongoing session maintenance
            var initialTimer = new System.Timers.Timer(5 * 60 * 1000); // 5 minutes
            initialTimer.Elapsed += async (sender, e) =>
            {
                initialTimer.Stop();
                initialTimer.Dispose();
                await ProactiveTokenRefresh();
            };
            initialTimer.AutoReset = false;
            initialTimer.Start();

            Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Secondary refresh check will run in 5 minutes");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Error starting timer: {ex.Message}");
        }
    }

    /// <summary>
    /// Proactively refreshes tokens if they're close to expiring
    /// </summary>
    private async Task ProactiveTokenRefresh()
    {
        try
        {
            Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] PROACTIVE TOKEN REFRESH CHECK");
            Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Current time: {DateTime.Now}");

            // Only attempt refresh if we have authentication and tokens
            if (!_settings.IsAuthenticated || string.IsNullOrEmpty(_settings.TwitchAccessToken))
            {
                Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Not authenticated or no access token - skipping");
                return;
            }

            if (string.IsNullOrEmpty(_settings.TwitchRefreshToken))
            {
                Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] No refresh token available - skipping");
                return;
            }

            Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Checking token validity...");

            // Check if current token is still valid
            var isValid = await _twitchClient.ValidateTokenAsync();
            Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Token validation result: {isValid}");

            if (!isValid)
            {
                Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Token invalid - attempting refresh...");

                var refreshSuccess = await _twitchClient.RefreshTokenAsync();
                Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Refresh result: {refreshSuccess}");

                if (refreshSuccess)
                {
                    Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Proactive refresh successful!");

                    // Save the new tokens immediately
                    _settings.TwitchAccessToken = _twitchClient.AccessToken ?? "";
                    _settings.TwitchRefreshToken = _twitchClient.RefreshToken ?? "";
                    _settings.IsAuthenticated = true;

                    Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] New tokens saved - Access: {_settings.TwitchAccessToken.Length} chars, Refresh: {_settings.TwitchRefreshToken.Length} chars");

                    // The AuthenticationChanged event will handle EventSub reconnection automatically
                }
                else
                {
                    Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Proactive refresh failed");

                    // Don't clear authentication here - let the user handle it when they try to use features
                    // Just log the issue for debugging
                }
            }
            else
            {
                Debug.WriteLine("TwitchSettingsForm: [PROACTIVE-REFRESH] Token still valid - no refresh needed");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Exception during proactive refresh: {ex.Message}");
            Debug.WriteLine($"TwitchSettingsForm: [PROACTIVE-REFRESH] Stack trace: {ex.StackTrace}");
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