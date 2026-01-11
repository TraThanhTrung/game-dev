using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Applies server state snapshots to the local player GameObject.
/// Attach this to the Player prefab/object in the game scene.
/// Automatically polls state from NetClient when enabled.
/// </summary>
public class ServerStateApplier : MonoBehaviour
{
    #region Constants
    private const float c_DefaultLerpSpeed = 10f;
    private const float c_DefaultPollInterval = 0.2f;
    #endregion

    #region Private Fields
    [Header("State Sync Settings")]
    [SerializeField] private float m_LerpSpeed = c_DefaultLerpSpeed;
    [SerializeField] private float m_PollInterval = c_DefaultPollInterval;
    [SerializeField] private bool m_EnableLogging = true;
    [SerializeField] private bool m_AutoPoll = true;

    [Header("Auto-Save Settings")]
    [SerializeField] private bool m_AutoSave = true;
    [SerializeField] private float m_AutoSaveInterval = 30f; // Save every 30 seconds

    [Header("Dev Mode (for testing without Login scene)")]
    [SerializeField] private bool m_DevModeAutoConnect = false;
    [SerializeField] private string m_DevServerUrl = "http://localhost:5220";
    [SerializeField] private string m_DevPlayerName = "DevPlayer";

    private Vector3 targetPosition;
    private bool hasTargetPosition;
    private int lastLoggedSequence = -1;
    private bool isPolling;
    private bool devModeConnecting;
    private float autoSaveTimer;
    private bool isSavingOnQuit;
    private bool saveCompleted;
    private ExpManager expManager;
    private EnemySpawner enemySpawner;
    private Vector3 lastAppliedPosition;
    private float positionChangeThreshold = 0.01f; // Only apply position if change is significant

    // SmoothDamp for smoother interpolation (better than Lerp for network sync)
    private Vector3 velocity = Vector3.zero;

    // Prediction & Interpolation components
    private ClientPredictor m_Predictor;
    private StateInterpolator m_Interpolator;
    private bool m_UseSignalRMode = false;
    #endregion

    #region Public Properties
    public int CurrentHp { get; private set; }
    public int MaxHp { get; private set; }
    public int Sequence { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Handle dev mode: auto-create NetClient and connect if not already connected
        if (m_DevModeAutoConnect && (NetClient.Instance == null || !NetClient.Instance.IsConnected))
        {
            StartCoroutine(DevModeConnect());
            return;
        }

        expManager = FindObjectOfType<ExpManager>();
        enemySpawner = FindObjectOfType<EnemySpawner>();

        // Load enemy configs if already connected
        if (NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            LoadEnemyConfigsAfterJoin();
        }

        // Load polling settings from config
        if (GameConfigLoader.Instance != null && GameConfigLoader.Instance.Config != null)
        {
            var pollingConfig = GameConfigLoader.Instance.Config.polling;

            // Apply polling interval
            if (NetClient.Instance != null)
            {
                NetClient.Instance.SetPollInterval(pollingConfig.stateIntervalSeconds);
            }

            // Apply lerp speed from config
            m_LerpSpeed = pollingConfig.lerpSpeed;

            // Apply position change threshold from config
            positionChangeThreshold = pollingConfig.positionChangeThreshold;

            if (m_EnableLogging)
            {
                Debug.Log($"[StateApplier] Loaded polling config: interval={pollingConfig.stateIntervalSeconds}s, lerpSpeed={m_LerpSpeed}, threshold={positionChangeThreshold}");
            }
        }

        // Log status on start for debugging
        LogConnectionStatus();
    }

    private void LogConnectionStatus()
    {
        if (!m_EnableLogging) return;

        if (NetClient.Instance == null)
        {
            Debug.LogWarning("[StateApplier] NetClient.Instance is NULL. Did you load Login scene first? Or enable Dev Mode.");
        }
        else if (!NetClient.Instance.IsConnected)
        {
            Debug.LogWarning($"[StateApplier] NetClient not connected. Enable Dev Mode to auto-connect.");
        }
        else
        {
            Debug.Log($"[StateApplier] Ready. NetClient connected. PlayerId={NetClient.Instance.PlayerId}");
        }
    }

