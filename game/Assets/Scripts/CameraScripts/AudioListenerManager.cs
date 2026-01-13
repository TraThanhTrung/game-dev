using UnityEngine;

/// <summary>
/// Ensures only one AudioListener is active in the scene.
/// Automatically disables duplicate AudioListeners to prevent Unity warnings.
/// </summary>
public class AudioListenerManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[AudioListenerManager]";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        EnsureSingleAudioListener();
    }

    private void Start()
    {
        // Double-check in Start in case other objects spawn AudioListeners
        EnsureSingleAudioListener();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensure only one AudioListener is active in the scene.
    /// Keeps the first one found (usually Main Camera from persistent prefab).
    /// </summary>
    private void EnsureSingleAudioListener()
    {
        // Find all AudioListeners in the scene (include inactive)
        AudioListener[] audioListeners = Object.FindObjectsOfType<AudioListener>(true);

        if (audioListeners.Length <= 1)
        {
            // Only one or zero AudioListeners - no problem
            return;
        }

        // Multiple AudioListeners found - keep only the first one active
        AudioListener firstListener = audioListeners[0];
        int disabledCount = 0;

        for (int i = 1; i < audioListeners.Length; i++)
        {
            AudioListener listener = audioListeners[i];
            if (listener.enabled)
            {
                listener.enabled = false;
                disabledCount++;

                Debug.Log($"{c_LogPrefix} Disabled duplicate AudioListener on '{listener.gameObject.name}' " +
                    $"(kept AudioListener on '{firstListener.gameObject.name}')");
            }
        }

        if (disabledCount > 0)
        {
            Debug.Log($"{c_LogPrefix} Fixed duplicate AudioListener warning: disabled {disabledCount} duplicate(s), " +
                $"kept AudioListener on '{firstListener.gameObject.name}'");
        }
    }
    #endregion
}

