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
            ConnectionStatusChanged?.Invoke(this, "?? Connecting to Twitch EventSub...");
            
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
            
            ConnectionStatusChanged?.Invoke(this, "? Connected to WebSocket, waiting for session...");
            
            // Wait a bit to see if we get a welcome message
            await Task.Delay(5000); // 5 second timeout
            
            if (string.IsNullOrEmpty(_sessionId))
            {
                Debug.WriteLine("TwitchEventSub: ERROR - No session ID received after 5 seconds");
                ConnectionStatusChanged?.Invoke(this, "? Failed to establish session with Twitch");
                return false;
            }
            
            Debug.WriteLine($"TwitchEventSub: SUCCESS - Session established: {_sessionId}");
            ConnectionStatusChanged?.Invoke(this, "? Connected to Twitch EventSub");
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
            ConnectionStatusChanged?.Invoke(this, "?? Disconnected from Twitch EventSub");
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
                ConnectionStatusChanged?.Invoke(this, $"?? Ready! Subscribed to {_subscribedEvents.Count} event types");
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
        if (string.IsNullOrEmpty(_sessionId) || !_twitchClient.IsAuthenticated)
        {
            Debug.WriteLine("TwitchEventSub: Cannot setup subscriptions - missing session or auth");
            Debug.WriteLine($"TwitchEventSub: Session ID: {_sessionId}");
            Debug.WriteLine($"TwitchEventSub: Authenticated: {_twitchClient.IsAuthenticated}");
            return;
        }
        
        try
        {
            // Get the user ID for the authenticated user
            Debug.WriteLine("TwitchEventSub: Getting user ID for authenticated user...");
            var userId = await GetUserIdAsync();
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("TwitchEventSub: ERROR - Could not get user ID");
                ConnectionStatusChanged?.Invoke(this, "? Could not get user ID for subscriptions");
                return;
            }
            
            Debug.WriteLine($"TwitchEventSub: Setting up subscriptions for user ID: {userId}");
            
            var subscriptionAttempts = new(string, object)[]
            {
                ("channel.subscribe", new { broadcaster_user_id = userId }),
                ("channel.subscription.gift", new { broadcaster_user_id = userId }),
                ("channel.cheer", new { broadcaster_user_id = userId }),
                ("channel.follow", new { broadcaster_user_id = userId, moderator_user_id = userId })
            };
            
            foreach (var subscription in subscriptionAttempts)
            {
                Debug.WriteLine($"TwitchEventSub: Attempting to subscribe to {subscription.Item1}...");
                await SubscribeToEvent(subscription.Item1, subscription.Item2);
            }
            
            Debug.WriteLine($"TwitchEventSub: Subscription setup complete. Subscribed events: {_subscribedEvents.Count}");
            
            if (_subscribedEvents.Count == 0)
            {
                Debug.WriteLine("TwitchEventSub: WARNING - No events were successfully subscribed!");
                ConnectionStatusChanged?.Invoke(this, "?? Connected but failed to subscribe to events");
            }
            else
            {
                Debug.WriteLine($"TwitchEventSub: SUCCESS - Subscribed to: {string.Join(", ", _subscribedEvents)}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error setting up subscriptions: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: Subscription exception: {ex}");
            ConnectionStatusChanged?.Invoke(this, $"? Subscription error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets the user ID for the authenticated user
    /// </summary>
    private async Task<string?> GetUserIdAsync()
    {
        try
        {
            Debug.WriteLine("TwitchEventSub: Making API call to get user info...");
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _twitchClient.AccessToken);
            request.Headers.Add("Client-Id", _twitchClient.GetClientId());
            
            Debug.WriteLine($"TwitchEventSub: Request headers - Bearer token: {(_twitchClient.AccessToken?.Length > 10 ? _twitchClient.AccessToken.Substring(0, 10) + "..." : _twitchClient.AccessToken ?? "null")}");
            Debug.WriteLine($"TwitchEventSub: Request headers - Client-Id: {_twitchClient.GetClientId()}");
            
            var response = await _httpClient.SendAsync(request);
            
            Debug.WriteLine($"TwitchEventSub: User API response: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: User API response content: {content}");
                
                var userResponse = JsonSerializer.Deserialize<TwitchUsersResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                
                var userId = userResponse?.Data?.FirstOrDefault()?.Id;
                Debug.WriteLine($"TwitchEventSub: Extracted user ID: {userId}");
                return userId;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: User API error: {response.StatusCode} - {errorContent}");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error getting user ID: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: GetUserIdAsync exception: {ex}");
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
            Debug.WriteLine($"TwitchEventSub: Creating subscription for {eventType}...");
            
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
            
            Debug.WriteLine($"TwitchEventSub: Subscription JSON for {eventType}: {json}");
            
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _twitchClient.AccessToken);
            request.Headers.Add("Client-Id", _twitchClient.GetClientId());
            
            Debug.WriteLine($"TwitchEventSub: Sending subscription request to Twitch API...");
            var response = await _httpClient.SendAsync(request);
            
            Debug.WriteLine($"TwitchEventSub: Subscription response for {eventType}: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: Subscription success response: {responseContent}");
                
                _subscribedEvents.Add(eventType);
                Debug.WriteLine($"TwitchEventSub: Successfully subscribed to {eventType}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"TwitchEventSub: Subscription FAILED for {eventType}: {response.StatusCode} - {errorContent}");
                
                // Try to parse the error for more details
                try
                {
                    var errorDoc = JsonDocument.Parse(errorContent);
                    if (errorDoc.RootElement.TryGetProperty("message", out var message))
                    {
                        Debug.WriteLine($"TwitchEventSub: Error message: {message.GetString()}");
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Exception subscribing to {eventType}: {ex.Message}");
            Debug.WriteLine($"TwitchEventSub: Subscription exception details: {ex}");
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
                    await HandleSubscriptionGiftEvent(eventData);
                    break;
                    
                case "channel.cheer":
                    await HandleCheerEvent(eventData);
                    break;
                    
                case "channel.follow":
                    await HandleFollowEvent(eventData);
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
    private async Task HandleSubscriptionEvent(JsonElement eventData, bool isGift)
    {
        try
        {
            var userName = eventData.TryGetProperty("user_name", out var userNameProp) ? userNameProp.GetString() : "Unknown";
            var tier = eventData.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() : "1000";
            
            var subTier = tier switch
            {
                "1000" => SubTier.Tier1,
                "2000" => SubTier.Tier2,
                "3000" => SubTier.Tier3,
                "Prime" => SubTier.Prime,
                _ => SubTier.Tier1
            };
            
            var eventArgs = new TwitchEventArgs
            {
                Username = userName ?? "Unknown",
                Message = isGift ? "gifted a subscription!" : "subscribed!",
                SubTier = subTier,
                GiftCount = 1,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };
            
            Debug.WriteLine($"TwitchEventSub: {userName} {eventArgs.Message} (Tier {subTier})");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling subscription: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles subscription gift events
    /// </summary>
    private async Task HandleSubscriptionGiftEvent(JsonElement eventData)
    {
        try
        {
            var userName = eventData.TryGetProperty("user_name", out var userNameProp) ? userNameProp.GetString() : "Anonymous";
            var tier = eventData.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() : "1000";
            var total = eventData.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : 1;
            
            var subTier = tier switch
            {
                "1000" => SubTier.Tier1,
                "2000" => SubTier.Tier2,
                "3000" => SubTier.Tier3,
                _ => SubTier.Tier1
            };
            
            var eventArgs = new TwitchEventArgs
            {
                Username = userName ?? "Anonymous",
                Message = $"gifted {total} subscription{(total > 1 ? "s" : "")}!",
                SubTier = subTier,
                GiftCount = total,
                Bits = 0,
                Timestamp = DateTime.UtcNow
            };
            
            Debug.WriteLine($"TwitchEventSub: {userName} gifted {total} x Tier {subTier} subs!");
            SubscriptionReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling gift subscription: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles cheer/bits events
    /// </summary>
    private async Task HandleCheerEvent(JsonElement eventData)
    {
        try
        {
            var userName = eventData.TryGetProperty("user_name", out var userNameProp) ? userNameProp.GetString() : "Anonymous";
            var bits = eventData.TryGetProperty("bits", out var bitsProp) ? bitsProp.GetInt32() : 0;
            var message = eventData.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : "";
            
            var eventArgs = new TwitchEventArgs
            {
                Username = userName ?? "Anonymous",
                Message = $"cheered {bits} bits! {message}",
                SubTier = SubTier.Tier1,
                GiftCount = 0,
                Bits = bits,
                Timestamp = DateTime.UtcNow
            };
            
            Debug.WriteLine($"TwitchEventSub: {userName} cheered {bits} bits!");
            BitsReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TwitchEventSub: Error handling cheer: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles follow events
    /// </summary>
    private async Task HandleFollowEvent(JsonElement eventData)
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