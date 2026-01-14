using System.Collections;
using UnityEngine;

/// <summary>
/// Handles spawning the local player with the correct character type.
/// Replaces or configures the existing player object based on character selection.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[PlayerSpawner]";
    private const float c_DefaultSpawnX = -16f;
    private const float c_DefaultSpawnY = 12f;
    #endregion

    #region Private Fields
    [Header("Player Prefabs")]
    [SerializeField] private GameObject m_LancerPrefab;
    [SerializeField] private GameObject m_WarriousPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Vector2 m_SpawnPosition = new Vector2(c_DefaultSpawnX, c_DefaultSpawnY);
    [SerializeField] private Transform m_PlayerContainer;

    [Header("Settings")]
    [SerializeField] private bool m_EnableLogging = true;
    [SerializeField] private bool m_ReplaceExistingPlayer = true;

    // Spawned player reference
    private GameObject m_LocalPlayer;
    #endregion

    #region Public Properties
    public static PlayerSpawner Instance { get; private set; }

    /// <summary>
    /// Get the spawned local player.
    /// </summary>
    public GameObject LocalPlayer => m_LocalPlayer;

    /// <summary>
    /// Get spawn position.
    /// </summary>
    public Vector2 SpawnPosition => m_SpawnPosition;
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
        // Try to find existing player
        m_LocalPlayer = GameObject.FindGameObjectWithTag("Player");

        if (m_LocalPlayer != null)
        {
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Found existing player: {m_LocalPlayer.name}");
            }

            // Auto-configure player with selected character type from NetClient
            if (!m_ReplaceExistingPlayer && NetClient.Instance != null)
            {
                string characterType = NetClient.Instance.SelectedCharacterType;
                if (!string.IsNullOrEmpty(characterType))
                {
                    Debug.Log($"{c_LogPrefix} Auto-configuring player for character: {characterType}");
                    ConfigureExistingPlayer(m_LocalPlayer, characterType);
                }
                else
                {
                    Debug.LogWarning($"{c_LogPrefix} No character type selected, using default");
                }
            }
        }
        else
        {
            // No player found - this is OK, PlayerSpawner.SpawnLocalPlayer() will be called by GameSceneInitializer
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} No player found in scene. Will be spawned by GameSceneInitializer.");
            }
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
    /// Spawn or configure the local player with the specified character type.
    /// </summary>
    public IEnumerator SpawnLocalPlayer(string characterType)
    {
        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Spawning local player: {characterType}");
        }

        // Get prefab for character type
        GameObject prefab = GetPrefabForCharacterType(characterType);

        if (prefab == null)
        {
            Debug.LogError($"{c_LogPrefix} No prefab found for character type: {characterType}");
            yield break;
        }

        // Check for existing player
        var existingPlayer = GameObject.FindGameObjectWithTag("Player");

        if (existingPlayer != null)
        {
            if (m_ReplaceExistingPlayer)
            {
                if (m_EnableLogging)
                {
                    Debug.Log($"{c_LogPrefix} Replacing existing player: {existingPlayer.name}");
                }

                // Store reference to old player's data if needed
                var oldPosition = existingPlayer.transform.position;

                // Destroy old player
                Destroy(existingPlayer);
                yield return null; // Wait a frame for destruction

                // Spawn new player
                m_LocalPlayer = SpawnPlayer(prefab, m_SpawnPosition);
            }
            else
            {
                // Keep existing player, just configure it
                m_LocalPlayer = existingPlayer;
                ConfigureExistingPlayer(existingPlayer, characterType);
            }
        }
        else
        {
            // No existing player, spawn new one
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} No existing player found, spawning new player: {characterType}");
            }
            m_LocalPlayer = SpawnPlayer(prefab, m_SpawnPosition);
        }

        // Ensure player is active and enabled
        if (m_LocalPlayer != null)
        {
            m_LocalPlayer.SetActive(true);

            // Enable all components
            var components = m_LocalPlayer.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp != null)
                {
                    comp.enabled = true;
                }
            }

            // Ensure Rigidbody2D is not kinematic (for movement)
            var rb = m_LocalPlayer.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }

        // Add required components for networking
        EnsureNetworkComponents();

        if (m_EnableLogging)
        {
            Debug.Log($"{c_LogPrefix} Local player spawned: {m_LocalPlayer?.name}");
        }
    }

    /// <summary>
    /// Get the local player or find it if not cached.
    /// </summary>
    public GameObject GetLocalPlayer()
    {
        if (m_LocalPlayer == null)
        {
            m_LocalPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        return m_LocalPlayer;
    }

    /// <summary>
    /// Respawn the local player at spawn position.
    /// </summary>
    public void RespawnLocalPlayer()
    {
        if (m_LocalPlayer != null)
        {
            m_LocalPlayer.transform.position = new Vector3(m_SpawnPosition.x, m_SpawnPosition.y, 0);

            // Reset prediction
            var predictor = m_LocalPlayer.GetComponent<ClientPredictor>();
            if (predictor != null)
            {
                predictor.ForcePosition(m_LocalPlayer.transform.position);
            }

            // Reset interpolation
            var interpolator = m_LocalPlayer.GetComponent<StateInterpolator>();
            if (interpolator != null)
            {
                interpolator.ForcePosition(m_LocalPlayer.transform.position);
            }

            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Player respawned at {m_SpawnPosition}");
            }
        }
    }
    #endregion

    #region Private Methods
    private GameObject GetPrefabForCharacterType(string characterType)
    {
        string lowerType = characterType?.ToLower()?.Trim() ?? "";
        Debug.Log($"{c_LogPrefix} GetPrefabForCharacterType: input='{characterType}', normalized='{lowerType}'");

        GameObject prefab = null;

        switch (lowerType)
        {
            case "lancer":
                prefab = m_LancerPrefab;
                break;
            case "warrious":
            case "warrior":
                prefab = m_WarriousPrefab;
                break;
            default:
                Debug.LogWarning($"{c_LogPrefix} Unknown character type: '{characterType}', using lancer");
                prefab = m_LancerPrefab;
                break;
        }

        if (prefab == null)
        {
            Debug.LogError($"{c_LogPrefix} Prefab for '{lowerType}' is NULL! Check Inspector assignment.");
        }
        else
        {
            Debug.Log($"{c_LogPrefix} Returning prefab: {prefab.name}");
        }

        return prefab;
    }

    private GameObject SpawnPlayer(GameObject prefab, Vector2 position)
    {
        var spawnPos = new Vector3(position.x, position.y, 0);
        Transform parent = m_PlayerContainer;

        var player = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
        player.name = "Player_Local";
        player.tag = "Player";

        // Mark as persistent (don't destroy on scene load)
        if (player.transform.parent == null)
        {
            DontDestroyOnLoad(player);
        }

        return player;
    }

    private void ConfigureExistingPlayer(GameObject player, string characterType)
    {
        Debug.Log($"{c_LogPrefix} Configuring existing player for character: '{characterType}'");

        // Get Animator and swap controller based on character type
        var animator = player.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"{c_LogPrefix} Player has no Animator component!");
            return;
        }

        RuntimeAnimatorController controller = GetAnimatorForCharacterType(characterType);
        if (controller == null)
        {
            Debug.LogError($"{c_LogPrefix} Failed to get animator for character: '{characterType}'");
            return;
        }

        Debug.Log($"{c_LogPrefix} Swapping animator to: {controller.name}");
        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Update(0f);
        Debug.Log($"{c_LogPrefix} Successfully applied animator for {characterType}");

        // Store character type in NetClient for server sync
        if (NetClient.Instance != null)
        {
            NetClient.Instance.SelectedCharacterType = characterType;
        }
    }

    /// <summary>
    /// Get animator controller for character type from prefab or Resources.
    /// </summary>
    private RuntimeAnimatorController GetAnimatorForCharacterType(string characterType)
    {
        Debug.Log($"{c_LogPrefix} GetAnimatorForCharacterType: '{characterType}'");

        GameObject prefab = GetPrefabForCharacterType(characterType);
        if (prefab == null)
        {
            Debug.LogError($"{c_LogPrefix} Prefab is null for character: '{characterType}'! Make sure m_LancerPrefab and m_WarriousPrefab are assigned in Inspector.");
            return null;
        }

        var prefabAnimator = prefab.GetComponent<Animator>();
        if (prefabAnimator == null)
        {
            Debug.LogError($"{c_LogPrefix} Prefab '{prefab.name}' has no Animator component!");
            return null;
        }

        var controller = prefabAnimator.runtimeAnimatorController;
        if (controller == null)
        {
            Debug.LogError($"{c_LogPrefix} Prefab '{prefab.name}' Animator has no RuntimeAnimatorController!");
            return null;
        }

        Debug.Log($"{c_LogPrefix} Found controller '{controller.name}' from prefab '{prefab.name}'");
        return controller;
    }

    private void EnsureNetworkComponents()
    {
        if (m_LocalPlayer == null)
            return;

        // Ensure ClientPredictor is attached (for local player prediction)
        var predictor = m_LocalPlayer.GetComponent<ClientPredictor>();
        if (predictor == null)
        {
            predictor = m_LocalPlayer.AddComponent<ClientPredictor>();
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Added ClientPredictor to player");
            }
        }

        // Ensure StateInterpolator is attached BUT marked as local player
        // Local player uses ClientPredictor for movement, not interpolation
        var interpolator = m_LocalPlayer.GetComponent<StateInterpolator>();
        if (interpolator == null)
        {
            interpolator = m_LocalPlayer.AddComponent<StateInterpolator>();
            if (m_EnableLogging)
            {
                Debug.Log($"{c_LogPrefix} Added StateInterpolator to player");
            }
        }

        // Mark as local player to disable position updates via interpolation
        interpolator.SetIsLocalPlayer(true);
    }
    #endregion
}

