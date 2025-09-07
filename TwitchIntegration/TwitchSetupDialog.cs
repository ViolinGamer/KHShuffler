using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Helper dialog to guide users through Twitch app setup
/// </summary>
public class TwitchSetupDialog : Form
{
    private readonly Button _createAppButton = new() { Text = "1. Create Twitch App", Size = new Size(200, 35) };
    private readonly Button _setupGuideButton = new() { Text = "2. View Setup Guide", Size = new Size(200, 35) };
    private readonly Button _closeButton = new() { Text = "Close", Size = new Size(100, 35) };
    
    public TwitchSetupDialog()
    {
        InitializeDialog();
        SetupEventHandlers();
    }
    
    private void InitializeDialog()
    {
        Text = "Twitch Integration Setup";
        Size = new Size(500, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20)
        };
        
        // Title
        var titleLabel = new Label
        {
            Text = "?? Twitch Integration Setup Required",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            AutoSize = false,
            Size = new Size(450, 40),
            TextAlign = ContentAlignment.MiddleCenter
        };
        
        // Instructions
        var instructionsLabel = new Label
        {
            Text = "To use real Twitch integration with your streams, you need to create a Twitch application and configure KHShuffler with your credentials.\n\n" +
                   "This is a one-time setup process that takes about 5 minutes.",
            Font = new Font("Segoe UI", 10),
            AutoSize = false,
            Size = new Size(450, 80),
            TextAlign = ContentAlignment.TopLeft
        };
        
        // Steps
        var stepsLabel = new Label
        {
            Text = "?? Setup Steps:\n\n" +
                   "1. Create a Twitch application at dev.twitch.tv\n" +
                   "2. Get your Client ID and Client Secret\n" +
                   "3. Update TwitchClient.cs with your credentials\n" +
                   "4. Build and test the integration",
            Font = new Font("Segoe UI", 10),
            AutoSize = false,
            Size = new Size(450, 120),
            TextAlign = ContentAlignment.TopLeft
        };
        
        // Buttons panel
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Size = new Size(450, 50),
            Padding = new Padding(10)
        };
        
        _createAppButton.BackColor = Color.LightGreen;
        _setupGuideButton.BackColor = Color.LightBlue;
        _closeButton.BackColor = Color.LightCoral;
        
        buttonPanel.Controls.AddRange(new Control[] { _createAppButton, _setupGuideButton, _closeButton });
        
        mainPanel.Controls.AddRange(new Control[] 
        { 
            titleLabel, instructionsLabel, stepsLabel, buttonPanel 
        });
        
        Controls.Add(mainPanel);
    }
    
    private void SetupEventHandlers()
    {
        _createAppButton.Click += (_, __) => OpenTwitchDeveloperConsole();
        _setupGuideButton.Click += (_, __) => OpenSetupGuide();
        _closeButton.Click += (_, __) => Close();
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
            
            MessageBox.Show("?? Opened Twitch Developer Console in your browser!\n\n" +
                          "Create a new application with these settings:\n" +
                          "• Name: KHShuffler-YourUsername\n" +
                          "• OAuth Redirect URLs: http://localhost:3000/auth/callback\n" +
                          "• Category: Game Integration", 
                          "Twitch App Creation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open browser. Please manually visit:\nhttps://dev.twitch.tv/console\n\nError: {ex.Message}",
                          "Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    
    private void OpenSetupGuide()
    {
        try
        {
            var setupGuidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TWITCH_SETUP_GUIDE.md");
            
            if (File.Exists(setupGuidePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = setupGuidePath,
                    UseShellExecute = true
                });
            }
            else
            {
                // Show instructions directly if file doesn't exist
                var instructions = "?? Setup Instructions:\n\n" +
                                 "1. Create Twitch app at: https://dev.twitch.tv/console\n" +
                                 "2. Copy your Client ID and Client Secret\n" +
                                 "3. Open: TwitchIntegration/TwitchClient.cs\n" +
                                 "4. Replace 'your_app_client_id_here' with your Client ID\n" +
                                 "5. Replace 'your_client_secret_here' with your Client Secret\n" +
                                 "6. Build the project and try authentication again\n\n" +
                                 "For detailed instructions, see TWITCH_SETUP_GUIDE.md";
                
                MessageBox.Show(instructions, "Setup Instructions", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open setup guide: {ex.Message}\n\n" +
                          "Please see TWITCH_SETUP_GUIDE.md for detailed instructions.",
                          "Guide Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}