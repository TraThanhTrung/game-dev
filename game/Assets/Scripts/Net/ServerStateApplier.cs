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

        // Polling will start automatically in Update()
    }

    private void OnEnable()
    {
        // Subscribe to quit event
        Application.wantsToQuit += OnWantsToQuit;

#if UNITY_EDITOR
        // Subscribe to play mode state change for Editor
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

        // Auto-start polling if NetClient is connected
        if (m_AutoPoll && NetClient.Instance != null && NetClient.Instance.IsConnected && !isPolling)
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

        StopPolling();
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
        }

        if (hasTargetPosition)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, m_LerpSpeed * Time.deltaTime);
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

        if (m_EnableLogging)
            Debug.Log("[StateApplier] Starting state polling...");

        NetClient.Instance.StartPolling(null, m_PollInterval, OnStateReceived, OnPollError);
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
                return;
            }
        }

        // Player not found in state
        if (m_EnableLogging && lastLoggedSequence != state.version)
        {
            Debug.LogWarning($"[StateApplier] My player not found in state v{state.version} (players: {state.players.Length})");
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

        var oldPos = targetPosition;
        targetPosition = new Vector3(snapshot.x, snapshot.y, 0);
        hasTargetPosition = true;
        CurrentHp = snapshot.hp;
        MaxHp = snapshot.maxHp;
        Sequence = snapshot.sequence;

        // Sync with StatsManager if available
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.currentHealth = snapshot.hp;
            StatsManager.Instance.maxHealth = snapshot.maxHp;
            // Sync Level, Exp, Gold if StatsManager has these fields
        }

        // Log when position or HP changes
        if (m_EnableLogging && snapshot.sequence != lastLoggedSequence)
        {
            var posDelta = Vector3.Distance(oldPos, targetPosition);
            Debug.Log($"[StateApplier] Applied: pos=({snapshot.x:F1},{snapshot.y:F1}) Δ={posDelta:F2} hp={snapshot.hp}/{snapshot.maxHp} seq={snapshot.sequence}");
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
    #endregion
}