    private System.Collections.IEnumerator DevModeConnect()
    {
        if (devModeConnecting) yield break;
        devModeConnecting = true;

        Debug.Log("[StateApplier] DEV MODE: Auto-connecting to server...");

        // Create NetClient if needed
        if (NetClient.Instance == null)
        {
            var go = new GameObject("NetClient");
            go.AddComponent<NetClient>();
            // KillReporter will be added in NetClient.Awake()
            yield return null; // Wait a frame for Awake
        }

        NetClient.Instance.ConfigureBaseUrl(m_DevServerUrl);

        // Register
        bool registered = false;
        string error = null;
        yield return NetClient.Instance.RegisterPlayer(m_DevPlayerName,
            () => registered = true,
            err => error = err);

        if (!registered)
        {
            Debug.LogError($"[StateApplier] DEV MODE: Register failed: {error}");
            devModeConnecting = false;
            yield break;
        }

        Debug.Log($"[StateApplier] DEV MODE: Registered as {m_DevPlayerName}, PlayerId={NetClient.Instance.PlayerId}");

        // Join session
        bool joined = false;
        yield return NetClient.Instance.JoinSession(m_DevPlayerName,
            () => joined = true,
            err => error = err);

        if (!joined)
        {
            Debug.LogError($"[StateApplier] DEV MODE: Join failed: {error}");
            devModeConnecting = false;
            yield break;
        }

        Debug.Log($"[StateApplier] DEV MODE: Joined session {NetClient.Instance.SessionId}");
        devModeConnecting = false;

        // Load enemy configs from server after joining session
        LoadEnemyConfigsAfterJoin();

        // Polling will start automatically in Update()
    }

    /// <summary>
    /// Load enemy configs from server after joining session.
    /// Called automatically after successful connection.
    /// </summary>
    private void LoadEnemyConfigsAfterJoin()
    {
        // Ensure EnemyConfigManager exists
        if (EnemyConfigManager.Instance == null)
        {
            var go = new GameObject("EnemyConfigManager");
            go.AddComponent<EnemyConfigManager>();
        }

        // Load all enemy configs from server
        StartCoroutine(EnemyConfigManager.Instance.LoadAllEnemiesAsync());
    }

    private void OnEnable()
    {
        // Subscribe to quit event
        Application.wantsToQuit += OnWantsToQuit;

#if UNITY_EDITOR
        // Subscribe to play mode state change for Editor
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

        // Get prediction/interpolation components
        m_Predictor = GetComponent<ClientPredictor>();
        m_Interpolator = GetComponent<StateInterpolator>();

        // Subscribe to SignalR events if available
        if (NetClient.Instance != null)
        {
            NetClient.Instance.OnGameStateReceived += OnSignalRGameStateReceived;
            NetClient.Instance.OnSignalRConnectionChanged += OnSignalRConnectionChanged;

            // Check if SignalR is already connected
            if (NetClient.Instance.IsSignalRConnected)
            {
                m_UseSignalRMode = true;
                if (m_EnableLogging)
                {
                    Debug.Log("[StateApplier] Using SignalR mode for state sync");
                }
            }
        }

        // Auto-start polling if NetClient is connected and not using SignalR
        if (m_AutoPoll && !m_UseSignalRMode && NetClient.Instance != null && NetClient.Instance.IsConnected && !isPolling)
        {
            StartPolling();
        }
    }

    private void OnDisable()
    {
        Application.wantsToQuit -= OnWantsToQuit;

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

        // Unsubscribe from SignalR events
        if (NetClient.Instance != null)
        {
            NetClient.Instance.OnGameStateReceived -= OnSignalRGameStateReceived;
            NetClient.Instance.OnSignalRConnectionChanged -= OnSignalRConnectionChanged;
        }

        StopPolling();
    }

