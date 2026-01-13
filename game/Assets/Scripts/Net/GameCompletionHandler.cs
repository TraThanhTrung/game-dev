using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles game completion flow: shows completion status on loading screen and returns to Home scene.
/// </summary>
public class GameCompletionHandler : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[GameCompletion]";
    private const string c_HomeSceneName = "Home";
    private const float c_CompletionDisplayTime = 3f; // Show completion message for 3 seconds
    #endregion

    #region Private Fields
    private bool m_IsHandlingCompletion = false;
    #endregion

    #region Public Properties
    public static GameCompletionHandler Instance { get; private set; }
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
        // Don't destroy on load - we need this to persist across scene transitions
        // But only if parent is null (not already a child of another DontDestroyOnLoad object)
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
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
    /// Handle game completion: show loading screen with completion status and return to Home scene.
    /// </summary>
    public void HandleGameCompleted()
    {
        if (m_IsHandlingCompletion)
        {
            Debug.LogWarning($"{c_LogPrefix} Already handling completion, ignoring duplicate call");
            return;
        }

        m_IsHandlingCompletion = true;
        StartCoroutine(CompletionSequence());
    }

    /// <summary>
    /// Handle game failure: show loading screen with failure status and return to Home scene.
    /// </summary>
    public void HandleGameFailed()
    {
        if (m_IsHandlingCompletion)
        {
            Debug.LogWarning($"{c_LogPrefix} Already handling completion, ignoring duplicate call");
            return;
        }

        m_IsHandlingCompletion = true;
        StartCoroutine(FailureSequence());
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sequence for handling game completion.
    /// </summary>
    private IEnumerator CompletionSequence()
    {
        Debug.Log($"{c_LogPrefix} Game completed! Starting completion sequence...");

        // Show loading screen with completion message
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Show("Game Completed!");
            LoadingScreenManager.Instance.SetStatus("All sections cleared! Victory!");
            LoadingScreenManager.Instance.SetProgress(1.0f);
        }

        // Disconnect from server
        if (NetClient.Instance != null && NetClient.Instance.IsSignalRConnected)
        {
            Debug.Log($"{c_LogPrefix} Disconnecting from server...");
            NetClient.Instance.DisconnectSignalR();
        }

        // Wait for completion message display
        yield return new WaitForSeconds(c_CompletionDisplayTime);

        // Update status to indicate returning
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.SetStatus("Returning to profile...");
            LoadingScreenManager.Instance.SetProgress(1.0f);
        }

        // Wait a bit before loading scene
        yield return new WaitForSeconds(1f);

        // Load Home scene
        Debug.Log($"{c_LogPrefix} Loading Home scene...");
        SceneManager.LoadScene(c_HomeSceneName);

        // Reset flag after scene load
        yield return new WaitForSeconds(0.5f);
        m_IsHandlingCompletion = false;

        // Hide loading screen after scene transition
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Hide();
        }
    }

    /// <summary>
    /// Sequence for handling game failure.
    /// </summary>
    private IEnumerator FailureSequence()
    {
        Debug.Log($"{c_LogPrefix} Game failed! Starting failure sequence...");

        // Show loading screen with failure message
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Show("Game Over");
            LoadingScreenManager.Instance.SetStatus("All players died. Returning to profile...");
            LoadingScreenManager.Instance.SetProgress(1.0f);
        }

        // Disconnect from server
        if (NetClient.Instance != null && NetClient.Instance.IsSignalRConnected)
        {
            Debug.Log($"{c_LogPrefix} Disconnecting from server...");
            NetClient.Instance.DisconnectSignalR();
        }

        // Wait for failure message display
        yield return new WaitForSeconds(c_CompletionDisplayTime);

        // Load Home scene
        Debug.Log($"{c_LogPrefix} Loading Home scene...");
        SceneManager.LoadScene(c_HomeSceneName);

        // Reset flag after scene load
        yield return new WaitForSeconds(0.5f);
        m_IsHandlingCompletion = false;

        // Hide loading screen after scene transition
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Hide();
        }
    }
    #endregion
}

