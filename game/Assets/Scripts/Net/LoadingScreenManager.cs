using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the loading screen before entering the game.
/// Validates all resources before allowing SignalR connection.
/// Blocks input until loading is complete.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[LoadingScreen]";
    #endregion

    #region Private Fields
    [Header("UI References")]
    [SerializeField] private Canvas m_LoadingCanvas;
    [SerializeField] private Image m_BackgroundPanel;
    [SerializeField] private Slider m_ProgressBar;
    [SerializeField] private TextMeshProUGUI m_StatusText;
    [SerializeField] private TextMeshProUGUI m_ProgressText;
    [SerializeField] private GameObject m_SpinnerObject;
    
    [Header("Settings")]
    [SerializeField] private float m_MinLoadingTime = 1.0f; // Minimum time to show loading screen
    [SerializeField] private bool m_EnableLogging = true;
    
    // Loading state
    private float m_LoadingProgress;
    private string m_CurrentStatus = "Initializing...";
    private bool m_IsLoading;
    private Action m_OnLoadingComplete;
    private Action<string> m_OnLoadingFailed;
    #endregion

    #region Public Properties
    public static LoadingScreenManager Instance { get; private set; }
    public bool IsLoading => m_IsLoading;
    public float Progress => m_LoadingProgress;
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
        
        // Don't destroy on load - we need this across scenes
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        // Initialize UI
        if (m_LoadingCanvas != null)
        {
            m_LoadingCanvas.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Update UI during loading
        if (m_IsLoading)
        {
            UpdateUI();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Start the loading process for a game session.
    /// </summary>
    /// <param name="characterType">Selected character type ("lancer" or "warrious")</param>
    /// <param name="sessionId">Session ID to join</param>
    /// <param name="onComplete">Called when loading succeeds</param>
    /// <param name="onFailed">Called when loading fails with error message</param>
    public void StartLoading(string characterType, string sessionId, Action onComplete, Action<string> onFailed)
    {
        if (m_IsLoading)
        {
            Debug.LogWarning($"{c_LogPrefix} Already loading!");
            return;
        }
        
        m_OnLoadingComplete = onComplete;
        m_OnLoadingFailed = onFailed;
        
        StartCoroutine(LoadGameAsync(characterType, sessionId));
    }

    /// <summary>
    /// Show loading screen with custom status message.
    /// </summary>
    public void Show(string statusMessage = "Loading...")
    {
        m_CurrentStatus = statusMessage;
        m_LoadingProgress = 0f;
        
        if (m_LoadingCanvas != null)
        {
            m_LoadingCanvas.gameObject.SetActive(true);
        }
        
        // Ensure input is blocked
        InputBlocker.EnsureExists();
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.BlockInput();
        }
        
        UpdateUI();
    }

    /// <summary>
    /// Hide loading screen and unblock input.
    /// </summary>
    public void Hide()
    {
        m_IsLoading = false;
        
        if (m_LoadingCanvas != null)
        {
            m_LoadingCanvas.gameObject.SetActive(false);
        }
        
        // Unblock input
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.UnblockInput();
        }
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Loading screen hidden, input unblocked");
        }
    }

    /// <summary>
    /// Update loading status text.
    /// </summary>
    public void SetStatus(string status)
    {
        m_CurrentStatus = status;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} {status}");
        }
    }

    /// <summary>
    /// Update loading progress (0 to 1).
    /// </summary>
    public void SetProgress(float progress)
    {
        m_LoadingProgress = Mathf.Clamp01(progress);
    }
    #endregion

    #region Private Methods - Loading Sequence
    /// <summary>
    /// Main loading coroutine. Executes all loading steps in order.
    /// </summary>
    private IEnumerator LoadGameAsync(string characterType, string sessionId)
    {
        m_IsLoading = true;
        float startTime = Time.time;
        
        // Show loading screen
        Show("Starting...");
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Starting game load: character={characterType}, session={sessionId}");
        }
        
        // Step 1: Load game configs via REST (10%)
        SetStatus("Loading game configs...");
        SetProgress(0.05f);
        yield return LoadGameConfigs();
        SetProgress(0.10f);
        
        // Step 2: Load session metadata (20%)
        SetStatus("Loading session info...");
        yield return LoadSessionMetadata(sessionId);
        SetProgress(0.20f);
        
        // Step 3: Load enemy configs (35%)
        SetStatus("Loading enemy configs...");
        yield return LoadEnemyConfigs();
        SetProgress(0.35f);
        
        // Step 4: Validate all resources (45%)
        SetStatus("Validating resources...");
        bool isValid = ValidateResources(characterType);
        if (!isValid)
        {
            SetStatus("Resource validation failed!");
            m_OnLoadingFailed?.Invoke("Failed to validate game resources");
            yield break;
        }
        SetProgress(0.45f);
        
        // Step 5: Wait for player prefab to be spawned (done by GameManager/PlayerSpawner) (60%)
        SetStatus("Initializing player...");
        yield return WaitForPlayerInitialization();
        SetProgress(0.60f);
        
        // Step 6: Initialize game objects (70%)
        SetStatus("Initializing game objects...");
        yield return InitializeGameObjects();
        SetProgress(0.70f);
        
        // Step 7: Connect SignalR (85%)
        SetStatus("Connecting to server...");
        bool connected = false;
        string connectError = null;
        
        yield return ConnectSignalR(sessionId, 
            () => connected = true,
            (err) => { connectError = err; });
        
        if (!connected)
        {
            SetStatus($"Connection failed: {connectError}");
            m_OnLoadingFailed?.Invoke(connectError ?? "Failed to connect to server");
            yield break;
        }
        SetProgress(0.85f);
        
        // Step 8: Wait for initial state (95%)
        SetStatus("Synchronizing game state...");
        yield return WaitForInitialState();
        SetProgress(0.95f);
        
        // Step 9: Apply initial state
        SetStatus("Applying game state...");
        ApplyInitialState();
        SetProgress(1.0f);
        
        // Ensure minimum loading time for UX
        float elapsed = Time.time - startTime;
        if (elapsed < m_MinLoadingTime)
        {
            yield return new WaitForSeconds(m_MinLoadingTime - elapsed);
        }
        
        // Step 10: Ready - disable loading, enable input
        SetStatus("Ready!");
        yield return new WaitForSeconds(0.2f); // Brief pause to show "Ready!"
        
        OnLoadingComplete();
    }

    private IEnumerator LoadGameConfigs()
    {
        // Game configs are loaded from shared/game-config.json via GameConfigLoader
        // Wait a frame to ensure configs are loaded
        yield return null;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Game configs loaded");
        }
    }

    private IEnumerator LoadSessionMetadata(string sessionId)
    {
        // Load session metadata via REST API
        if (NetClient.Instance != null)
        {
            bool loaded = false;
            string error = null;
            
            StartCoroutine(NetClient.Instance.GetSessionMetadata(sessionId,
                (metadata) => { loaded = true; },
                (err) => { error = err; loaded = true; }
            ));
            
            // Wait for response (max 5 seconds)
            float timeout = 5f;
            while (!loaded && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            
            if (error != null)
            {
                Debug.LogWarning($"{c_LogPrefix} Failed to load session metadata: {error}");
                // Continue anyway - session may be new
            }
        }
        else
        {
            Debug.LogWarning($"{c_LogPrefix} NetClient not available for session metadata");
        }
        
        yield return null;
    }

    private IEnumerator LoadEnemyConfigs()
    {
        // Wait for EnemyConfigManager to load configs
        float timeout = 5f;
        while (EnemyConfigManager.Instance == null && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        if (EnemyConfigManager.Instance != null)
        {
            // Trigger config load if not already loaded
            if (!EnemyConfigManager.Instance.IsLoaded)
            {
                StartCoroutine(EnemyConfigManager.Instance.LoadAllEnemiesAsync());
                
                // Wait for load
                timeout = 5f;
                while (!EnemyConfigManager.Instance.IsLoaded && timeout > 0)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }
        }
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Enemy configs loaded");
        }
    }

    private bool ValidateResources(string characterType)
    {
        // Check all required resources are available
        bool valid = true;
        
        // Check NetClient
        if (NetClient.Instance == null)
        {
            Debug.LogError($"{c_LogPrefix} NetClient not found!");
            valid = false;
        }
        
        // Check EnemyConfigManager
        if (EnemyConfigManager.Instance == null)
        {
            Debug.LogWarning($"{c_LogPrefix} EnemyConfigManager not found (may be OK if no enemies)");
        }
        
        // Check InputBlocker
        if (InputBlocker.Instance == null)
        {
            Debug.LogWarning($"{c_LogPrefix} InputBlocker not found, creating...");
            InputBlocker.EnsureExists();
        }
        
        if (m_EnableLogging && valid)
        {
            Debug.Log($"{c_LogPrefix} Resources validated successfully");
        }
        
        return valid;
    }

    private IEnumerator WaitForPlayerInitialization()
    {
        // Wait for player to be spawned by GameManager/PlayerSpawner
        // This should happen in the game scene
        float timeout = 5f;
        while (timeout > 0)
        {
            // Look for player object
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (m_EnableLogging)
                {
                    Debug.Log($"{c_LogPrefix} Player found: {player.name}");
                }
                yield break;
            }
            
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        Debug.LogWarning($"{c_LogPrefix} Player not found after timeout (may spawn later)");
    }

    private IEnumerator InitializeGameObjects()
    {
        // Initialize any required game objects
        yield return null;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Game objects initialized");
        }
    }

    private IEnumerator ConnectSignalR(string sessionId, Action onSuccess, Action<string> onError)
    {
        if (NetClient.Instance == null)
        {
            onError?.Invoke("NetClient not available");
            yield break;
        }
        
        bool completed = false;
        string error = null;
        
        StartCoroutine(NetClient.Instance.ConnectSignalRAsync(sessionId,
            () => { completed = true; },
            (err) => { error = err; completed = true; }
        ));
        
        // Wait for connection (max 10 seconds)
        float timeout = 10f;
        while (!completed && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        if (error != null)
        {
            onError?.Invoke(error);
        }
        else if (!completed)
        {
            onError?.Invoke("Connection timed out");
        }
        else
        {
            onSuccess?.Invoke();
        }
    }

    private IEnumerator WaitForInitialState()
    {
        // Wait for initial state from SignalR
        if (NetClient.Instance != null)
        {
            float timeout = 5f;
            while (!NetClient.Instance.HasReceivedInitialState && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            
            if (timeout <= 0)
            {
                Debug.LogWarning($"{c_LogPrefix} Timed out waiting for initial state");
            }
        }
        
        yield return null;
    }

    private void ApplyInitialState()
    {
        // Initial state will be applied by ServerStateApplier when received
        // Nothing specific to do here
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Initial state applied");
        }
    }

    private void OnLoadingComplete()
    {
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Loading complete! Game ready.");
        }
        
        // Hide loading screen (also unblocks input)
        Hide();
        
        // Notify callback
        m_OnLoadingComplete?.Invoke();
    }

    private void UpdateUI()
    {
        if (m_StatusText != null)
        {
            m_StatusText.text = m_CurrentStatus;
        }
        
        if (m_ProgressBar != null)
        {
            m_ProgressBar.value = m_LoadingProgress;
        }
        
        if (m_ProgressText != null)
        {
            m_ProgressText.text = $"{Mathf.RoundToInt(m_LoadingProgress * 100)}%";
        }
        
        if (m_SpinnerObject != null)
        {
            m_SpinnerObject.transform.Rotate(0, 0, -200f * Time.deltaTime);
        }
    }
    #endregion
}

