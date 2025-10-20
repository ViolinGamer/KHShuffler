using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Handles real-time Twitch EventSub WebSocket connections for live stream events
/// </summary>
public class TwitchEventSubClient : IDisposable
{
    private readonly TwitchClient _twitchClient;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected = false;
    private string? _sessionId;
    private readonly List<string> _subscribedEvents = new();

    // Events for real-time stream interactions
    public event EventHandler<TwitchEventArgs>? SubscriptionReceived;
    public event EventHandler<TwitchEventArgs>? BitsReceived;
    public event EventHandler<TwitchEventArgs>? FollowReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    private const string EVENTSUB_WEBSOCKET_URL = "wss://eventsub.wss.twitch.tv/ws";

    public TwitchEventSubClient(TwitchClient twitchClient)
    {
        _twitchClient = twitchClient;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Connects to Twitch EventSub WebSocket and sets up live event subscriptions
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (!_twitchClient.IsAuthenticated)
            {
                Debug.WriteLine("TwitchEventSub: Cannot connect - not authenticated");
                Debug.WriteLine($"TwitchEventSub: AccessToken exists: {!string.IsNullOrEmpty(_twitchClient.AccessToken)}");
                Debug.WriteLine($"TwitchEventSub: Username: '{_twitchClient.Username}'");
                ConnectionStatusChanged?.Invoke(this, "? Not authenticated - please connect to Twitch first");
                return false;
            }

            Debug.WriteLine("TwitchEventSub: Connecting to Twitch EventSub WebSocket...");
            Debug.WriteLine($"TwitchEventSub: Using AccessToken: {(_twitchClient.AccessToken?.Length > 10 ? _twitchClient.AccessToken.Substring(0, 10) + "..." : _twitchClient.AccessToken ?? "null")}");
            Debug.WriteLine($"TwitchEventSub: Client ID: {_twitchClient.GetClientId()}");
            ConnectionStatusChanged?.Invoke(this, "Connecting to Twitch EventSub...");

            _cancellationTokenSource = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            // Connect to Twitch EventSub WebSocket
            Debug.WriteLine($"TwitchEventSub: Attempting WebSocket connection to {EVENTSUB_WEBSOCKET_URL}");
            await _webSocket.ConnectAsync(new Uri(EVENTSUB_WEBSOCKET_URL), _cancellationTokenSource.Token);
            _isConnected = true;

            Debug.WriteLine($"TwitchEventSub: WebSocket State: {_webSocket.State}");
            Debug.WriteLine("TwitchEventSub: Connected to WebSocket, waiting for welcome message...");

            // Start listening for messages
            _ = Task.Run(async () => await ListenForMessagesAsync(_cancellationTokenSource.Token));

            ConnectionStatusChanged?.Invoke(this, "Connected to WebSocket, waiting for session...");

            // Wait a bit to see if we get a welcome message
            await Task.Delay(5000); // 5 second timeout

            if (string.IsNullOrEmpty(_sessionId))
            {
                Debug.WriteLine("TwitchEventSub: ERROR - No session ID received after 5 seconds");
                ConnectionStatusChanged?.Invoke(this, "? Failed to establish session with Twitch");
                return false;
            }

            Debug.WriteLine($"TwitchEventSub: SUCCESS - Session established: {_sessionId}");
            ConnectionStatusChanged?.Invoke(this, "Connected to Twitch EventSub");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Connection error: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: Exception details: {ex}");
            ConnectionStatusChanged?.Invoke(this, $"? Connection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnects from Twitch EventSub WebSocket
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            _isConnected = false;

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }

            Debug.WriteLine("TwitchEventSub: Disconnected");
            ConnectionStatusChanged?.Invoke(this, "Disconnected from Twitch EventSub");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Disconnect error: {ex.Message}");
        }
    }

    /// <summary>
    /// Listens for incoming WebSocket messages from Twitch
    /// </summary>
    private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    break;
                }

                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessWebSocketMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.WriteLine("TwitchEventSub: WebSocket closed by server");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("TwitchEventSub: Listen loop cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Listen error: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, $"? Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes incoming WebSocket messages from Twitch
    /// </summary>
    private async Task ProcessWebSocketMessage(string message)
    {
        try
        {
            Debug.WriteLine($"TwitchEventSub: Received message: {message.Substring(0, Math.Min(200, message.Length))}...");

            var jsonDoc = JsonDocument.Parse(message);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("metadata", out var metadata))
            {
                return;
            }

            if (!metadata.TryGetProperty("message_type", out var messageType))
            {
                return;
            }

            var messageTypeString = messageType.GetString();

            switch (messageTypeString)
            {
                case "session_welcome":
                    await HandleWelcomeMessage(root);
                    break;

                case "session_keepalive":
                    Debug.WriteLine("TwitchEventSub: Received keepalive");
                    break;

                case "notification":
                    await HandleNotificationMessage(root);
                    break;

                case "session_reconnect":
                    Debug.WriteLine("TwitchEventSub: Server requested reconnect");
                    await HandleReconnectMessage(root);
                    break;

                default:
                    Debug.WriteLine($"TwitchEventSub: Unknown message type: {messageTypeString}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error processing message: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the welcome message and sets up event subscriptions
    /// </summary>
    private async Task HandleWelcomeMessage(JsonElement root)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: Processing welcome message...");

            if (root.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("session", out var session) &&
                session.TryGetProperty("id", out var sessionId))
            {
                _sessionId = sessionId.GetString();
                Debug.WriteLine($"TwitchEventSub: Session established: {_sessionId}");

                // Set up event subscriptions now that we have a session
                Debug.WriteLine("TwitchEventSub: Setting up event subscriptions...");
                await SetupEventSubscriptions();

                Debug.WriteLine($"TwitchEventSub: Successfully subscribed to {_subscribedEvents.Count} events");
                ConnectionStatusChanged?.Invoke(this, $"Ready! Subscribed to {_subscribedEvents.Count} event types");
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: ERROR - Welcome message missing session data");
                Debug.WriteLine($"TwitchEventSub: Welcome message content: {root}");
                ConnectionStatusChanged?.Invoke(this, "? Invalid welcome message from Twitch");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling welcome: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: Welcome exception: {ex}");
            ConnectionStatusChanged?.Invoke(this, $"? Welcome error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets up subscriptions for live Twitch events
    /// </summary>
    private async Task SetupEventSubscriptions()
    {
        // Clear any existing subscriptions to prevent duplicates on reconnection
        _subscribedEvents.Clear();
        Debug.WriteLine("TwitchEventSub: [SETUP-DEBUG] Cleared existing subscription list to prevent duplicates");

        // Enhanced pre-flight validation with detailed debugging
        Debug.WriteLine("TwitchEventSub: [SETUP-DEBUG] Starting subscription setup...");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Session ID: '{_sessionId ?? "NULL"}'");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Session ID empty: {string.IsNullOrEmpty(_sessionId)}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] TwitchClient authenticated: {_twitchClient.IsAuthenticated}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] TwitchClient configured: {_twitchClient.IsClientConfigured()}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Access token present: {!string.IsNullOrEmpty(_twitchClient.AccessToken)}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Access token length: {_twitchClient.AccessToken?.Length ?? 0}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Client ID present: {!string.IsNullOrEmpty(_twitchClient.GetClientId())}");
        Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Client ID length: {_twitchClient.GetClientId()?.Length ?? 0}");

        if (string.IsNullOrEmpty(_sessionId) || !_twitchClient.IsAuthenticated)
        {
            Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] Cannot setup subscriptions - missing session or auth");
            Debug.WriteLine($"TwitchEventSub: [SETUP-ERROR] Session ID: {_sessionId}");
            Debug.WriteLine($"TwitchEventSub: [SETUP-ERROR] Authenticated: {_twitchClient.IsAuthenticated}");

            // Enhanced diagnostics for missing requirements
            if (string.IsNullOrEmpty(_sessionId))
            {
                Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] Session ID is missing - WebSocket connection may not be established");
            }

            if (!_twitchClient.IsAuthenticated)
            {
                Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] TwitchClient not authenticated - OAuth flow may have failed");
                if (string.IsNullOrEmpty(_twitchClient.AccessToken))
                {
                    Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] Access token is missing");
                }
                if (!_twitchClient.IsClientConfigured())
                {
                    Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] Client credentials not configured properly");
                }
            }

            return;
        }

        try
        {
            // Get the user ID for the authenticated user
            Debug.WriteLine("TwitchEventSub: [SETUP-DEBUG] Getting user ID for authenticated user...");
            var userId = await GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("TwitchEventSub: [SETUP-ERROR] Could not get user ID - cannot set up subscriptions");
                ConnectionStatusChanged?.Invoke(this, "❌ Could not get user ID for subscriptions");
                return;
            }

            Debug.WriteLine($"TwitchEventSub: [SETUP-SUCCESS] Setting up subscriptions for user ID: {userId}");

            var subscriptionAttempts = new (string, object)[]
            {
                // Using multiple events to cover all subscription types:
                // - channel.subscribe: new subscriptions ONLY (excludes resubscriptions)
                // - channel.subscription.gift: gifted subscriptions (single and community gifts)
                // - channel.subscription.message: resub announcements when user shares a message
                // - channel.cheer: bits/cheers
                // This combination covers all subs, resubs, and gifts while avoiding OAuth scope issues
                ("channel.subscribe", new { broadcaster_user_id = userId }),
                ("channel.subscription.gift", new { broadcaster_user_id = userId }),
                ("channel.subscription.message", new { broadcaster_user_id = userId }),
                ("channel.cheer", new { broadcaster_user_id = userId })
            };

            Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Attempting to subscribe to {subscriptionAttempts.Length} event types...");

            foreach (var subscription in subscriptionAttempts)
            {
                Debug.WriteLine($"TwitchEventSub: [SETUP-DEBUG] Attempting to subscribe to {subscription.Item1}...");
                await SubscribeToEvent(subscription.Item1, subscription.Item2);
            }

            Debug.WriteLine($"TwitchEventSub: [SETUP-RESULT] Subscription setup complete. Subscribed events: {_subscribedEvents.Count}");

            if (_subscribedEvents.Count == 0)
            {
                Debug.WriteLine("TwitchEventSub: [SETUP-WARNING] No events were successfully subscribed!");
                ConnectionStatusChanged?.Invoke(this, "⚠️ Connected but failed to subscribe to events");
            }
            else
            {
                Debug.WriteLine($"TwitchEventSub: [SETUP-SUCCESS] Subscribed to: {string.Join(", ", _subscribedEvents)}");
                ConnectionStatusChanged?.Invoke(this, $"✅ Subscribed to {_subscribedEvents.Count} events: {string.Join(", ", _subscribedEvents)}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [SETUP-EXCEPTION] Error setting up subscriptions: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [SETUP-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [SETUP-EXCEPTION] Full exception: {ex}");
            ConnectionStatusChanged?.Invoke(this, $"❌ Subscription error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the user ID for the authenticated user
    /// </summary>
    private async Task<string?> GetUserIdAsync()
    {
        try
        {
            // Enhanced pre-flight validation
            Debug.WriteLine("TwitchEventSub: [USER-ID-DEBUG] Starting user ID retrieval...");

            // Validate authentication prerequisites
            if (string.IsNullOrEmpty(_twitchClient.AccessToken))
            {
                Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] Access token is null or empty!");
                return null;
            }

            if (string.IsNullOrEmpty(_twitchClient.GetClientId()))
            {
                Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] Client ID is null or empty!");
                return null;
            }

            if (!_twitchClient.IsAuthenticated)
            {
                Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] TwitchClient reports not authenticated!");
                return null;
            }

            Debug.WriteLine($"TwitchEventSub: [USER-ID-DEBUG] Access token length: {_twitchClient.AccessToken.Length}");
            Debug.WriteLine($"TwitchEventSub: [USER-ID-DEBUG] Client ID length: {_twitchClient.GetClientId().Length}");
            Debug.WriteLine($"TwitchEventSub: [USER-ID-DEBUG] Authentication status: {_twitchClient.IsAuthenticated}");

            Debug.WriteLine("TwitchEventSub: Making API call to get user info...");

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _twitchClient.AccessToken);
            request.Headers.Add("Client-Id", _twitchClient.GetClientId());

            Debug.WriteLine($"TwitchEventSub: Request headers - Bearer token: {(_twitchClient.AccessToken?.Length > 10 ? _twitchClient.AccessToken.Substring(0, 10) + "..." : _twitchClient.AccessToken ?? "null")}");
            Debug.WriteLine($"TwitchEventSub: Request headers - Client-Id: {_twitchClient.GetClientId()}");

            var response = await _httpClient.SendAsync(request);

            Debug.WriteLine($"TwitchEventSub: [USER-ID-RESPONSE] Status: {response.StatusCode}");
            Debug.WriteLine($"TwitchEventSub: [USER-ID-RESPONSE] Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"))}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: [USER-ID-SUCCESS] Response content: {content}");

                if (string.IsNullOrEmpty(content))
                {
                    Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] API returned empty content!");
                    return null;
                }

                var userResponse = JsonSerializer.Deserialize<TwitchUsersResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (userResponse?.Data == null || !userResponse.Data.Any())
                {
                    Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] No user data in API response!");
                    return null;
                }

                var userId = userResponse.Data.FirstOrDefault()?.Id;
                Debug.WriteLine($"TwitchEventSub: [USER-ID-SUCCESS] Extracted user ID: {userId}");

                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] User ID is null or empty in response!");
                    return null;
                }

                return userId;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: [USER-ID-ERROR] API error: {response.StatusCode} - {errorContent}");

                // If we get 401 Unauthorized, try refreshing the token
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] 401 Unauthorized - attempting token refresh...");

                    var refreshSuccess = await _twitchClient.RefreshTokenAsync();
                    if (refreshSuccess)
                    {
                        Debug.WriteLine("TwitchEventSub: [USER-ID-RETRY] Token refreshed, retrying user ID request...");

                        // Retry the request with the new token
                        var retryRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
                        retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _twitchClient.AccessToken);
                        retryRequest.Headers.Add("Client-Id", _twitchClient.GetClientId());

                        var retryResponse = await _httpClient.SendAsync(retryRequest);

                        if (retryResponse.IsSuccessStatusCode)
                        {
                            var retryContent = await retryResponse.Content.ReadAsStringAsync();
                            Debug.WriteLine($"TwitchEventSub: [USER-ID-RETRY-SUCCESS] Response: {retryContent}");

                            var retryUserResponse = JsonSerializer.Deserialize<TwitchUsersResponse>(retryContent, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                            });

                            if (retryUserResponse?.Data != null && retryUserResponse.Data.Any())
                            {
                                var userId = retryUserResponse.Data.FirstOrDefault()?.Id;
                                Debug.WriteLine($"TwitchEventSub: [USER-ID-RETRY-SUCCESS] Got user ID after refresh: {userId}");
                                return userId;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"TwitchEventSub: [USER-ID-RETRY-FAILED] Retry failed: {retryResponse.StatusCode}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] Token refresh failed");
                    }
                }

                // Parse error details if available
                try
                {
                    var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorObj.TryGetProperty("message", out var msgProp))
                    {
                        Debug.WriteLine($"TwitchEventSub: [USER-ID-ERROR] API error message: {msgProp.GetString()}");
                    }
                    if (errorObj.TryGetProperty("error", out var errProp))
                    {
                        Debug.WriteLine($"TwitchEventSub: [USER-ID-ERROR] API error type: {errProp.GetString()}");
                    }
                }
                catch
                {
                    Debug.WriteLine("TwitchEventSub: [USER-ID-ERROR] Could not parse error response JSON");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [USER-ID-EXCEPTION] Error getting user ID: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [USER-ID-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [USER-ID-EXCEPTION] Full exception: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Subscribes to a specific Twitch event type
    /// </summary>
    private async Task SubscribeToEvent(string eventType, object condition)
    {
        try
        {
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Creating subscription for {eventType}...");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Condition: {JsonSerializer.Serialize(condition)}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Session ID: {_sessionId}");

            var subscriptionData = new
            {
                type = eventType,
                version = "1",
                condition = condition,
                transport = new
                {
                    method = "websocket",
                    session_id = _sessionId
                }
            };

            var json = JsonSerializer.Serialize(subscriptionData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Subscription JSON for {eventType}: {json}");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _twitchClient.AccessToken);
            request.Headers.Add("Client-Id", _twitchClient.GetClientId());

            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Request headers - Authorization: Bearer {(_twitchClient.AccessToken?.Length > 10 ? _twitchClient.AccessToken.Substring(0, 10) + "..." : "null")}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Request headers - Client-Id: {_twitchClient.GetClientId()}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-DEBUG] Sending subscription request to Twitch API...");

            var response = await _httpClient.SendAsync(request);

            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-RESPONSE] Subscription response for {eventType}: {response.StatusCode}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-RESPONSE] Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"))}");

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-SUCCESS] Subscription success response: {responseContent}");

                // Parse response to get subscription details
                try
                {
                    var responseDoc = JsonDocument.Parse(responseContent);
                    if (responseDoc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var subElement in dataArray.EnumerateArray())
                        {
                            if (subElement.TryGetProperty("id", out var idProp))
                            {
                                Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-SUCCESS] Created subscription ID: {idProp.GetString()}");
                            }
                            if (subElement.TryGetProperty("status", out var statusProp))
                            {
                                Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-SUCCESS] Subscription status: {statusProp.GetString()}");
                            }
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-WARNING] Could not parse success response: {parseEx.Message}");
                }

                _subscribedEvents.Add(eventType);
                Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-SUCCESS] Successfully subscribed to {eventType}. Total subscriptions: {_subscribedEvents.Count}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Subscription FAILED for {eventType}: {response.StatusCode} - {errorContent}");

                // Try to parse the error for more details
                try
                {
                    var errorDoc = JsonDocument.Parse(errorContent);
                    if (errorDoc.RootElement.TryGetProperty("message", out var message))
                    {
                        Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Error message: {message.GetString()}");
                    }
                    if (errorDoc.RootElement.TryGetProperty("error", out var error))
                    {
                        Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Error type: {error.GetString()}");
                    }
                    if (errorDoc.RootElement.TryGetProperty("status", out var status))
                    {
                        Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Error status: {status.GetInt32()}");
                    }
                }
                catch (Exception parseEx)
                {
                    Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Could not parse error response: {parseEx.Message}");
                }

                // Specific error analysis
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Conflict error - subscription may already exist for {eventType}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Unauthorized - check access token and scopes for {eventType}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-ERROR] Bad request - check subscription format for {eventType}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-EXCEPTION] Exception subscribing to {eventType}: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [SUB-SETUP-EXCEPTION] Full exception: {ex}");
        }
    }

    /// <summary>
    /// Handles incoming event notifications
    /// </summary>
    private async Task HandleNotificationMessage(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("subscription", out var subscription) ||
                !subscription.TryGetProperty("type", out var eventType) ||
                !payload.TryGetProperty("event", out var eventData))
            {
                return;
            }

            var eventTypeString = eventType.GetString();
            Debug.WriteLine($"TwitchEventSub: Received {eventTypeString} event");

            switch (eventTypeString)
            {
                case "channel.subscribe":
                    await HandleSubscriptionEvent(eventData, false);
                    break;

                case "channel.subscription.gift":
                    await HandleSubscriptionEvent(eventData, true);
                    break;

                case "channel.subscription.message":
                    await HandleSubscriptionMessageEvent(eventData);
                    break;

                case "channel.cheer":
                    await HandleCheerEvent(eventData);
                    break;

                default:
                    Debug.WriteLine($"TwitchEventSub: Unhandled event type: {eventTypeString}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles subscription events (new subs)
    /// </summary>
    private Task HandleSubscriptionEvent(JsonElement eventData, bool isGift)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [SUB-DEBUG] Processing subscription event...");
            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Is gift: {isGift}");
            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Raw event data: {eventData}");

            // Parse username with enhanced debugging
            var userName = "Unknown";
            if (eventData.TryGetProperty("user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Unknown";
                Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Extracted username: '{userName}'");
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: [SUB-WARNING] No 'user_name' property found in event data");
            }

            // Parse tier with enhanced debugging
            var tier = "1000";
            if (eventData.TryGetProperty("tier", out var tierProp))
            {
                tier = tierProp.GetString() ?? "1000";
                Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Extracted tier: '{tier}'");
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: [SUB-WARNING] No 'tier' property found in event data, defaulting to 1000");
            }

            var subTier = tier switch
            {
                "1000" => SubTier.Tier1,
                "2000" => SubTier.Tier2,
                "3000" => SubTier.Tier3,
                "Prime" => SubTier.Prime,
                _ => SubTier.Tier1
            };

            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Mapped tier '{tier}' to SubTier.{subTier}");

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = isGift ? "gifted a subscription!" : "subscribed!",
                SubTier = subTier,
                GiftCount = 1,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [SUB-SUCCESS] {userName} {eventArgs.Message} (Tier {subTier})");
            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Created event args - Username: '{eventArgs.Username}', Message: '{eventArgs.Message}', SubTier: {eventArgs.SubTier}");
            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] Firing SubscriptionReceived event...");

            SubscriptionReceived?.Invoke(this, eventArgs);

            Debug.WriteLine($"TwitchEventSub: [SUB-DEBUG] SubscriptionReceived event fired successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [SUB-EXCEPTION] Error handling subscription: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [SUB-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [SUB-EXCEPTION] Event data: {eventData}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles subscription message events (resub announcements with chat messages)
    /// This triggers when a subscriber shares a message with their resub in chat
    /// </summary>
    private Task HandleSubscriptionMessageEvent(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [SUB-MSG-DEBUG] Processing subscription message (resub announcement) event...");
            Debug.WriteLine($"TwitchEventSub: [SUB-MSG-DEBUG] Raw event data: {eventData}");

            // Parse username
            var userName = "Unknown";
            if (eventData.TryGetProperty("user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Unknown";
                Debug.WriteLine($"TwitchEventSub: [SUB-MSG-DEBUG] Extracted username: '{userName}'");
            }

            // Parse tier
            var tier = "1000";
            if (eventData.TryGetProperty("tier", out var tierProp))
            {
                tier = tierProp.GetString() ?? "1000";
                Debug.WriteLine($"TwitchEventSub: [SUB-MSG-DEBUG] Extracted tier: '{tier}'");
            }

            // Parse cumulative months
            var cumulativeMonths = 1;
            if (eventData.TryGetProperty("cumulative_months", out var monthsProp))
            {
                cumulativeMonths = monthsProp.GetInt32();
                Debug.WriteLine($"TwitchEventSub: [SUB-MSG-DEBUG] Extracted cumulative months: {cumulativeMonths}");
            }

            // Parse the subscription message if available
            var subMessage = "";
            if (eventData.TryGetProperty("message", out var messageProp))
            {
                if (messageProp.TryGetProperty("text", out var textProp))
                {
                    subMessage = textProp.GetString() ?? "";
                    Debug.WriteLine($"TwitchEventSub: [SUB-MSG-DEBUG] Extracted message: '{subMessage}'");
                }
            }

            var subTier = tier switch
            {
                "1000" => SubTier.Tier1,
                "2000" => SubTier.Tier2,
                "3000" => SubTier.Tier3,
                "Prime" => SubTier.Prime,
                _ => SubTier.Tier1
            };

            // Create appropriate message for resub announcement
            var displayMessage = $"resubscribed for {cumulativeMonths} months!" + (string.IsNullOrEmpty(subMessage) ? "" : $" \"{subMessage}\"");

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = displayMessage,
                SubTier = subTier,
                GiftCount = 1,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [SUB-MSG-SUCCESS] {userName} announced resub ({cumulativeMonths} months) (Tier {subTier})");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [SUB-MSG-EXCEPTION] Error handling subscription message: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [SUB-MSG-EXCEPTION] Stack trace: {ex.StackTrace}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles chat notification events (subscriptions, resubs, gift subs that appear in chat)
    /// This event covers all subscription-related announcements that appear in chat
    /// </summary>
    private Task HandleChatNotificationEvent(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [CHAT-NOTIF-DEBUG] Processing chat notification event...");
            Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-DEBUG] Raw event data: {eventData}");

            // Check notice type to filter for subscription-related events only
            if (!eventData.TryGetProperty("notice_type", out var noticeTypeProp))
            {
                Debug.WriteLine("TwitchEventSub: [CHAT-NOTIF-DEBUG] No notice_type found, ignoring event");
                return Task.CompletedTask;
            }

            var noticeType = noticeTypeProp.GetString();
            Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-DEBUG] Notice type: '{noticeType}'");

            // Only process subscription-related notifications
            switch (noticeType)
            {
                case "sub":
                    return HandleNewSubscriptionNotification(eventData);

                case "resub":
                    return HandleResubscriptionNotification(eventData);

                case "sub_gift":
                case "community_sub_gift":
                    return HandleGiftSubscriptionNotification(eventData);

                default:
                    Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-DEBUG] Ignoring non-subscription notification: {noticeType}");
                    return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-EXCEPTION] Error handling chat notification: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [CHAT-NOTIF-EXCEPTION] Event data: {eventData}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles new subscription notifications from chat
    /// </summary>
    private Task HandleNewSubscriptionNotification(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [NEW-SUB-DEBUG] Processing new subscription notification...");

            // Parse username
            var userName = "Unknown";
            if (eventData.TryGetProperty("chatter_user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Unknown";
                Debug.WriteLine($"TwitchEventSub: [NEW-SUB-DEBUG] Extracted username: '{userName}'");
            }

            // Parse subscription data
            var subTier = SubTier.Tier1;
            if (eventData.TryGetProperty("sub", out var subProp))
            {
                if (subProp.TryGetProperty("sub_plan", out var planProp))
                {
                    var plan = planProp.GetString();
                    subTier = plan switch
                    {
                        "1000" => SubTier.Tier1,
                        "2000" => SubTier.Tier2,
                        "3000" => SubTier.Tier3,
                        "Prime" => SubTier.Prime,
                        _ => SubTier.Tier1
                    };
                    Debug.WriteLine($"TwitchEventSub: [NEW-SUB-DEBUG] Subscription tier: {subTier}");
                }
            }

            // Parse the chat message if available
            var subMessage = "";
            if (eventData.TryGetProperty("message", out var messageProp))
            {
                if (messageProp.TryGetProperty("text", out var textProp))
                {
                    subMessage = textProp.GetString() ?? "";
                    Debug.WriteLine($"TwitchEventSub: [NEW-SUB-DEBUG] Subscription message: '{subMessage}'");
                }
            }

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = "subscribed!" + (string.IsNullOrEmpty(subMessage) ? "" : $" \"{subMessage}\""),
                SubTier = subTier,
                GiftCount = 1,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [NEW-SUB-SUCCESS] {userName} subscribed (Tier {subTier})");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [NEW-SUB-EXCEPTION] Error handling new subscription: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles resubscription notifications from chat
    /// </summary>
    private Task HandleResubscriptionNotification(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [RESUB-DEBUG] Processing resubscription notification...");

            // Parse username
            var userName = "Unknown";
            if (eventData.TryGetProperty("chatter_user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Unknown";
                Debug.WriteLine($"TwitchEventSub: [RESUB-DEBUG] Extracted username: '{userName}'");
            }

            // Parse resubscription data
            var subTier = SubTier.Tier1;
            var cumulativeMonths = 1;
            var streakMonths = 0;

            if (eventData.TryGetProperty("resub", out var resubProp))
            {
                if (resubProp.TryGetProperty("sub_plan", out var planProp))
                {
                    var plan = planProp.GetString();
                    subTier = plan switch
                    {
                        "1000" => SubTier.Tier1,
                        "2000" => SubTier.Tier2,
                        "3000" => SubTier.Tier3,
                        "Prime" => SubTier.Prime,
                        _ => SubTier.Tier1
                    };
                }

                if (resubProp.TryGetProperty("cumulative_months", out var monthsProp))
                {
                    cumulativeMonths = monthsProp.GetInt32();
                }

                if (resubProp.TryGetProperty("streak_months", out var streakProp) && streakProp.ValueKind != JsonValueKind.Null)
                {
                    streakMonths = streakProp.GetInt32();
                }
            }

            Debug.WriteLine($"TwitchEventSub: [RESUB-DEBUG] Resub details - Tier: {subTier}, Months: {cumulativeMonths}, Streak: {streakMonths}");

            // Parse the chat message if available
            var subMessage = "";
            if (eventData.TryGetProperty("message", out var messageProp))
            {
                if (messageProp.TryGetProperty("text", out var textProp))
                {
                    subMessage = textProp.GetString() ?? "";
                    Debug.WriteLine($"TwitchEventSub: [RESUB-DEBUG] Resub message: '{subMessage}'");
                }
            }

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = $"resubscribed for {cumulativeMonths} months!" + (string.IsNullOrEmpty(subMessage) ? "" : $" \"{subMessage}\""),
                SubTier = subTier,
                GiftCount = 1,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [RESUB-SUCCESS] {userName} resubscribed for {cumulativeMonths} months (Tier {subTier})");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [RESUB-EXCEPTION] Error handling resubscription: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles gift subscription notifications from chat
    /// </summary>
    private Task HandleGiftSubscriptionNotification(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [GIFT-SUB-DEBUG] Processing gift subscription notification...");

            // Parse username (gifter)
            var userName = "Anonymous";
            if (eventData.TryGetProperty("chatter_user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Anonymous";
                Debug.WriteLine($"TwitchEventSub: [GIFT-SUB-DEBUG] Extracted gifter username: '{userName}'");
            }

            // Parse gift subscription data
            var subTier = SubTier.Tier1;
            var giftCount = 1;

            // Check for single gift sub
            if (eventData.TryGetProperty("sub_gift", out var subGiftProp))
            {
                if (subGiftProp.TryGetProperty("sub_plan", out var planProp))
                {
                    var plan = planProp.GetString();
                    subTier = plan switch
                    {
                        "1000" => SubTier.Tier1,
                        "2000" => SubTier.Tier2,
                        "3000" => SubTier.Tier3,
                        "Prime" => SubTier.Prime,
                        _ => SubTier.Tier1
                    };
                }
                giftCount = 1;
                Debug.WriteLine($"TwitchEventSub: [GIFT-SUB-DEBUG] Single gift sub - Tier: {subTier}");
            }
            // Check for community gift subs
            else if (eventData.TryGetProperty("community_sub_gift", out var communityGiftProp))
            {
                if (communityGiftProp.TryGetProperty("sub_plan", out var planProp))
                {
                    var plan = planProp.GetString();
                    subTier = plan switch
                    {
                        "1000" => SubTier.Tier1,
                        "2000" => SubTier.Tier2,
                        "3000" => SubTier.Tier3,
                        "Prime" => SubTier.Prime,
                        _ => SubTier.Tier1
                    };
                }

                if (communityGiftProp.TryGetProperty("count", out var countProp))
                {
                    giftCount = countProp.GetInt32();
                }
                Debug.WriteLine($"TwitchEventSub: [GIFT-SUB-DEBUG] Community gift subs - Tier: {subTier}, Count: {giftCount}");
            }

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = $"gifted {giftCount} subscription{(giftCount > 1 ? "s" : "")}!",
                SubTier = subTier,
                GiftCount = giftCount,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [GIFT-SUB-SUCCESS] {userName} gifted {giftCount} x Tier {subTier} subs");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [GIFT-SUB-EXCEPTION] Error handling gift subscription: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles cheer/bits events
    /// </summary>
    private Task HandleCheerEvent(JsonElement eventData)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: [BITS-DEBUG] Processing cheer/bits event...");
            Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Raw event data: {eventData}");

            // Parse username with enhanced debugging
            var userName = "Anonymous";
            if (eventData.TryGetProperty("user_name", out var userNameProp))
            {
                userName = userNameProp.GetString() ?? "Anonymous";
                Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Extracted username: '{userName}'");
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: [BITS-WARNING] No 'user_name' property found in event data");
            }

            // Parse bits amount with enhanced debugging
            var bits = 0;
            if (eventData.TryGetProperty("bits", out var bitsProp))
            {
                if (bitsProp.ValueKind == JsonValueKind.Number)
                {
                    bits = bitsProp.GetInt32();
                    Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Extracted bits amount: {bits}");
                }
                else
                {
                    Debug.WriteLine($"TwitchEventSub: [BITS-WARNING] Bits property is not a number: {bitsProp.ValueKind}");
                }
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: [BITS-WARNING] No 'bits' property found in event data");
            }

            // Parse message with enhanced debugging
            var message = "";
            if (eventData.TryGetProperty("message", out var messageProp))
            {
                message = messageProp.GetString() ?? "";
                Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Extracted message: '{message}'");
            }
            else
            {
                Debug.WriteLine("TwitchEventSub: [BITS-INFO] No message property found in event data");
            }

            // Validate bits amount
            if (bits <= 0)
            {
                Debug.WriteLine($"TwitchEventSub: [BITS-ERROR] Invalid bits amount: {bits}");
                return Task.CompletedTask;
            }

            var eventArgs = new TwitchEventArgs
            {
                Username = userName,
                Message = $"cheered {bits} bits! {message}",
                SubTier = SubTier.Tier1,
                GiftCount = 0,
                Bits = bits,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: [BITS-SUCCESS] {userName} cheered {bits} bits!");
            Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Created event args - Username: '{eventArgs.Username}', Bits: {eventArgs.Bits}, Message: '{eventArgs.Message}'");
            Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] Firing BitsReceived event...");

            BitsReceived?.Invoke(this, eventArgs);

            Debug.WriteLine($"TwitchEventSub: [BITS-DEBUG] BitsReceived event fired successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: [BITS-EXCEPTION] Error handling cheer: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: [BITS-EXCEPTION] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"TwitchEventSub: [BITS-EXCEPTION] Event data: {eventData}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles follow events
    /// </summary>
    private Task HandleFollowEvent(JsonElement eventData)
    {
        try
        {
            var userName = eventData.TryGetProperty("user_name", out var userNameProp) ? userNameProp.GetString() : "Unknown";

            var eventArgs = new TwitchEventArgs
            {
                Username = userName ?? "Unknown",
                Message = "followed the channel!",
                SubTier = SubTier.Tier1,
                GiftCount = 0,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };

            Debug.WriteLine($"TwitchEventSub: {userName} followed!");
            FollowReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling follow: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles reconnect messages from Twitch
    /// </summary>
    private async Task HandleReconnectMessage(JsonElement root)
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: Handling reconnect request");

            // In a full implementation, you'd extract the new WebSocket URL
            // and reconnect to that. For now, we'll just reconnect to the same URL.

            await DisconnectAsync();
            await Task.Delay(1000); // Brief delay before reconnecting
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling reconnect: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
        _cancellationTokenSource?.Dispose();
        _webSocket?.Dispose();
        _httpClient?.Dispose();
    }
}