    /// <summary>
    /// Handle SignalR connection state changes.
    /// </summary>
    private void OnSignalRConnectionChanged(bool connected)
    {
        m_UseSignalRMode = connected;

        if (connected)
        {
            // Stop polling when SignalR connects
            StopPolling();
            if (m_EnableLogging)
            {
                Debug.Log("[StateApplier] Switched to SignalR mode");
            }
        }
        else
        {
            // Fall back to polling when SignalR disconnects
            if (m_AutoPoll && NetClient.Instance != null && NetClient.Instance.IsConnected)
            {
                StartPolling();
                if (m_EnableLogging)
                {
                    Debug.Log("[StateApplier] Fell back to polling mode");
                }
            }
        }
    }

    /// <summary>
    /// Handle game state received via SignalR.
    /// </summary>
    private void OnSignalRGameStateReceived(GameStateSnapshot state)
    {
        if (state == null || state.players == null || NetClient.Instance == null)
            return;

        var myId = NetClient.Instance.PlayerId.ToString();

        foreach (var p in state.players)
        {
            if (p.id == myId)
            {
                ApplySignalRSnapshot(p, state.serverTime, state.sequence, state.confirmedInputSequence);
                break;
            }
        }

        // Update remote players via RemotePlayerManager
        var remotePlayerManager = FindObjectOfType<RemotePlayerManager>();
        if (remotePlayerManager != null)
        {
            // Convert to RemotePlayerSnapshot list for RemotePlayerManager
            var playerSnapshots = new System.Collections.Generic.List<RemotePlayerSnapshot>();
            foreach (var p in state.players)
            {
                playerSnapshots.Add(RemotePlayerSnapshot.FromSignalR(p));
            }
            remotePlayerManager.UpdateFromSnapshot(playerSnapshots, state.serverTime, state.sequence);
        }

        // Sync enemies via EnemySpawner
        if (enemySpawner != null)
        {
            // Convert SignalR state to legacy StateResponse for EnemySpawner
            var legacyState = ConvertToLegacyState(state);
            enemySpawner.OnStateReceived(legacyState);
        }
    }

    /// <summary>
    /// Apply player snapshot from SignalR with prediction reconciliation.
    /// </summary>
    private void ApplySignalRSnapshot(SignalRPlayerSnapshot snapshot, float serverTime, int sequence, int confirmedInputSequence)
    {
        if (snapshot == null) return;

        var serverPos = new Vector3(snapshot.x, snapshot.y, 0);

        // Add to interpolation buffer
        if (m_Interpolator != null)
        {
            m_Interpolator.AddSnapshot(serverPos, serverTime, sequence, snapshot.hp, snapshot.maxHp, snapshot.status);
        }

        // Reconcile prediction (for local player only)
        if (m_Predictor != null)
        {
            m_Predictor.Reconcile(snapshot.x, snapshot.y, snapshot.lastConfirmedInputSequence);
        }
        else
        {
            // No predictor - apply position via interpolation or directly
            if (m_Interpolator == null)
            {
                targetPosition = serverPos;
                hasTargetPosition = true;
            }
        }

        // Apply non-position state immediately
        CurrentHp = snapshot.hp;
        MaxHp = snapshot.maxHp;
        Sequence = sequence;

        // Sync with StatsManager
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.ApplySnapshot(snapshot.hp, snapshot.maxHp);
        }

