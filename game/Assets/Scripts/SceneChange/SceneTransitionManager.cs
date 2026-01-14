using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages scene transitions with proper cleanup of previous scene objects.
/// Preserves DontDestroyOnLoad objects (NetClient, etc.) while cleaning up scene-specific objects.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[SceneTransition]";
    #endregion

    #region Private Fields
    private static SceneTransitionManager s_Instance;
    private static readonly HashSet<string> s_PersistentObjectNames = new HashSet<string>
    {
        "NetClient",
        "GameCompletionHandler",
        "EnemySpriteManager",
        "EnemyConfigManager",
        "LoadingScreenManager"
    };
    #endregion

    #region Public Properties
    public static SceneTransitionManager Instance => s_Instance;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Load a new scene and unload all other scenes except the new one.
    /// Preserves DontDestroyOnLoad objects.
    /// </summary>
    public void LoadSceneSingle(string sceneName)
    {
        StartCoroutine(LoadSceneSingleCoroutine(sceneName));
    }

    /// <summary>
    /// Unload a specific scene by name.
    /// </summary>
    public void UnloadScene(string sceneName)
    {
        StartCoroutine(UnloadSceneCoroutine(sceneName));
    }

    /// <summary>
    /// Clean up all objects from a specific scene (except DontDestroyOnLoad objects).
    /// </summary>
    public void CleanupSceneObjects(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
        {
            Debug.LogWarning($"{c_LogPrefix} Scene '{sceneName}' is not loaded or invalid");
            return;
        }

        CleanupSceneObjects(scene);
    }

    /// <summary>
    /// Clean up all objects from current scene except DontDestroyOnLoad objects.
    /// </summary>
    public void CleanupCurrentSceneObjects()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        CleanupSceneObjects(currentScene);
    }
    #endregion

    #region Private Methods
    private IEnumerator LoadSceneSingleCoroutine(string sceneName)
    {
        Debug.Log($"{c_LogPrefix} Loading scene '{sceneName}' (single mode)...");

        // Get all currently loaded scenes
        List<Scene> scenesToUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != sceneName && scene.isLoaded)
            {
                scenesToUnload.Add(scene);
            }
        }

        // Unload all other scenes first
        foreach (var scene in scenesToUnload)
        {
            Debug.Log($"{c_LogPrefix} Unloading scene '{scene.name}'...");
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        // Clean up any remaining objects from unloaded scenes
        CleanupOrphanedObjects();

        // Load new scene
        Debug.Log($"{c_LogPrefix} Loading scene '{sceneName}'...");
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        // Clean up after scene load to ensure no leftover objects
        CleanupAfterSceneLoad(sceneName);

        Debug.Log($"{c_LogPrefix} Scene '{sceneName}' loaded successfully");
    }

    private IEnumerator UnloadSceneCoroutine(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"{c_LogPrefix} Scene '{sceneName}' is not loaded");
            yield break;
        }

        Debug.Log($"{c_LogPrefix} Unloading scene '{sceneName}'...");
        yield return SceneManager.UnloadSceneAsync(scene);

        // Clean up orphaned objects
        CleanupOrphanedObjects();
    }

    private void CleanupSceneObjects(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        int destroyedCount = 0;

        foreach (GameObject obj in rootObjects)
        {
            // Skip DontDestroyOnLoad objects
            if (IsPersistentObject(obj))
            {
                continue;
            }

            // Destroy the object
            Destroy(obj);
            destroyedCount++;
        }

        if (destroyedCount > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {destroyedCount} objects from scene '{scene.name}'");
        }
    }

    private void CleanupOrphanedObjects()
    {
        // Find all GameObjects in the scene that might be orphaned
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int destroyedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // Skip if it's a DontDestroyOnLoad object
            if (IsPersistentObject(obj))
            {
                continue;
            }

            // Skip if it's in a loaded scene
            if (obj.scene.IsValid() && obj.scene.isLoaded)
            {
                continue;
            }

            // This object is orphaned (not in any valid scene)
            Destroy(obj);
            destroyedCount++;
        }

        if (destroyedCount > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {destroyedCount} orphaned objects");
        }
    }

    private void CleanupAfterSceneLoad(string newSceneName)
    {
        Debug.Log($"{c_LogPrefix} Cleaning up after loading scene '{newSceneName}'...");

        // Clean up DontDestroyOnLoad objects that are not needed in Result scene
        // Only cleanup when entering Result scene (not Home, because Home -> RPG needs these objects)
        if (newSceneName == "Result")
        {
            CleanupDontDestroyOnLoadObjects();
        }

        // Find and remove duplicate AudioListeners (keep only one from new scene)
        AudioListener[] allListeners = FindObjectsOfType<AudioListener>(true);
        AudioListener keepListener = null;
        int removedListeners = 0;

        foreach (AudioListener listener in allListeners)
        {
            // Keep the listener from the new scene
            if (listener.gameObject.scene.name == newSceneName)
            {
                if (keepListener == null)
                {
                    keepListener = listener;
                }
                else
                {
                    // Multiple listeners in new scene - disable extras
                    Debug.LogWarning($"{c_LogPrefix} Multiple AudioListeners in '{newSceneName}' scene. Disabling '{listener.gameObject.name}'");
                    listener.enabled = false;
                    removedListeners++;
                }
            }
            else if (!IsPersistentObject(listener.gameObject))
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

        // Clean up all objects not in the new scene or DontDestroyOnLoad
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        int destroyedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // Skip persistent objects
            if (IsPersistentObject(obj))
            {
                continue;
            }

            // Skip objects in the new scene
            if (obj.scene.name == newSceneName)
            {
                continue;
            }

            // Skip if already destroyed
            if (obj == null)
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
            Debug.Log($"{c_LogPrefix} Cleaned up {destroyedCount} object(s) from previous scenes");
        }
    }

    private bool IsPersistentObject(GameObject obj)
    {
        if (obj == null)
            return false;

        // Check if object name matches persistent object names
        if (s_PersistentObjectNames.Contains(obj.name))
        {
            return true;
        }

        // Check if object is marked as DontDestroyOnLoad by checking if it's in DontDestroyOnLoad scene
        // Objects in DontDestroyOnLoad scene have scene.name == "DontDestroyOnLoad"
        if (obj.scene.name == "DontDestroyOnLoad")
        {
            return true;
        }

        // Check if parent is a persistent object
        if (obj.transform.parent != null)
        {
            return IsPersistentObject(obj.transform.parent.gameObject);
        }

        return false;
    }

    /// <summary>
    /// Clean up DontDestroyOnLoad objects that are not needed in Result scene.
    /// These objects should only persist from Login -> Home -> RPG, but not in Result.
    /// Note: Player is cleaned up here but will be re-spawned when returning to Home/RPG.
    /// Note: GameConfigLoader (GameConfig) is NOT cleaned up as it's needed for polling config.
    /// </summary>
    private void CleanupDontDestroyOnLoadObjects()
    {
        // Objects that should be cleaned up when entering Result scene
        // These will be re-created when needed in Home/RPG scenes
        // NOTE: GameConfigLoader (GameConfig) is NOT in this list - it's a global singleton
        // needed for polling config and exp curve settings across all scenes.
        string[] objectsToCleanup =
        {
            "ExpCanvas", "UIManager", "GameManager", "InventoryCanvas",
            "Cameras", "ShopCanvas", "Player",
            "[Debug Updater]", "Player UI", "InputBlocker"
        };

        int cleanedCount = 0;
        foreach (string objName in objectsToCleanup)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null && obj.scene.name == "DontDestroyOnLoad")
            {
                Debug.Log($"{c_LogPrefix} Cleaning up DontDestroyOnLoad object '{objName}'");
                Destroy(obj);
                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
        {
            Debug.Log($"{c_LogPrefix} Cleaned up {cleanedCount} object(s) from DontDestroyOnLoad");
        }
    }
    #endregion
}

