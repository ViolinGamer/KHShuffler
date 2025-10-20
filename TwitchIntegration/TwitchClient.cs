using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Linq;
using System.IO;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Handles real Twitch OAuth authentication and API communication
/// </summary>
public class TwitchClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private string _clientId;
    private string _clientSecret;
    private readonly string _redirectUri;
    private readonly List<string> _scopes;

    public string? AccessToken { get; set; } // Made settable for loading from settings
    public string? RefreshToken { get; set; } // Made settable for loading from settings
    public string? Username { get; set; } // Made settable for loading from settings
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public event EventHandler<TwitchAuthEventArgs>? AuthenticationChanged;

    // Default redirect URI for all KHShuffler installations
    private const string REDIRECT_URI = "http://localhost:3000/auth/callback";

    public TwitchClient()
    {
        _httpClient = new HttpClient();
        _clientId = ""; // Will be set by user through UI
        _clientSecret = ""; // Will be set by user through UI
        _redirectUri = REDIRECT_URI;
        _scopes = new List<string>
        {
            "channel:read:subscriptions",  // Read subscription events
            "bits:read",                   // Read bits donations
            "channel:read:hype_train",     // Read hype train events (optional)
            "user:read:email"              // Read user email for identification
        };
    }

    /// <summary>
    /// Sets the Twitch app credentials (Client ID and Secret)
    /// </summary>
    public void SetCredentials(string clientId, string clientSecret)
    {
        _clientId = clientId?.Trim() ?? "";
        _clientSecret = clientSecret?.Trim() ?? "";

        // Update HTTP client headers
        _httpClient.DefaultRequestHeaders.Remove("Client-ID");
        if (!string.IsNullOrEmpty(_clientId))
        {
            _httpClient.DefaultRequestHeaders.Add("Client-ID", _clientId);
        }

        Debug.WriteLine($"TwitchClient: Credentials updated - ClientID length: {_clientId.Length}");
    }

    /// <summary>
    /// Gets the current client ID (for display purposes)
    /// </summary>
    public string GetClientId() => _clientId;

    /// <summary>
    /// Checks if the client is properly configured with valid credentials
    /// </summary>
    public bool IsClientConfigured()
    {
        return !string.IsNullOrEmpty(_clientId) &&
               !string.IsNullOrEmpty(_clientSecret) &&
               _clientId.Length > 10 && // Twitch Client IDs are typically 30+ characters
               _clientSecret.Length > 10; // Twitch Client Secrets are typically 30+ characters
    }

    /// <summary>
    /// Initiates the OAuth flow by opening the browser to Twitch
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            // Check if client is properly configured
            if (!IsClientConfigured())
            {
                var setupMessage = "?? Twitch App Credentials Required!\n\n" +
                                 "To use Twitch integration, you need to:\n\n" +
                                 "1. Create a Twitch app at: https://dev.twitch.tv/console\n" +
                                 "2. Enter your Client ID and Client Secret in the settings below\n\n" +
                                 "This keeps your credentials private and secure!\n\n" +
                                 "Would you like to open the Twitch Developer Console now?";

                var result = MessageBox.Show(setupMessage, "Twitch App Setup Required",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Open the Twitch developer console
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://dev.twitch.tv/console",
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // If opening fails, just show the URL
                        MessageBox.Show("Please visit: https://dev.twitch.tv/console", "Twitch Developer Console");
                    }
                }

                AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
                {
                    IsAuthenticated = false,
                    Username = "",
                    Message = "Credentials not configured - please enter your Twitch app details"
                });

                return false;
            }

            // Generate state parameter for security
            var state = Guid.NewGuid().ToString("N");

            // Build authorization URL
            var authUrl = BuildAuthorizationUrl(state);

            Debug.WriteLine($"TwitchClient: Opening browser for authentication: {authUrl}");

            // Open browser to Twitch OAuth page
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            // Start local server to listen for callback
            var authResult = await ListenForCallbackAsync(state);

            if (authResult.Success && !string.IsNullOrEmpty(authResult.AuthCode))
            {
                // Exchange authorization code for access token
                var tokenResult = await ExchangeCodeForTokenAsync(authResult.AuthCode);

                if (tokenResult.Success)
                {
                    AccessToken = tokenResult.AccessToken;
                    RefreshToken = tokenResult.RefreshToken;

                    // Get user info
                    var userInfo = await GetUserInfoAsync();
                    if (userInfo != null)
                    {
                        Username = userInfo.DisplayName;

                        Debug.WriteLine($"TwitchClient: Successfully authenticated as {Username}");

                        AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
                        {
                            IsAuthenticated = true,
                            Username = Username,
                            Message = "Successfully connected to Twitch!"
                        });

                        return true;
                    }
                }
                else
                {
                    // Show specific error for token exchange failure
                    var errorMessage = "Failed to exchange authorization code for access token.\n\n" +
                                     "This usually means:\n" +
                                     "� Invalid Client Secret\n" +
                                     "� Incorrect redirect URI in Twitch app settings\n" +
                                     "� Authorization code expired\n\n" +
                                     $"Error: {tokenResult.Error}";

                    MessageBox.Show(errorMessage, "Token Exchange Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                var errorMessage = !string.IsNullOrEmpty(authResult.Error)
                    ? $"Authorization failed: {authResult.Error}"
                    : "Authorization was cancelled or failed";

                MessageBox.Show(errorMessage, "Authorization Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
            {
                IsAuthenticated = false,
                Username = "",
                Message = "Failed to authenticate with Twitch"
            });

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Authentication error: {ex.Message}");

            var errorMessage = "Authentication error occurred:\n\n" +
                             $"{ex.Message}\n\n" +
                             "Please check:\n" +
                             "� Your internet connection\n" +
                             "� Twitch app configuration\n" +
                             "� Client ID and Secret are correct";

            MessageBox.Show(errorMessage, "Authentication Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
            {
                IsAuthenticated = false,
                Username = "",
                Message = $"Authentication error: {ex.Message}"
            });

            return false;
        }
    }

    /// <summary>
    /// Disconnects from Twitch by revoking the token
    /// </summary>
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(AccessToken))
            {
                // Revoke the access token
                await RevokeTokenAsync(AccessToken);
            }

            AccessToken = null;
            RefreshToken = null;
            Username = null;

            AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
            {
                IsAuthenticated = false,
                Username = "",
                Message = "Disconnected from Twitch"
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Disconnect error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates the current access token
    /// </summary>
    public async Task<bool> ValidateTokenAsync()
    {
        if (string.IsNullOrEmpty(AccessToken)) return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var validation = JsonSerializer.Deserialize<TwitchValidationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                Debug.WriteLine($"TwitchClient: Token validation successful for {validation?.Login}");
                return true;
            }
            else
            {
                Debug.WriteLine($"TwitchClient: Token validation failed: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Token validation error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
        {
            Debug.WriteLine("TwitchClient: [TOKEN-REFRESH] No refresh token available");
            return false;
        }

        try
        {
            Debug.WriteLine("TwitchClient: [TOKEN-REFRESH] Attempting to refresh access token...");

            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = RefreshToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchClient: [TOKEN-REFRESH] Success response: {responseContent}");

                var tokenResponse = JsonSerializer.Deserialize<TwitchTokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    AccessToken = tokenResponse.AccessToken;

                    // Refresh token might be updated too
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        RefreshToken = tokenResponse.RefreshToken;
                    }

                    Debug.WriteLine("TwitchClient: [TOKEN-REFRESH] Successfully refreshed access token");

                    AuthenticationChanged?.Invoke(this, new TwitchAuthEventArgs
                    {
                        IsAuthenticated = true,
                        Username = Username ?? "",
                        Message = "Token refreshed successfully"
                    });

                    return true;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchClient: [TOKEN-REFRESH] Failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: [TOKEN-REFRESH] Exception: {ex.Message}");
        }

        Debug.WriteLine("TwitchClient: [TOKEN-REFRESH] Token refresh failed");
        return false;
    }

    private string BuildAuthorizationUrl(string state)
    {
        var scopesString = string.Join(" ", _scopes);

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = scopesString,
            ["state"] = state
        };

        var queryString = string.Join("&",
            parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"https://id.twitch.tv/oauth2/authorize?{queryString}";
    }

    private async Task<CallbackResult> ListenForCallbackAsync(string expectedState)
    {
        // This is a simplified version - in production you'd want a proper HTTP server
        // For now, we'll show a dialog asking the user to paste the callback URL

        var instructionDialog = new Form
        {
            Text = "Twitch Authentication",
            Size = new System.Drawing.Size(500, 300),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var instructionLabel = new Label
        {
            Text = "After authorizing with Twitch, you'll be redirected to a page that shows an error.\n\n" +
                   "Copy the ENTIRE URL from your browser's address bar and paste it below:",
            AutoSize = false,
            Size = new System.Drawing.Size(460, 60),
            Location = new System.Drawing.Point(20, 20),
            TextAlign = System.Drawing.ContentAlignment.TopLeft
        };

        var urlTextBox = new TextBox
        {
            Size = new System.Drawing.Size(460, 25),
            Location = new System.Drawing.Point(20, 90),
            PlaceholderText = "Paste the callback URL here..."
        };

        var okButton = new Button
        {
            Text = "OK",
            Size = new System.Drawing.Size(75, 25),
            Location = new System.Drawing.Point(320, 130),
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new System.Drawing.Size(75, 25),
            Location = new System.Drawing.Point(405, 130),
            DialogResult = DialogResult.Cancel
        };

        instructionDialog.Controls.AddRange(new Control[] { instructionLabel, urlTextBox, okButton, cancelButton });
        instructionDialog.AcceptButton = okButton;
        instructionDialog.CancelButton = cancelButton;

        var result = instructionDialog.ShowDialog();

        if (result == DialogResult.OK && !string.IsNullOrEmpty(urlTextBox.Text))
        {
            try
            {
                var uri = new Uri(urlTextBox.Text);
                var query = HttpUtility.ParseQueryString(uri.Query);

                var code = query["code"];
                var state = query["state"];

                if (state == expectedState && !string.IsNullOrEmpty(code))
                {
                    return new CallbackResult { Success = true, AuthCode = code };
                }
                else
                {
                    return new CallbackResult { Success = false, Error = "Invalid state or missing authorization code" };
                }
            }
            catch (Exception ex)
            {
                return new CallbackResult { Success = false, Error = $"Invalid URL format: {ex.Message}" };
            }
        }

        return new CallbackResult { Success = false, Error = "Authentication cancelled" };
    }

    private async Task<TokenResult> ExchangeCodeForTokenAsync(string authCode)
    {
        try
        {
            var tokenRequest = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["code"] = authCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = _redirectUri
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TwitchTokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                return new TokenResult
                {
                    Success = true,
                    AccessToken = tokenResponse?.AccessToken,
                    RefreshToken = tokenResponse?.RefreshToken
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchClient: Token exchange failed: {response.StatusCode} - {errorContent}");

                return new TokenResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {response.StatusCode}"
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Token exchange error: {ex.Message}");
            return new TokenResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<TwitchUser?> GetUserInfoAsync()
    {
        if (string.IsNullOrEmpty(AccessToken)) return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userResponse = JsonSerializer.Deserialize<TwitchUsersResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                return userResponse?.Data?.FirstOrDefault();
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Get user info error: {ex.Message}");
            return null;
        }
    }

    private async Task RevokeTokenAsync(string token)
    {
        try
        {
            var revokeRequest = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["token"] = token
            };

            var content = new FormUrlEncodedContent(revokeRequest);
            await _httpClient.PostAsync("https://id.twitch.tv/oauth2/revoke", content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchClient: Token revocation error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Data classes for Twitch API responses
public class TwitchTokenResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? TokenType { get; set; }
    public string[]? Scope { get; set; }
}

public class TwitchValidationResponse
{
    public string? ClientId { get; set; }
    public string? Login { get; set; }
    public string[]? Scopes { get; set; }
    public string? UserId { get; set; }
    public int ExpiresIn { get; set; }
}

public class TwitchUsersResponse
{
    public TwitchUser[]? Data { get; set; }
}

public class TwitchUser
{
    public string? Id { get; set; }
    public string? Login { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? ProfileImageUrl { get; set; }
}

// Helper classes
public class CallbackResult
{
    public bool Success { get; set; }
    public string? AuthCode { get; set; }
    public string? Error { get; set; }
}

public class TokenResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Error { get; set; }
}

public class TwitchAuthEventArgs : EventArgs
{
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; } = "";
    public string Message { get; set; } = "";
}