        // Update ExpManager (only level, exp values come from regular state sync)
        if (expManager != null && snapshot.level > 0)
        {
            expManager.SyncFromServer(snapshot.level, 0, 100); // Basic level sync from SignalR
        }
    }

    /// <summary>
    /// Convert SignalR GameStateSnapshot to legacy StateResponse.
    /// </summary>
    private StateResponse ConvertToLegacyState(GameStateSnapshot state)
    {
        var response = new StateResponse
        {
            sessionId = NetClient.Instance?.SessionId ?? "default",
            version = state.sequence
        };

        // Convert enemies
        if (state.enemies != null)
        {
            response.enemies = new EnemySnapshot[state.enemies.Count];
            for (int i = 0; i < state.enemies.Count; i++)
            {
                var e = state.enemies[i];
                response.enemies[i] = new EnemySnapshot
                {
                    id = e.id,
                    typeId = e.typeId,
                    x = e.x,
                    y = e.y,
                    hp = e.hp,
                    maxHp = e.maxHp,
                    status = e.status
                };
            }
        }

        return response;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Called when play mode state changes in Editor.
    /// </summary>
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // ExitingPlayMode is called when Stop button is pressed
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (m_EnableLogging) Debug.Log("[StateApplier] Editor stopping play mode, saving progress...");

            if (NetClient.Instance != null && NetClient.Instance.IsConnected && !isSavingOnQuit)
            {
                isSavingOnQuit = true;
                // Use synchronous save for Editor since coroutines won't complete
                SaveProgressSync();
            }
        }
    }

    /// <summary>
    /// Synchronous save for Editor play mode exit.
    /// </summary>
    private void SaveProgressSync()
    {
        if (NetClient.Instance == null || !NetClient.Instance.IsConnected)
        {
            Debug.LogWarning("[StateApplier] Cannot save - not connected");
            return;
        }

        var playerId = NetClient.Instance.PlayerId.ToString();
        var token = NetClient.Instance.Token;
        var url = $"{GetBaseUrl()}/sessions/save";

        var payload = JsonUtility.ToJson(new SaveProgressRequest { playerId = playerId, token = token });

        using (var client = new System.Net.Http.HttpClient())
        {
            client.Timeout = System.TimeSpan.FromSeconds(5);
            var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = client.PostAsync(url, content).Result;
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log("[StateApplier] Progress saved successfully on Editor stop!");
                }
                else
                {
                    Debug.LogWarning($"[StateApplier] Save failed: {response.StatusCode}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[StateApplier] Save exception: {ex.Message}");
            }
        }
    }

    private string GetBaseUrl()
    {
        // Try to get from NetClient, fallback to dev URL
        if (NetClient.Instance != null)
        {
            var field = typeof(NetClient).GetField("m_BaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(NetClient.Instance) as string ?? m_DevServerUrl;
            }
        }
        return m_DevServerUrl;
    }
