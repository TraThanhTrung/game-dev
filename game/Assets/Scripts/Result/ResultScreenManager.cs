using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages the Result scene: fetches match data from server and displays it.
/// </summary>
public class ResultScreenManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[ResultScreen]";
    #endregion

    #region Private Fields
    [SerializeField] private GameSessionInfoDisplay m_GameSessionInfoDisplay;
    [SerializeField] private EnemyListManager m_EnemyListManager;
    [SerializeField] private PlayerListManager m_PlayerListManager;
    [SerializeField] private GameObject m_LoadingPanel;
    [SerializeField] private TMP_Text m_ErrorText;
    [SerializeField] private UnityEngine.UI.Button m_BackToHomeButton;

    private bool m_IsLoading = false;
    #endregion

    #region Public Properties
    public static ResultScreenManager Instance { get; private set; }
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
        // Clean up objects from previous scene (RPG)
        CleanupPreviousSceneObjects();

        // Setup back button
        if (m_BackToHomeButton != null)
        {
            m_BackToHomeButton.onClick.AddListener(OnBackToHomeClicked);
        }

        // Hide error text initially
        if (m_ErrorText != null)
        {
            m_ErrorText.gameObject.SetActive(false);
        }

        // Start loading match result
        LoadMatchResult();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Clean up objects from previous scene (RPG) that might interfere with Result scene.
    /// </summary>
    private void CleanupPreviousSceneObjects()
    {
        Debug.Log($"{c_LogPrefix} Starting cleanup of previous scene objects...");

        // 1. Clean up AudioListeners - keep only one from Result scene
        AudioListener[] allListeners = FindObjectsOfType<AudioListener>(true);
        AudioListener resultListener = null;
        int removedListeners = 0;

        foreach (AudioListener listener in allListeners)
        {
            if (listener.gameObject.scene.name == "Result")
            {
                if (resultListener == null)
                {
                    resultListener = listener;
                }
                else
                {
                    // Multiple listeners in Result scene - disable extras
                    Debug.LogWarning($"{c_LogPrefix} Multiple AudioListeners in Result scene. Disabling '{listener.gameObject.name}'");
                    listener.enabled = false;
                    removedListeners++;
                }
            }
            else if (listener.gameObject.scene.name != "DontDestroyOnLoad")
            {
                // Remove listeners from other scenes
                Debug.Log($"{c_LogPrefix} Removing AudioListener from '{listener.gameObject.name}' (scene: {listener.gameObject.scene.name})");
                Destroy(listener.gameObject);
                removedListeners++;
            }
        }

        if (removedListeners > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {removedListeners} AudioListener(s)");
        }

        // 2. Clean up all Canvas objects from previous scenes
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        int removedCanvases = 0;
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.gameObject.scene.name != "Result" && canvas.gameObject.scene.name != "DontDestroyOnLoad")
            {
                Debug.Log($"{c_LogPrefix} Cleaning up canvas '{canvas.name}' from scene '{canvas.gameObject.scene.name}'");
                Destroy(canvas.gameObject);
                removedCanvases++;
            }
        }

        if (removedCanvases > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {removedCanvases} Canvas object(s)");
        }

        // 3. Clean up all Camera objects from previous scenes
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        int removedCameras = 0;
        foreach (Camera cam in allCameras)
        {
            if (cam.gameObject.scene.name != "Result" && cam.gameObject.scene.name != "DontDestroyOnLoad")
            {
                Debug.Log($"{c_LogPrefix} Cleaning up camera '{cam.name}' from scene '{cam.gameObject.scene.name}'");
                Destroy(cam.gameObject);
                removedCameras++;
            }
        }

        if (removedCameras > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {removedCameras} Camera object(s)");
        }

        // 4. Clean up DontDestroyOnLoad objects that are not needed in Result scene
        // These objects should only persist from Login -> Home -> RPG, but not in Result
        // NOTE: GameConfigLoader (GameConfig) is NOT in this list - it's a global singleton
        // needed for polling config and exp curve settings across all scenes.
        HashSet<string> objectsToCleanupFromDontDestroy = new HashSet<string>
        {
            "ExpCanvas", "UIManager", "GameManager", "InventoryCanvas",
            "Cameras", "ShopCanvas", "Player",
            "[Debug Updater]", "Player UI", "InputBlocker"
        };

        HashSet<string> persistentNames = new HashSet<string>
        {
            "NetClient", "GameCompletionHandler", "EnemySpriteManager",
            "EnemyConfigManager", "LoadingScreenManager", "SceneTransitionManager"
        };

        // Clean up specific DontDestroyOnLoad objects
        int cleanedFromDontDestroy = 0;
        GameObject[] allDontDestroyObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allDontDestroyObjects)
        {
            if (obj == null)
                continue;

            // Only process objects in DontDestroyOnLoad scene
            if (obj.scene.name != "DontDestroyOnLoad")
                continue;

            // Skip objects that should persist
            if (persistentNames.Contains(obj.name))
                continue;

            // Clean up objects that are not needed in Result scene
            if (objectsToCleanupFromDontDestroy.Contains(obj.name))
            {
                Debug.Log($"{c_LogPrefix} Cleaning up DontDestroyOnLoad object '{obj.name}'");
                Destroy(obj);
                cleanedFromDontDestroy++;
            }
        }

        if (cleanedFromDontDestroy > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {cleanedFromDontDestroy} object(s) from DontDestroyOnLoad");
        }

        // 5. Clean up all GameObjects from previous scenes (comprehensive cleanup)
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        int destroyedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // Skip if null or already destroyed
            if (obj == null)
                continue;

            // Skip persistent objects
            if (persistentNames.Contains(obj.name))
            {
                continue;
            }

            // Skip objects in DontDestroyOnLoad (already handled above)
            if (obj.scene.name == "DontDestroyOnLoad")
            {
                continue;
            }

            // Skip objects in Result scene
            if (obj.scene.name == "Result")
            {
                continue;
            }

            // Destroy objects from other scenes
            Debug.Log($"{c_LogPrefix} Cleaning up object '{obj.name}' from scene '{obj.scene.name}'");
            Destroy(obj);
            destroyedCount++;
        }

        if (destroyedCount > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {destroyedCount} GameObject(s) from previous scenes");
        }

        // 6. Force cleanup of RPG scene if still loaded
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.name == "RPG" && scene.isLoaded)
            {
                Debug.Log($"{c_LogPrefix} RPG scene still loaded, cleaning up root objects...");
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObj in rootObjects)
                {
                    if (rootObj != null && !persistentNames.Contains(rootObj.name))
                    {
                        Debug.Log($"{c_LogPrefix} Cleaning up root object '{rootObj.name}' from RPG scene");
                        Destroy(rootObj);
                    }
                }
            }
        }

        Debug.Log($"{c_LogPrefix} Cleanup completed");
    }

    /// <summary>
    /// Load match result data from server.
    /// </summary>
    private void LoadMatchResult()
    {
        if (m_IsLoading)
        {
            Debug.LogWarning($"{c_LogPrefix} Already loading match result");
            return;
        }

        // Get session ID from NetClient or PlayerPrefs
        string sessionId = GetSessionId();
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError($"{c_LogPrefix} No session ID found. Cannot load match result.");
            ShowError("No session data available.");
            return;
        }

        m_IsLoading = true;
        ShowLoading(true);

        Debug.Log($"{c_LogPrefix} Loading match result for session: {sessionId}");

        // Fetch match result from server
        if (NetClient.Instance != null)
        {
            NetClient.Instance.GetMatchResult(
                sessionId,
                OnMatchResultSuccess,
                OnMatchResultError
            );
        }
        else
        {
            Debug.LogError($"{c_LogPrefix} NetClient.Instance is null");
            ShowError("Network client not available.");
            m_IsLoading = false;
        }
    }

    /// <summary>
    /// Get session ID from NetClient or PlayerPrefs.
    /// </summary>
    private string GetSessionId()
    {
        // Try NetClient first
        if (NetClient.Instance != null && !string.IsNullOrEmpty(NetClient.Instance.SessionId))
        {
            return NetClient.Instance.SessionId;
        }

        // Fallback to PlayerPrefs
        if (PlayerPrefs.HasKey("mp_session_id"))
        {
            return PlayerPrefs.GetString("mp_session_id");
        }

        return null;
    }

    /// <summary>
    /// Handle successful match result fetch.
    /// </summary>
    private void OnMatchResultSuccess(GameResult.MatchResultData resultData)
    {
        Debug.Log($"{c_LogPrefix} Match result loaded successfully. Players: {resultData.players.Count}, Enemies: {resultData.enemies.Count}");

        m_IsLoading = false;
        ShowLoading(false);

        // Populate UI components
        if (m_GameSessionInfoDisplay != null)
        {
            m_GameSessionInfoDisplay.DisplaySessionInfo(resultData);
        }

        if (m_EnemyListManager != null)
        {
            m_EnemyListManager.PopulateEnemies(resultData.enemies);
        }

        if (m_PlayerListManager != null)
        {
            m_PlayerListManager.PopulatePlayers(resultData.players);
        }
    }

    /// <summary>
    /// Handle error when fetching match result.
    /// </summary>
    private void OnMatchResultError(string error)
    {
        Debug.LogError($"{c_LogPrefix} Failed to load match result: {error}");
        m_IsLoading = false;
        ShowLoading(false);
        ShowError($"Failed to load match result: {error}");
    }

    /// <summary>
    /// Show or hide loading panel.
    /// </summary>
    private void ShowLoading(bool show)
    {
        if (m_LoadingPanel != null)
        {
            m_LoadingPanel.SetActive(show);
        }
    }

    /// <summary>
    /// Show error message.
    /// </summary>
    private void ShowError(string message)
    {
        if (m_ErrorText != null)
        {
            m_ErrorText.text = message;
            m_ErrorText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Handle back to home button click.
    /// </summary>
    private void OnBackToHomeClicked()
    {
        Debug.Log($"{c_LogPrefix} Back to Home clicked");

        // Load Home scene with proper cleanup
        // Home scene doesn't need Player, but Player will be re-spawned when entering RPG scene
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneSingle("Home");
        }
        else
        {
            // Fallback to standard load
            UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
        }
    }
    #endregion
}

