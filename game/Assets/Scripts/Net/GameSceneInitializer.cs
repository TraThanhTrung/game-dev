using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coordinates game scene initialization.
/// Ensures correct order of operations for loading and SignalR connection.
/// Attach to a persistent GameObject that loads with the game scene.
/// </summary>
public class GameSceneInitializer : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[GameSceneInit]";
    #endregion

    #region Private Fields
    [Header("References")]
    [SerializeField] private GameObject m_LoadingScreenPrefab;
    [SerializeField] private PlayerSpawner m_PlayerSpawner;
    
    [Header("Settings")]
    [SerializeField] private bool m_AutoInitialize = true;
    [SerializeField] private bool m_EnableLogging = true;
    
    // State
    private bool m_IsInitialized;
    private LoadingScreenManager m_LoadingScreen;
    #endregion

    #region Public Properties
    public static GameSceneInitializer Instance { get; private set; }
    public bool IsInitialized => m_IsInitialized;
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
    }

    private void Start()
    {
        if (m_AutoInitialize)
        {
            // Get character type from NetClient
            string characterType = "lancer";
            string sessionId = "default";
            
            if (NetClient.Instance != null)
            {
                characterType = NetClient.Instance.SelectedCharacterType;
                sessionId = NetClient.Instance.SessionId;
            }
            
            StartCoroutine(InitializeGameScene(characterType, sessionId));
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
    /// Manually trigger game scene initialization.
    /// </summary>
    public void Initialize(string characterType, string sessionId)
    {
        if (m_IsInitialized)
        {
            Debug.LogWarning($"{c_LogPrefix} Already initialized!");
            return;
        }
        
        StartCoroutine(InitializeGameScene(characterType, sessionId));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Main initialization coroutine.
    /// </summary>
    private IEnumerator InitializeGameScene(string characterType, string sessionId)
    {
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Starting initialization: char={characterType}, session={sessionId}");
        }
        
        // Step 1: Ensure InputBlocker exists and blocks input
        InputBlocker.EnsureExists();
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.BlockInput();
        }
        
        // Step 2: Create or get loading screen
        m_LoadingScreen = LoadingScreenManager.Instance;
        if (m_LoadingScreen == null && m_LoadingScreenPrefab != null)
        {
            var loadingObj = Instantiate(m_LoadingScreenPrefab);
            m_LoadingScreen = loadingObj.GetComponent<LoadingScreenManager>();
        }
        
        // Step 3: Use LoadingScreenManager for the full loading sequence
        if (m_LoadingScreen != null)
        {
            bool loadingComplete = false;
            string loadingError = null;
            
            m_LoadingScreen.StartLoading(characterType, sessionId,
                () => loadingComplete = true,
                (err) => { loadingError = err; loadingComplete = true; });
            
            // Wait for loading to complete
            while (!loadingComplete)
            {
                yield return null;
            }
            
            if (loadingError != null)
            {
                Debug.LogError($"{c_LogPrefix} Loading failed: {loadingError}");
                yield break;
            }
        }
        else
        {
            // No loading screen - do minimal initialization
            yield return InitializeWithoutLoadingScreen(characterType, sessionId);
        }
        
        m_IsInitialized = true;
        
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Initialization complete! Game ready.");
        }
    }

    /// <summary>
    /// Minimal initialization without loading screen.
    /// </summary>
    private IEnumerator InitializeWithoutLoadingScreen(string characterType, string sessionId)
    {
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Initializing without loading screen");
        }
        
        // Spawn player
        if (m_PlayerSpawner != null)
        {
            yield return m_PlayerSpawner.SpawnLocalPlayer(characterType);
        }
        
        // Connect SignalR
        if (NetClient.Instance != null)
        {
            bool connected = false;
            yield return NetClient.Instance.ConnectSignalRAsync(sessionId,
                () => connected = true,
                (err) => Debug.LogError($"{c_LogPrefix} SignalR error: {err}"));
            
            if (!connected)
            {
                Debug.LogWarning($"{c_LogPrefix} Failed to connect SignalR");
            }
        }
        
        // Unblock input
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.UnblockInput();
        }
    }
    #endregion
}