#endif

    private void Update()
    {
        // Check if we should start polling (NetClient might connect after OnEnable)
        if (m_AutoPoll && !isPolling && NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            StartPolling();

            // Load enemy configs when connection is established
            if (EnemyConfigManager.Instance != null && !EnemyConfigManager.Instance.IsLoaded)
            {
                StartCoroutine(EnemyConfigManager.Instance.LoadAllEnemiesAsync());
            }
        }

        // Try to get predictor if not found yet (may be added after Start)
        if (m_Predictor == null)
        {
            m_Predictor = GetComponent<ClientPredictor>();
        }

        // IMPORTANT: Skip position interpolation if ClientPredictor is active
        // Local player position is handled by ClientPredictor + Rigidbody physics
        // Only apply position updates for remote players or when predictor is not available
        if (hasTargetPosition && m_Predictor == null)
        {
            // Use SmoothDamp for smoother interpolation (better than Lerp for network sync)
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > positionChangeThreshold)
            {
                // Calculate smooth time based on lerp speed (lower smooth time = faster interpolation)
                float smoothTime = 1f / m_LerpSpeed;
                // Clamp smooth time to reasonable range (0.01s to 0.2s)
                smoothTime = Mathf.Clamp(smoothTime, 0.01f, 0.2f);

                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            }
            else if (distance > 0.001f)
            {
                // Snap to target if very close to reduce micro-jitter
                transform.position = targetPosition;
                velocity = Vector3.zero; // Reset velocity when snapping
            }
            else
            {
                // Very close, reset velocity to prevent drift
                velocity = Vector3.zero;
            }
        }

        // Auto-save timer
        if (m_AutoSave)
        {
            if (NetClient.Instance == null)
            {
                // Log once every 5 seconds to avoid spam
                if (Time.frameCount % 300 == 0 && m_EnableLogging)
                    Debug.LogWarning("[StateApplier] Auto-save skipped: NetClient.Instance is null");
                return;
            }

            if (!NetClient.Instance.IsConnected)
            {
                // Log once every 5 seconds to avoid spam
                if (Time.frameCount % 300 == 0 && m_EnableLogging)
                    Debug.LogWarning("[StateApplier] Auto-save skipped: Not connected");
                return;
            }

            autoSaveTimer += Time.deltaTime;

            // Log countdown every 10 seconds for debugging
            if (m_EnableLogging && Mathf.FloorToInt(autoSaveTimer) % 10 == 0 && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[StateApplier] Auto-save in {m_AutoSaveInterval - autoSaveTimer:F0}s");
            }

            if (autoSaveTimer >= m_AutoSaveInterval)
            {
                autoSaveTimer = 0f;
                if (m_EnableLogging) Debug.Log("[StateApplier] Triggering auto-save...");
                StartCoroutine(NetClient.Instance.SaveProgress(
                    () => { if (m_EnableLogging) Debug.Log("[StateApplier] Auto-saved progress successfully!"); },
                    err => Debug.LogWarning($"[StateApplier] Auto-save failed: {err}")
                ));
            }
        }
    }

    /// <summary>
    /// Called when application wants to quit. Returns false to delay quit until save completes.
    /// </summary>
    private bool OnWantsToQuit()
    {
        // If already saved or not connected, allow quit
        if (saveCompleted || NetClient.Instance == null || !NetClient.Instance.IsConnected)
        {
            if (m_EnableLogging) Debug.Log("[StateApplier] Quit allowed (already saved or not connected)");
            return true;
        }

        // If already saving, wait
        if (isSavingOnQuit)
        {
            if (m_EnableLogging) Debug.Log("[StateApplier] Waiting for save to complete...");
            return false;
        }

        // Start saving
        isSavingOnQuit = true;
        if (m_EnableLogging) Debug.Log("[StateApplier] Saving progress before quit...");

        StartCoroutine(SaveAndQuit());
        return false; // Delay quit
    }

    private System.Collections.IEnumerator SaveAndQuit()
    {
        yield return NetClient.Instance.SaveProgress(
            () =>
            {
                if (m_EnableLogging) Debug.Log("[StateApplier] Progress saved! Quitting...");
                saveCompleted = true;
            },
            err =>
            {
                Debug.LogWarning($"[StateApplier] Save failed on quit: {err}");
                saveCompleted = true; // Quit anyway
            }
        );

        // Force quit after save
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDestroy()
    {
        // Only save on destroy if not already saving on quit
        if (!isSavingOnQuit && NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            if (m_EnableLogging) Debug.Log("[StateApplier] Saving progress on destroy...");
            StartCoroutine(NetClient.Instance.SaveProgress());
        }
    }
    #endregion

    #region Private Methods
    private void StartPolling()
    {
        if (isPolling) return;
        isPolling = true;

        // Use polling interval from config if available
        float pollInterval = m_PollInterval;
        if (GameConfigLoader.Instance != null && GameConfigLoader.Instance.Config != null)
        {
            pollInterval = GameConfigLoader.Instance.Config.polling.stateIntervalSeconds;
        }

        if (m_EnableLogging)
            Debug.Log($"[StateApplier] Starting state polling with interval: {pollInterval}s");

        NetClient.Instance.StartPolling(null, pollInterval, OnStateReceived, OnPollError);
    }

    private void StopPolling()
    {
        if (!isPolling) return;
        isPolling = false;

        if (NetClient.Instance != null)
            NetClient.Instance.StopPolling();

        if (m_EnableLogging)
            Debug.Log("[StateApplier] Stopped state polling.");
    }

    private void OnStateReceived(StateResponse state)
    {
        if (state == null || state.players == null || NetClient.Instance == null)
            return;

        var myId = NetClient.Instance.PlayerId.ToString();
        foreach (var p in state.players)
        {
            if (p.id == myId)
            {
                ApplySnapshot(p);
                break;
            }
        }

        // Sync enemies via EnemySpawner
        if (enemySpawner != null)
        {
            enemySpawner.OnStateReceived(state);
        }

        // Player not found in state
        if (m_EnableLogging && lastLoggedSequence != state.version)
        {
            Debug.LogWarning($"[StateApplier] My player not found in state v{state.version} (players: {state.players?.Length ?? 0})");
            lastLoggedSequence = state.version;
        }
    }

    private void OnPollError(string error)
    {
        Debug.LogError($"[StateApplier] Poll error: {error}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Applies the player snapshot received from the server.
    /// </summary>
    public void ApplySnapshot(PlayerSnapshot snapshot)
    {
        if (snapshot == null) return;

        if (snapshot.sequence < Sequence)
        {
            return; // Skip stale state to reduce jitter
        }

        var oldPos = targetPosition;
        var newPos = new Vector3(snapshot.x, snapshot.y, 0);

        // Only update target position if it changed significantly
        float posDelta = Vector3.Distance(oldPos, newPos);
        if (posDelta > positionChangeThreshold || !hasTargetPosition)
        {
            targetPosition = newPos;
            hasTargetPosition = true;

            // If position changed significantly, adjust velocity to match direction
            if (posDelta > 0.5f && hasTargetPosition)
            {
                Vector3 currentPos = transform.position;
                Vector3 direction = (newPos - currentPos).normalized;
                float currentSpeed = velocity.magnitude;
                // Scale velocity to match expected speed based on distance
                float expectedSpeed = posDelta / (1f / m_LerpSpeed);
                velocity = direction * Mathf.Min(currentSpeed, expectedSpeed);
            }
        }
        CurrentHp = snapshot.hp;
        MaxHp = snapshot.maxHp;
        Sequence = snapshot.sequence;

        // Sync with StatsManager if available
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.ApplySnapshot(snapshot.hp, snapshot.maxHp);
            // Sync player stats from server (loaded from database)
            StatsManager.Instance.ApplyServerStats(
                snapshot.damage, snapshot.range, snapshot.speed,
                snapshot.weaponRange, snapshot.knockbackForce, snapshot.knockbackTime, snapshot.stunTime,
                snapshot.bonusDamagePercent, snapshot.damageReductionPercent);
        }

        // Sync progression
        if (expManager != null)
        {
            expManager.SyncFromServer(snapshot.level, snapshot.exp, snapshot.expToLevel);
        }

        // Sync gold via InventoryManager
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SyncGold(snapshot.gold);
        }

        // Log when position or HP changes
        if (m_EnableLogging && snapshot.sequence != lastLoggedSequence)
        {
            var logPosDelta = Vector3.Distance(oldPos, targetPosition);
            Debug.Log($"[StateApplier] Applied: pos=({snapshot.x:F1},{snapshot.y:F1}) Δ={logPosDelta:F2} hp={snapshot.hp}/{snapshot.maxHp} seq={snapshot.sequence}");
            lastLoggedSequence = snapshot.sequence;
        }
    }

    /// <summary>
    /// Resets the applier state (e.g., when disconnecting).
    /// </summary>
    public void ResetState()
    {
        hasTargetPosition = false;
        CurrentHp = 0;
        MaxHp = 0;
        Sequence = 0;
        lastLoggedSequence = -1;
    }

    /// <summary>
    /// Manually trigger polling (if AutoPoll is disabled).
    /// </summary>
    public void ManualStartPolling()
    {
        StartPolling();
    }

    /// <summary>
    /// Force snap position immediately (used for respawn to skip interpolation).
    /// </summary>
    public void ForceSnapPosition(Vector3 position)
    {
        transform.position = position;
        targetPosition = position;
        hasTargetPosition = true;
        velocity = Vector3.zero; // Reset velocity to prevent interpolation drift

        if (m_EnableLogging)
        {
            Debug.Log($"[StateApplier] Force snapped position to ({position.x:F1}, {position.y:F1})");
        }
    }
    #endregion
}

