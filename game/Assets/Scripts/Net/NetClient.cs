using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetClient : MonoBehaviour
{
    #region Constants
    private const string ContentTypeJson = "application/json";
    private const string c_PrefKeyPlayerId = "mp_player_id";
    private const string c_PrefKeyToken = "mp_token";
    private const string c_PrefKeySessionId = "mp_session_id";
    #endregion

    #region Private Fields
    [SerializeField] private ServerConfig m_ServerConfig;
    [SerializeField] private string m_DefaultSessionId = "default";
    [SerializeField] private bool m_EnableSignalRLogging = true;

    private Coroutine pollRoutine;
    private float m_PollInterval = 0.2f; // Default polling interval
    private float m_BasePollInterval = 0.2f; // Base polling interval (for adaptive polling)
    private int m_LastReceivedVersion = 0; // Track last received version for smart polling
    private int m_UnchangedStateCount = 0; // Count consecutive unchanged states

    // SignalR state
    private bool m_IsSignalRConnected = false;
    private bool m_HasReceivedInitialState = false;
    private string m_SelectedCharacterType = "lancer";
    private GameStateSnapshot m_LatestGameState;
    private int m_CurrentSectionId = -1; // Track current section ID to detect changes (-1 = no section)
    #endregion

    #region Events
    /// <summary>
    /// Event fired when game state is received from SignalR.
    /// </summary>
    public event Action<GameStateSnapshot> OnGameStateReceived;

    /// <summary>
    /// Event fired when a player joins the session.
    /// </summary>
    public event Action<string, string> OnPlayerJoined; // playerId, characterType

    /// <summary>
    /// Event fired when a player leaves the session.
    /// </summary>
    public event Action<string> OnPlayerLeft; // playerId

    /// <summary>
    /// Event fired when SignalR connection state changes.
    /// </summary>
    public event Action<bool> OnSignalRConnectionChanged;
    #endregion

    #region Public Properties
    public static NetClient Instance { get; private set; }

    public Guid PlayerId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string SessionId { get; private set; } = "default";
    public bool IsConnected => PlayerId != Guid.Empty && !string.IsNullOrEmpty(Token);

    /// <summary>
    /// Is SignalR currently connected.
    /// </summary>
    public bool IsSignalRConnected => m_IsSignalRConnected;

    /// <summary>
    /// Has received initial state from SignalR.
    /// </summary>
    public bool HasReceivedInitialState => m_HasReceivedInitialState;

    /// <summary>
    /// Selected character type for current session.
    /// </summary>
    public string SelectedCharacterType
    {
        get => m_SelectedCharacterType;
        set => m_SelectedCharacterType = value;
    }

    /// <summary>
    /// Latest game state snapshot from server.
    /// </summary>
    public GameStateSnapshot LatestGameState => m_LatestGameState;

    /// <summary>
    /// Base URL của server. Lấy từ ServerConfig nếu có, fallback về default.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            if (m_ServerConfig != null)
            {
                return m_ServerConfig.GetBaseUrl();
            }
            return "http://localhost:5220";
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load ServerConfig from Resources if not assigned
        if (m_ServerConfig == null)
        {
            LoadServerConfigFromResources();
        }

        // Log current server URL for debugging
        if (m_ServerConfig != null)
        {
            Debug.Log($"[NetClient] ServerConfig loaded: {m_ServerConfig.name}, BaseUrl: {BaseUrl}");
        }
        else
        {
            Debug.LogWarning($"[NetClient] ServerConfig not found! Using fallback URL: {BaseUrl}");
        }

        // Ensure KillReporter component exists
        if (GetComponent<KillReporter>() == null)
        {
            gameObject.AddComponent<KillReporter>();
            Debug.Log("[NetClient] Added KillReporter component");
        }

        // Clear any old saved session to avoid stale data
        ClearSavedSession();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Tự động load ServerConfig từ Resources folder nếu chưa được assign.
    /// </summary>
    private void LoadServerConfigFromResources()
    {
        // Try to load from Resources/Config/ServerConfig
        var config = Resources.Load<ServerConfig>("Config/ServerConfig");
        if (config != null)
        {
            m_ServerConfig = config;
            Debug.Log("[NetClient] Loaded ServerConfig from Resources/Config/ServerConfig");
            return;
        }

        // Try alternative path: Resources/ServerConfig
        config = Resources.Load<ServerConfig>("ServerConfig");
        if (config != null)
        {
            m_ServerConfig = config;
            Debug.Log("[NetClient] Loaded ServerConfig from Resources/ServerConfig");
            return;
        }

        Debug.LogWarning("[NetClient] ServerConfig not found in Resources. Please either:\n" +
                        "1. Assign ServerConfig asset to NetClient in Inspector, OR\n" +
                        "2. Place ServerConfig asset in Resources/Config/ServerConfig or Resources/ServerConfig");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Cấu hình base URL. Nếu có ServerConfig, sẽ update ServerConfig.
    /// Giữ lại method này để backward compatibility.
    /// </summary>
    public void ConfigureBaseUrl(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            if (m_ServerConfig != null)
            {
                m_ServerConfig.BaseUrl = baseUrl;
            }
            else
            {
                // Fallback: tạo ServerConfig nếu chưa có
                Debug.LogWarning("[NetClient] ServerConfig not assigned. Consider assigning ServerConfig asset in Inspector.");
            }
        }
    }

    public IEnumerator RegisterPlayer(string playerName, Action onSuccess, Action<string> onError)
    {
        var payload = JsonUtility.ToJson(new RegisterRequest { playerName = playerName });
        using var req = BuildPost("/auth/register", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        RegisterResponse result;
        try
        {
            result = JsonUtility.FromJson<RegisterResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid register response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.playerId) || !Guid.TryParse(result.playerId, out var pid))
        {
            onError?.Invoke("Missing/invalid PlayerId in register response");
            yield break;
        }

        PlayerId = pid;
        Token = result.token;
        SessionId = string.IsNullOrEmpty(result.sessionId) ? m_DefaultSessionId : result.sessionId;
        // Session persistence disabled - server handles player data
        onSuccess?.Invoke();
    }

    public IEnumerator GetPlayerProfile(Action<PlayerProfileResponse> onSuccess, Action<string> onError)
    {
        if (PlayerId == Guid.Empty)
        {
            onError?.Invoke("Player not logged in");
            yield break;
        }

        var url = $"{BaseUrl}/auth/profile/{PlayerId}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        // Debug: log raw JSON response
        Debug.Log($"[NetClient] Profile response JSON: {req.downloadHandler.text}");

        PlayerProfileResponse result;
        try
        {
            result = JsonUtility.FromJson<PlayerProfileResponse>(req.downloadHandler.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetClient] Failed to parse profile JSON: {ex.Message}");
            onError?.Invoke($"Invalid profile response JSON: {ex.Message}");
            yield break;
        }

        if (result == null)
        {
            onError?.Invoke("Null profile response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Downloads avatar image from server and converts it to Texture2D.
    /// </summary>
    public IEnumerator DownloadAvatarImage(string avatarPath, Action<Texture2D> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(avatarPath))
        {
            onError?.Invoke("Avatar path is empty");
            yield break;
        }

        // Build full URL - avatarPath should be relative (e.g., "/avatars/xxx.jpg" or "avatars/xxx.jpg")
        string url;
        if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
        {
            url = avatarPath; // Already a full URL
        }
        else
        {
            // Ensure path starts with "/" for proper URL construction
            var path = avatarPath.StartsWith("/") ? avatarPath : $"/{avatarPath}";
            url = $"{BaseUrl}{path}";
        }

        Debug.Log($"[NetClient] Downloading avatar from: {url}");

        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        var texture = DownloadHandlerTexture.GetContent(req);
        if (texture == null)
        {
            onError?.Invoke("Failed to download texture");
            yield break;
        }

        Debug.Log($"[NetClient] Avatar downloaded successfully: {texture.width}x{texture.height}");
        onSuccess?.Invoke(texture);
    }

    public IEnumerator LoginPlayer(string playerName, string password, Action onSuccess, Action<string> onError)
    {
        var payload = JsonUtility.ToJson(new LoginRequest { playerName = playerName, password = password });
        using var req = BuildPost("/auth/login", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        RegisterResponse result;
        try
        {
            result = JsonUtility.FromJson<RegisterResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid login response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.playerId) || !Guid.TryParse(result.playerId, out var pid))
        {
            onError?.Invoke("Missing/invalid PlayerId in login response");
            yield break;
        }

        PlayerId = pid;
        Token = result.token;
        SessionId = string.IsNullOrEmpty(result.sessionId) ? m_DefaultSessionId : result.sessionId;
        onSuccess?.Invoke();
    }

    public IEnumerator JoinSession(string playerName, Action onSuccess, Action<string> onError)
    {
        if (PlayerId == Guid.Empty)
        {
            onError?.Invoke("Player not registered");
            yield break;
        }

        var payload = JsonUtility.ToJson(new JoinSessionRequest
        {
            playerId = PlayerId.ToString(),
            playerName = playerName,
            sessionId = SessionId,
            token = Token
        });

        using var req = BuildPost("/sessions/join", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        var result = JsonUtility.FromJson<JoinSessionResponse>(req.downloadHandler.text);
        SessionId = string.IsNullOrEmpty(result.sessionId) ? SessionId : result.sessionId;
        onSuccess?.Invoke();
    }

    public IEnumerator SendInput(InputPayload input, Action<string> onError)
    {
        if (!IsConnected) yield break;

        input.playerId = PlayerId.ToString();
        input.sessionId = SessionId;
        input.token = Token;

        var payload = JsonUtility.ToJson(input);
        using var req = BuildPost("/sessions/input", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
        }
    }

    public void StartPolling(int? sinceVersion, float intervalSeconds, Action<StateResponse> onState, Action<string> onError)
    {
        StopPolling();
        pollRoutine = StartCoroutine(PollLoop(sinceVersion, intervalSeconds, onState, onError));
    }

    public void SetPollInterval(float intervalSeconds)
    {
        if (intervalSeconds > 0f)
        {
            m_PollInterval = intervalSeconds;
            m_BasePollInterval = intervalSeconds; // Update base interval too
        }
    }

    public void StopPolling()
    {
        if (pollRoutine != null)
        {
            StopCoroutine(pollRoutine);
            pollRoutine = null;
        }
    }

    /// <summary>
    /// Clears the current session data (playerId, token, sessionId).
    /// Also clears the saved session from PlayerPrefs.
    /// Call after disconnecting or logging out.
    /// </summary>
    public void ClearSession()
    {
        PlayerId = Guid.Empty;
        Token = string.Empty;
        SessionId = m_DefaultSessionId;
        ClearSavedSession();
    }

    #region SignalR Methods
    /// <summary>
    /// Connect to SignalR hub and join session.
    /// Called AFTER loading screen completes resource validation.
    /// </summary>
    public IEnumerator ConnectSignalRAsync(string sessionId, Action onConnected, Action<string> onError)
    {
        if (m_IsSignalRConnected)
        {
            Debug.LogWarning("[NetClient] Already connected to SignalR");
            onConnected?.Invoke();
            yield break;
        }

        if (m_EnableSignalRLogging)
        {
            Debug.Log($"[NetClient] Connecting SignalR to session {sessionId}...");
        }

        // Since Unity doesn't have native SignalR client, we simulate it with WebSocket polling
        // In production, use SignalR Unity package or implement WebSocket client
        // For now, we use a faster polling approach as SignalR simulation

        // Step 1: Signal ready to server via REST
        bool ready = false;
        string readyError = null;

        yield return SignalReady(sessionId,
            () => ready = true,
            (err) => readyError = err);

        if (!ready)
        {
            onError?.Invoke(readyError ?? "Failed to signal ready");
            yield break;
        }

        // Step 2: Start high-frequency polling (simulating SignalR)
        m_IsSignalRConnected = true;
        m_HasReceivedInitialState = false;

        // Start SignalR-like polling at higher frequency
        StartSignalRPolling();

        // Wait for first state
        float timeout = 5f;
        while (!m_HasReceivedInitialState && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!m_HasReceivedInitialState)
        {
            Debug.LogWarning("[NetClient] Timed out waiting for initial state");
        }

        if (m_EnableSignalRLogging)
        {
            Debug.Log("[NetClient] SignalR connected!");
        }

        OnSignalRConnectionChanged?.Invoke(true);
        onConnected?.Invoke();
    }

    /// <summary>
    /// Disconnect from SignalR hub.
    /// </summary>
    public void DisconnectSignalR()
    {
        if (!m_IsSignalRConnected)
            return;

        StopPolling();
        m_IsSignalRConnected = false;
        m_HasReceivedInitialState = false;
        m_CurrentSectionId = -1;

        OnSignalRConnectionChanged?.Invoke(false);

        if (m_EnableSignalRLogging)
        {
            Debug.Log("[NetClient] SignalR disconnected");
        }
    }

    /// <summary>
    /// Send input via SignalR (high frequency).
    /// </summary>
    public void SendInputViaSignalR(SignalRInputPayload input)
    {
        if (!m_IsSignalRConnected)
            return;

        input.playerId = PlayerId.ToString();
        input.sessionId = SessionId;

        // Send via REST (simulating SignalR)
        StartCoroutine(SendInputCoroutine(input));
    }

    /// <summary>
    /// Get session metadata from REST API.
    /// </summary>
    public IEnumerator GetSessionMetadata(string sessionId, Action<SessionMetadataResponse> onSuccess, Action<string> onError)
    {
        var url = $"{BaseUrl}/sessions/{sessionId}/metadata";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        try
        {
            var result = JsonUtility.FromJson<SessionMetadataResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(result);
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Failed to parse metadata: {ex.Message}");
        }
    }
    #endregion

    #region SignalR Private Methods
    private void StartSignalRPolling()
    {
        // Use faster polling interval to simulate SignalR (50ms = 20Hz)
        float signalRInterval = 0.05f;
        StartPolling(null, signalRInterval, OnSignalRStateReceived, OnSignalRError);
    }

    private void OnSignalRStateReceived(StateResponse state)
    {
        if (!m_HasReceivedInitialState)
        {
            m_HasReceivedInitialState = true;
            if (m_EnableSignalRLogging)
            {
                Debug.Log("[NetClient] Received initial state from server");
            }
        }

        // Track section changes (no loading screen, just update tracking)

        if (!string.IsNullOrEmpty(state.status))
        {
            if (state.status == "Completed")
            {
                Debug.LogWarning("[NetClient] Session COMPLETED!");
                // Trigger game completion handler
                if (GameCompletionHandler.Instance != null)
                {
                    GameCompletionHandler.Instance.HandleGameCompleted();
                }
                else
                {
                    var handler = new GameObject("GameCompletionHandler");
                    handler.AddComponent<GameCompletionHandler>();
                    GameCompletionHandler.Instance.HandleGameCompleted();
                }
            }
            else if (state.status == "Failed")
            {
                Debug.LogWarning("[NetClient] Session FAILED!");
                if (GameCompletionHandler.Instance != null)
                {
                    GameCompletionHandler.Instance.HandleGameFailed();
                }
                else
                {
                    var handler = new GameObject("GameCompletionHandler");
                    handler.AddComponent<GameCompletionHandler>();
                    GameCompletionHandler.Instance.HandleGameFailed();
                }
            }
        }

        if (state.status == "InProgress")
        {
            // Simply update current section ID when it changes
            if (state.currentSectionId >= 0 && m_CurrentSectionId != state.currentSectionId)
            {
                if (m_CurrentSectionId >= 0)
                {
                    Debug.Log($"[NetClient] Section changed: {m_CurrentSectionId} -> {state.currentSectionId} ({state.sectionName})");
                }
                m_CurrentSectionId = state.currentSectionId;
            }
            else if (state.currentSectionId < 0)
            {
                m_CurrentSectionId = -1;
            }
        }

        m_LatestGameState = ConvertToGameStateSnapshot(state);
        OnGameStateReceived?.Invoke(m_LatestGameState);
    }

    private void OnSignalRError(string error)
    {
        Debug.LogWarning($"[NetClient] SignalR polling error: {error}");
    }


    private GameStateSnapshot ConvertToGameStateSnapshot(StateResponse state)
    {
        var snapshot = new GameStateSnapshot
        {
            sequence = state.version,
            serverTime = Time.time, // Use client time for now
            confirmedInputSequence = 0
        };

        // Convert players
        if (state.players != null)
        {
            foreach (var p in state.players)
            {
                snapshot.players.Add(new SignalRPlayerSnapshot
                {
                    id = p.id,
                    name = p.name,
                    characterType = "lancer", // Default, server should provide
                    x = p.x,
                    y = p.y,
                    hp = p.hp,
                    maxHp = p.maxHp,
                    level = p.level,
                    status = p.hp <= 0 ? "dead" : "idle",
                    lastConfirmedInputSequence = p.sequence
                });
            }
        }

        // Convert enemies
        if (state.enemies != null)
        {
            foreach (var e in state.enemies)
            {
                snapshot.enemies.Add(new SignalREnemySnapshot
                {
                    id = e.id,
                    typeId = e.typeId,
                    x = e.x,
                    y = e.y,
                    hp = e.hp,
                    maxHp = e.maxHp,
                    status = e.status
                });
            }
        }

        return snapshot;
    }

    private IEnumerator SignalReady(string sessionId, Action onSuccess, Action<string> onError)
    {
        var payload = JsonUtility.ToJson(new ReadyRequestPayload
        {
            playerId = PlayerId.ToString(),
            characterType = m_SelectedCharacterType
        });

        using var req = BuildPost($"/sessions/{sessionId}/ready", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        onSuccess?.Invoke();
    }

    private IEnumerator SendInputCoroutine(SignalRInputPayload input)
    {
        // Convert to regular InputPayload for existing endpoint
        var legacyInput = new InputPayload
        {
            playerId = input.playerId,
            sessionId = input.sessionId,
            moveX = input.moveX,
            moveY = input.moveY,
            aimX = 0,
            aimY = 0,
            attack = input.attack,
            shoot = input.skill,
            sequence = input.sequence,
            token = Token
        };

        yield return SendInput(legacyInput, (err) =>
        {
            if (m_EnableSignalRLogging)
            {
                Debug.LogWarning($"[NetClient] SendInput error: {err}");
            }
        });
    }
    #endregion
    #endregion

    #region Private Methods
    private void LoadSession()
    {
        var savedId = PlayerPrefs.GetString(c_PrefKeyPlayerId, "");
        var savedToken = PlayerPrefs.GetString(c_PrefKeyToken, "");
        var savedSession = PlayerPrefs.GetString(c_PrefKeySessionId, "");

        if (Guid.TryParse(savedId, out var pid) && !string.IsNullOrEmpty(savedToken))
        {
            PlayerId = pid;
            Token = savedToken;
            SessionId = string.IsNullOrEmpty(savedSession) ? m_DefaultSessionId : savedSession;
            Debug.Log($"[NetClient] Loaded saved session: PlayerId={PlayerId}");
        }
    }

    private void SaveSession()
    {
        PlayerPrefs.SetString(c_PrefKeyPlayerId, PlayerId.ToString());
        PlayerPrefs.SetString(c_PrefKeyToken, Token);
        PlayerPrefs.SetString(c_PrefKeySessionId, SessionId);
        PlayerPrefs.Save();
        Debug.Log($"[NetClient] Saved session: PlayerId={PlayerId}");
    }

    private void ClearSavedSession()
    {
        PlayerPrefs.DeleteKey(c_PrefKeyPlayerId);
        PlayerPrefs.DeleteKey(c_PrefKeyToken);
        PlayerPrefs.DeleteKey(c_PrefKeySessionId);
        PlayerPrefs.Save();
    }

    private IEnumerator PollLoop(int? sinceVersion, float intervalSeconds, Action<StateResponse> onState, Action<string> onError)
    {
        int? version = sinceVersion ?? m_LastReceivedVersion;
        float currentInterval = intervalSeconds;

        while (true)
        {
            yield return PollState(version, state =>
            {
                if (state.version > 0)
                {
                    // Smart polling: track version changes
                    bool versionChanged = state.version != m_LastReceivedVersion;

                    if (versionChanged)
                    {
                        // State changed - reset adaptive polling
                        m_LastReceivedVersion = state.version;
                        m_UnchangedStateCount = 0;
                        currentInterval = m_BasePollInterval; // Reset to base interval
                        version = state.version;
                    }
                    else
                    {
                        // State unchanged - increase polling interval (adaptive polling)
                        m_UnchangedStateCount++;
                        // Gradually increase interval up to 1 second max
                        currentInterval = Mathf.Min(m_BasePollInterval * (1f + m_UnchangedStateCount * 0.1f), 1f);
                    }

                    // Check session status for game completion/failure
                    if (!string.IsNullOrEmpty(state.status))
                    {
                        if (state.status == "Completed")
                        {
                            Debug.Log("[NetClient] Session completed! All sections finished.");
                            // Trigger game completion handler
                            if (GameCompletionHandler.Instance != null)
                            {
                                GameCompletionHandler.Instance.HandleGameCompleted();
                            }
                            else
                            {
                                Debug.LogWarning("[NetClient] GameCompletionHandler not found! Creating one...");
                                var handler = new GameObject("GameCompletionHandler");
                                handler.AddComponent<GameCompletionHandler>();
                                GameCompletionHandler.Instance.HandleGameCompleted();
                            }
                        }
                        else if (state.status == "Failed")
                        {
                            Debug.Log("[NetClient] Session failed! All players died.");
                            // Trigger game failure handler
                            if (GameCompletionHandler.Instance != null)
                            {
                                GameCompletionHandler.Instance.HandleGameFailed();
                            }
                            else
                            {
                                Debug.LogWarning("[NetClient] GameCompletionHandler not found! Creating one...");
                                var handler = new GameObject("GameCompletionHandler");
                                handler.AddComponent<GameCompletionHandler>();
                                GameCompletionHandler.Instance.HandleGameFailed();
                            }
                        }
                    }

                    // Only invoke callback if state actually changed
                    if (versionChanged)
                    {
                        onState?.Invoke(state);
                    }
                }
            }, onError);

            yield return new WaitForSeconds(currentInterval);
        }
    }

    private IEnumerator PollState(int? sinceVersion, Action<StateResponse> onState, Action<string> onError)
    {
        var url = $"{BaseUrl}/sessions/{SessionId}/state";
        // Always include sinceVersion to enable server-side optimization
        int versionToSend = sinceVersion ?? m_LastReceivedVersion;
        url += $"?sinceVersion={versionToSend}";

        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        // 204 NoContent means no update (server optimization)
        if (req.responseCode == 204)
        {
            // No state change - callback won't be invoked
            yield break;
        }

        if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
        {
            var state = JsonUtility.FromJson<StateResponse>(req.downloadHandler.text);
            onState?.Invoke(state);
        }
        else if (req.result != UnityWebRequest.Result.Success && req.responseCode != 204)
        {
            // Only log errors, not normal 204 responses
            onError?.Invoke(req.error);
        }
    }

    private UnityWebRequest BuildPost(string path, string payload)
    {
        var req = new UnityWebRequest($"{BaseUrl}{path}", UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", ContentTypeJson);
        return req;
    }

    /// <summary>
    /// Save current progress to database on the server.
    /// </summary>
    public IEnumerator SaveProgress(Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new SaveProgressRequest { playerId = PlayerId.ToString(), token = Token });
        using var req = BuildPost("/sessions/save", payload);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[NetClient] Progress saved to server.");
            onSuccess?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[NetClient] SaveProgress failed: {req.error}");
            onError?.Invoke(req.error);
        }
    }

    /// <summary>
    /// Disconnect from server (saves progress and cleans up).
    /// </summary>
    public IEnumerator Disconnect(Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onSuccess?.Invoke();
            yield break;
        }

        StopPolling();

        var payload = JsonUtility.ToJson(new DisconnectRequest { playerId = PlayerId.ToString(), token = Token });
        using var req = BuildPost("/sessions/disconnect", payload);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[NetClient] Disconnected and progress saved.");
        }
        else
        {
            Debug.LogWarning($"[NetClient] Disconnect failed: {req.error}");
        }

        // Clear local session
        ClearSession();
        onSuccess?.Invoke();
    }

    /// <summary>
    /// Report a kill so the server can grant rewards based on enemy type.
    /// </summary>
    public IEnumerator ReportKill(string enemyTypeId, Action<KillReportResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new KillReportRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            enemyTypeId = enemyTypeId,
            token = Token
        });

        using var req = BuildPost("/sessions/kill", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        KillReportResponse result = null;
        try
        {
            result = JsonUtility.FromJson<KillReportResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid kill report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Report damage taken from enemy so server can update HP authoritatively.
    /// </summary>
    public IEnumerator ReportDamage(int damageAmount, Action<DamageReportResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new DamageReportRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            damageAmount = damageAmount,
            token = Token
        });

        using var req = BuildPost("/sessions/damage", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        DamageReportResponse result = null;
        try
        {
            result = JsonUtility.FromJson<DamageReportResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid damage report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Report damage from player to enemy so server can update enemy HP authoritatively.
    /// </summary>
    public IEnumerator ReportEnemyDamage(System.Guid enemyId, int damageAmount, Action<EnemyDamageResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new EnemyDamageRequest
        {
            playerId = PlayerId.ToString(),
            enemyId = enemyId.ToString(),
            sessionId = SessionId,
            damageAmount = damageAmount,
            token = Token
        });

        using var req = BuildPost("/sessions/enemy-damage", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        EnemyDamageResponse result = null;
        try
        {
            result = JsonUtility.FromJson<EnemyDamageResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid enemy damage report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Request respawn from server (resets position to spawn and sets HP to 50%).
    /// </summary>
    public IEnumerator CreateRoom(Action<CreateRoomResponse> onSuccess, Action<string> onError)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new CreateRoomRequest
        {
            playerId = PlayerId.ToString(),
            token = Token
        });

        using var req = BuildPost("/rooms/create", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        CreateRoomResponse result;
        try
        {
            result = JsonUtility.FromJson<CreateRoomResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid create room response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.roomId))
        {
            onError?.Invoke("Missing/invalid RoomId in create room response");
            yield break;
        }

        SessionId = result.roomId;
        onSuccess?.Invoke(result);
    }

    public IEnumerator JoinRoom(string roomId, Action<JoinRoomResponse> onSuccess, Action<string> onError)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            onError?.Invoke("Room ID is required");
            yield break;
        }

        var payload = JsonUtility.ToJson(new JoinRoomRequest
        {
            playerId = PlayerId.ToString(),
            roomId = roomId,
            token = Token
        });

        using var req = BuildPost("/rooms/join", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        JoinRoomResponse result;
        try
        {
            result = JsonUtility.FromJson<JoinRoomResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid join room response JSON");
            yield break;
        }

        if (result == null || !result.success)
        {
            onError?.Invoke("Failed to join room");
            yield break;
        }

        SessionId = result.roomId;
        onSuccess?.Invoke(result);
    }

    public IEnumerator RequestRespawn(Action<RespawnResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new RespawnRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            token = Token
        });

        using var req = BuildPost("/sessions/respawn", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        RespawnResponse result = null;
        try
        {
            result = JsonUtility.FromJson<RespawnResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid respawn response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Upgrade a skill for the current player.
    /// </summary>
    public IEnumerator UpgradeSkill(string skillId, Action<SkillUpgradeResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new SkillUpgradeRequest
        {
            playerId = PlayerId.ToString(),
            skillId = skillId,
            token = Token
        });

        using var req = BuildPost("/skills/upgrade", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        SkillUpgradeResponse result = null;
        try
        {
            result = JsonUtility.FromJson<SkillUpgradeResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid skill upgrade response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }
    /// <summary>
    /// Get temporary skills for the current player from server.
    /// </summary>
    public IEnumerator GetSkills(Action<GetSkillsResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        string url = $"{BaseUrl}/skills/{PlayerId}";
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", $"Bearer {Token}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        GetSkillsResponse result = null;
        try
        {
            result = JsonUtility.FromJson<GetSkillsResponse>(req.downloadHandler.text);
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Invalid skills response: {ex.Message}");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    #endregion
}

#region DTOs
[Serializable]
public class RegisterRequest
{
    public string playerName;
}

[Serializable]
public class LoginRequest
{
    public string playerName;
    public string password;
}

[Serializable]
public class RegisterResponse
{
    public string playerId;
    public string token;
    public string sessionId;
}

[Serializable]
public class JoinSessionRequest
{
    public string playerId;
    public string playerName;
    public string sessionId;
    public string token;
}

[Serializable]
public class JoinSessionResponse
{
    public string sessionId;
}

[Serializable]
public class SaveProgressRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class DisconnectRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class InputPayload
{
    public string playerId;
    public string sessionId;
    public float moveX;
    public float moveY;
    public float aimX;
    public float aimY;
    public bool attack;
    public bool shoot;
    public int sequence;
    public string token;
}

[Serializable]
public class StateResponse
{
    public string sessionId;
    public int version;
    public string status = "InProgress"; // "InProgress", "Completed", "Failed"
    public int currentSectionId = -1; // Current section ID (-1 = no section, Unity JsonUtility doesn't support nullable)
    public string sectionName; // Current section name (empty string if no section)
    public PlayerSnapshot[] players;
    public EnemySnapshot[] enemies;
    public ProjectileSnapshot[] projectiles;
}

[Serializable]
public class PlayerSnapshot
{
    public string id;
    public string name;
    public string characterType; // "lancer" or "warrious"
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public int sequence;
    public int level;
    public int exp;
    public int expToLevel;
    public int gold;
    // Player stats (synced from database)
    public int damage;
    public float range;
    public float speed;
    public float weaponRange;
    public float knockbackForce;
    public float knockbackTime;
    public float stunTime;
    public float bonusDamagePercent;
    public float damageReductionPercent;
}

[Serializable]
public class EnemySnapshot
{
    public string id;
    public string typeId;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public string status;
}

[Serializable]
public class ProjectileSnapshot
{
    public string id;
    public string ownerId;
    public float x;
    public float y;
    public float dirX;
    public float dirY;
    public float radius;
}

[Serializable]
public class KillReportRequest
{
    public string playerId;
    public string enemyTypeId;
    public string sessionId;
    public string token;
}

[Serializable]
public class KillReportResponse
{
    public bool granted;
    public int level;
    public int exp;
    public int gold;
}

[Serializable]
public class DamageReportRequest
{
    public string playerId;
    public int damageAmount;
    public string sessionId;
    public string token;
}

[Serializable]
public class DamageReportResponse
{
    public bool accepted;
    public int currentHp;
    public int maxHp;
}

[Serializable]
public class EnemyDamageRequest
{
    public string playerId;
    public string enemyId;
    public int damageAmount;
    public string sessionId;
    public string token;
}

[Serializable]
public class EnemyDamageResponse
{
    public bool accepted;
    public int currentHp;
    public int maxHp;
    public bool isDead;
}

[Serializable]
public class RespawnRequest
{
    public string playerId;
    public string sessionId;
    public string token;
}

[Serializable]
public class RespawnResponse
{
    public bool accepted;
    public float x;
    public float y;
    public int currentHp;
    public int maxHp;
}

[Serializable]
public class PlayerProfileResponse
{
    public string playerId;
    public string name;
    public int level;
    public int exp;
    public int gold;
    public string avatarPath;
}

[Serializable]
public class CreateRoomRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class CreateRoomResponse
{
    public string roomId;
}

[Serializable]
public class JoinRoomRequest
{
    public string playerId;
    public string roomId;
    public string token;
}

[Serializable]
public class JoinRoomResponse
{
    public bool success;
    public string roomId;
}

[Serializable]
public class SkillUpgradeRequest
{
    public string playerId;
    public string skillId;
    public string token;
}

[Serializable]
public class SkillUpgradeResponse
{
    public bool success;
    public string skillId;
    public int level;
    public string message;
}

[Serializable]
public class GetSkillsResponse
{
    public List<SkillInfo> skills;
}

[Serializable]
public class SkillInfo
{
    public string skillId;
    public int level;
}

#region SignalR DTOs
[Serializable]
public class SignalRInputPayload
{
    public string playerId;
    public string sessionId;
    public float moveX;
    public float moveY;
    public int sequence;
    public bool attack;
    public bool skill;
    public float timestamp;
}

[Serializable]
public class GameStateSnapshot
{
    public int sequence;
    public float serverTime;
    public int confirmedInputSequence;
    public List<SignalRPlayerSnapshot> players = new List<SignalRPlayerSnapshot>();
    public List<SignalREnemySnapshot> enemies = new List<SignalREnemySnapshot>();
    public List<SignalRProjectileSnapshot> projectiles = new List<SignalRProjectileSnapshot>();
}

[Serializable]
public class SignalRPlayerSnapshot
{
    public string id;
    public string name;
    public string characterType;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public int level;
    public string status;
    public int lastConfirmedInputSequence;
}

[Serializable]
public class SignalREnemySnapshot
{
    public string id;
    public string typeId;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public string status;
}

[Serializable]
public class SignalRProjectileSnapshot
{
    public string id;
    public string ownerId;
    public float x;
    public float y;
    public float velocityX;
    public float velocityY;
}

[Serializable]
public class SessionMetadataResponse
{
    public string sessionId;
    public int playerCount;
    public int version;
    public List<PlayerMetadataInfo> players;
}

[Serializable]
public class PlayerMetadataInfo
{
    public string id;
    public string name;
    public string characterType;
    public int level;
}

[Serializable]
public class ReadyRequestPayload
{
    public string playerId;
    public string characterType;
}

[Serializable]
public class PlayerJoinedEventData
{
    public string playerId;
    public string sessionId;
    public string characterType;
}

[Serializable]
public class PlayerLeftEventData
{
    public string playerId;
    public string sessionId;
}
#endregion
#endregion


