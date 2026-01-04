using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles player death: shows Game Over UI or respawns player.
/// This should be on a separate GameObject (not the Player) so it stays active when player dies.
/// Recommended: Add to GameManager or a dedicated "DeathManager" object with DontDestroyOnLoad.
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    #region Constants
    private const float c_DefaultRespawnDelay = 3f;
    #endregion

    #region Private Fields
    [Header("Death Handling Mode")]
    [Tooltip("If true, player respawns after delay. If false, shows Game Over UI.")]
    [SerializeField] private bool m_UseRespawn = true;
    [SerializeField] private float m_RespawnDelay = c_DefaultRespawnDelay;
    [SerializeField] private Vector3 m_RespawnPosition = Vector3.zero;

    [Header("Game Over UI (if not using respawn)")]
    [SerializeField] private CanvasGroup m_GameOverCanvas;
    [SerializeField] private string m_RestartSceneName = "RPG";

    [Header("References")]
    [SerializeField] private GameObject m_PlayerObject;

    private Coroutine respawnCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Cache player reference early, before it can be deactivated
        FindAndCachePlayer();
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDied += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDeath;
    }

    private void Start()
    {
        // Try again in Start if not found in Awake
        if (m_PlayerObject == null)
        {
            FindAndCachePlayer();
        }

        // Hide game over canvas at start
        if (m_GameOverCanvas != null)
        {
            m_GameOverCanvas.alpha = 0;
            m_GameOverCanvas.blocksRaycasts = false;
            m_GameOverCanvas.interactable = false;
        }
    }

    private void FindAndCachePlayer()
    {
        if (m_PlayerObject != null) return;

        // Try FindWithTag first (only finds active objects)
        m_PlayerObject = GameObject.FindWithTag("Player");

        // If not found, search all PlayerHealth components (including inactive)
        if (m_PlayerObject == null)
        {
            var allPlayerHealth = Resources.FindObjectsOfTypeAll<PlayerHealth>();
            foreach (var ph in allPlayerHealth)
            {
                // Skip prefabs (only get scene objects)
                if (ph.gameObject.scene.IsValid())
                {
                    m_PlayerObject = ph.gameObject;
                    break;
                }
            }
        }

        if (m_PlayerObject != null)
        {
            Debug.Log($"[PlayerDeathHandler] Found player: {m_PlayerObject.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerDeathHandler] Player not found! Please assign m_PlayerObject in Inspector.");
        }
    }
    #endregion

    #region Private Methods
    private void HandlePlayerDeath()
    {
        Debug.Log("[PlayerDeathHandler] Player died!");

        // Try to cache player reference from the event sender if we don't have it
        if (m_PlayerObject == null)
        {
            FindAndCachePlayer();
        }

        if (m_UseRespawn)
        {
            if (respawnCoroutine != null)
                StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnAfterDelay());
        }
        else
        {
            ShowGameOver();
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        Debug.Log($"[PlayerDeathHandler] Respawning in {m_RespawnDelay} seconds...");

        // Use unscaled time in case Time.timeScale = 0
        yield return new WaitForSecondsRealtime(m_RespawnDelay);

        RespawnPlayer();
    }

    private void RespawnPlayer()
    {
        if (m_PlayerObject == null)
        {
            Debug.LogError("[PlayerDeathHandler] Player object not found!");
            return;
        }

        // Reset position before activating
        m_PlayerObject.transform.position = m_RespawnPosition;

        // Get PlayerHealth and call Respawn
        var playerHealth = m_PlayerObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Respawn();
        }
        else
        {
            // Fallback: just activate and reset health manually
            StatsManager.Instance.currentHealth = StatsManager.Instance.maxHealth;
            m_PlayerObject.SetActive(true);
        }

        Debug.Log("[PlayerDeathHandler] Player respawned!");
    }

    private void ShowGameOver()
    {
        Debug.Log("[PlayerDeathHandler] Game Over!");

        if (m_GameOverCanvas != null)
        {
            m_GameOverCanvas.alpha = 1;
            m_GameOverCanvas.blocksRaycasts = true;
            m_GameOverCanvas.interactable = true;
        }

        // Pause game
        Time.timeScale = 0;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Called by Restart button in Game Over UI.
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1;

        // Hide game over UI
        if (m_GameOverCanvas != null)
        {
            m_GameOverCanvas.alpha = 0;
            m_GameOverCanvas.blocksRaycasts = false;
            m_GameOverCanvas.interactable = false;
        }

        if (!string.IsNullOrEmpty(m_RestartSceneName))
        {
            SceneManager.LoadScene(m_RestartSceneName);
        }
    }

    /// <summary>
    /// Force respawn player immediately (can be called from other scripts or UI buttons).
    /// </summary>
    public void ForceRespawn()
    {
        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);

        // Hide game over if shown
        if (m_GameOverCanvas != null)
        {
            m_GameOverCanvas.alpha = 0;
            m_GameOverCanvas.blocksRaycasts = false;
            m_GameOverCanvas.interactable = false;
        }

        Time.timeScale = 1;
        RespawnPlayer();
    }
    #endregion
}
