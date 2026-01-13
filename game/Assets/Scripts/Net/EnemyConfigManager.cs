using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages enemy configurations loaded from server database.
/// Loads configs via API and caches them in memory.
/// Replaces GameConfigLoader for enemy stats (but GameConfigLoader still used for player stats).
/// </summary>
public class EnemyConfigManager : MonoBehaviour
{
    #region Constants
    private const float c_DefaultCacheTimeout = 300f; // 5 minutes cache timeout
    #endregion

    #region Private Fields
    [SerializeField] private bool m_EnableLogging = true;
    [SerializeField] private bool m_AutoLoadOnStart = true;

    private Dictionary<string, EnemyConfig> m_ConfigCache = new Dictionary<string, EnemyConfig>();
    private bool m_IsLoading = false;
    private bool m_IsLoaded = false;
    private float m_CacheTime = 0f;
    #endregion

    #region Public Properties
    public static EnemyConfigManager Instance { get; private set; }
    public bool IsLoaded => m_IsLoaded;
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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (m_AutoLoadOnStart && NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            StartCoroutine(LoadAllEnemiesAsync());
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Load all enemy configs from server API.
    /// Called automatically on Start if AutoLoadOnStart is enabled and NetClient is connected.
    /// </summary>
    public IEnumerator LoadAllEnemiesAsync()
    {
        if (m_IsLoading)
        {
            if (m_EnableLogging)
                Debug.Log("[EnemyConfigManager] Already loading, skipping...");
            yield break;
        }

        // Enemy configs don't require authentication, so we can load even if NetClient is not connected
        // But we still need NetClient to get the base URL
        if (NetClient.Instance == null)
        {
            if (m_EnableLogging)
                Debug.LogWarning("[EnemyConfigManager] NetClient.Instance is null, using default URL");
        }

        m_IsLoading = true;
        m_IsLoaded = false;

        if (m_EnableLogging)
            Debug.Log("[EnemyConfigManager] Loading enemy configs from server...");

        var url = $"{GetBaseUrl()}/api/enemies";
        using (var req = UnityWebRequest.Get(url))
        {
            // No auth token needed - enemy configs are public data
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[EnemyConfigManager] Failed to load enemy configs: {req.responseCode} {req.error}");
                m_IsLoading = false;
                yield break;
            }

            if (string.IsNullOrEmpty(req.downloadHandler.text))
            {
                Debug.LogError("[EnemyConfigManager] Empty response from server");
                m_IsLoading = false;
                yield break;
            }

            try
            {
                // Parse JSON array response from server
                // Server returns: [{...}, {...}, ...]
                var json = req.downloadHandler.text;

                if (m_EnableLogging)
                    Debug.Log($"[EnemyConfigManager] Received JSON response (length: {json.Length})");

                // Parse JSON array using helper method
                var dtos = ParseEnemyConfigArray(json);

                if (dtos == null || dtos.Length == 0)
                {
                    Debug.LogError($"[EnemyConfigManager] Failed to parse enemy configs or empty array. JSON (first 200 chars): {json.Substring(0, Mathf.Min(200, json.Length))}...");
                    m_IsLoading = false;
                    yield break;
                }

                // Clear old cache
                m_ConfigCache.Clear();

                // Cache all configs
                int successCount = 0;
                foreach (var dto in dtos)
                {
                    if (dto == null || string.IsNullOrEmpty(dto.typeId))
                    {
                        if (m_EnableLogging)
                            Debug.LogWarning("[EnemyConfigManager] Skipping null or empty typeId enemy config");
                        continue;
                    }

                    try
                    {
                        m_ConfigCache[dto.typeId] = new EnemyConfig
                        {
                            typeId = dto.typeId,
                            expReward = dto.expReward,
                            goldReward = dto.goldReward,
                            maxHealth = dto.maxHealth,
                            damage = dto.damage,
                            speed = dto.speed,
                            detectRange = dto.detectRange,
                            attackRange = dto.attackRange,
                            attackCooldown = dto.attackCooldown,
                            weaponRange = dto.weaponRange,
                            knockbackForce = dto.knockbackForce,
                            stunTime = dto.stunTime,
                            respawnDelay = dto.respawnDelay
                        };
                        successCount++;

                        if (m_EnableLogging)
                            Debug.Log($"[EnemyConfigManager] ✓ Cached: {dto.typeId} | HP:{dto.maxHealth} DMG:{dto.damage} SPD:{dto.speed} EXP:{dto.expReward} GOLD:{dto.goldReward}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EnemyConfigManager] Failed to cache enemy config {dto?.typeId ?? "null"}: {ex.Message}");
                    }
                }

                m_CacheTime = Time.time;
                m_IsLoaded = true;

                if (m_EnableLogging)
                    Debug.Log($"[EnemyConfigManager] ✅ Successfully loaded {successCount}/{dtos.Length} enemy configs from server");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnemyConfigManager] ❌ Failed to parse enemy configs: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (m_EnableLogging && req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
                {
                    var json = req.downloadHandler.text;
                    Debug.LogError($"[EnemyConfigManager] JSON Response (first 500 chars): {json.Substring(0, Mathf.Min(500, json.Length))}");
                }
            }
        }

        m_IsLoading = false;
    }

    /// <summary>
    /// Get enemy config by typeId. Returns null if not found.
    /// If config is not in cache, attempts to load from server (async).
    /// </summary>
    public EnemyConfig GetEnemyConfig(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        // Check cache first
        if (m_ConfigCache.TryGetValue(typeId, out var config))
        {
            return config;
        }

        // Config not in cache - try to load single config from server (async fallback)
        if (!m_IsLoading)
        {
            if (m_EnableLogging)
                Debug.LogWarning($"[EnemyConfigManager] Config for {typeId} not in cache, loading from server...");

            // Trigger async load (will update cache when complete)
            StartCoroutine(LoadSingleEnemyAsync(typeId));
        }

        return null;
    }

    /// <summary>
    /// Clear cache and force reload from server.
    /// </summary>
    public void InvalidateCache()
    {
        m_ConfigCache.Clear();
        m_IsLoaded = false;
        m_CacheTime = 0f;

        if (m_EnableLogging)
            Debug.Log("[EnemyConfigManager] Cache invalidated");
    }

    /// <summary>
    /// Check if cache is still valid (not expired).
    /// </summary>
    public bool IsCacheValid()
    {
        if (!m_IsLoaded)
            return false;

        // Check if cache is expired
        if (Time.time - m_CacheTime > c_DefaultCacheTimeout)
        {
            if (m_EnableLogging)
                Debug.Log("[EnemyConfigManager] Cache expired, reloading...");
            return false;
        }

        return true;
    }
    #endregion

    #region Private Methods
    private string GetBaseUrl()
    {
        // Get base URL from NetClient (which uses ServerConfig)
        if (NetClient.Instance != null)
        {
            return NetClient.Instance.BaseUrl;
        }
        // Fallback
        return "http://localhost:5220";
    }

    /// <summary>
    /// Parse JSON array response from server into array of EnemyConfigDto.
    /// Unity's JsonUtility doesn't support arrays directly, so we wrap it.
    /// </summary>
    private EnemyConfigDto[] ParseEnemyConfigArray(string jsonArray)
    {
        if (string.IsNullOrWhiteSpace(jsonArray))
        {
            if (m_EnableLogging)
                Debug.LogWarning("[EnemyConfigManager] JSON array is null or empty");
            return new EnemyConfigDto[0];
        }

        try
        {
            // Trim whitespace and ensure it's a valid JSON array
            jsonArray = jsonArray.Trim();

            // Remove any leading/trailing whitespace or brackets if malformed
            if (!jsonArray.StartsWith("["))
            {
                Debug.LogError($"[EnemyConfigManager] JSON array doesn't start with '['. First 50 chars: {jsonArray.Substring(0, Mathf.Min(50, jsonArray.Length))}");
                return new EnemyConfigDto[0];
            }

            // Wrap array in object for JsonUtility parsing
            var wrappedJson = $"{{\"enemies\":{jsonArray}}}";
            var response = JsonUtility.FromJson<EnemyConfigResponse>(wrappedJson);

            if (response == null)
            {
                Debug.LogError("[EnemyConfigManager] Failed to parse wrapped JSON response - response is null");
                return new EnemyConfigDto[0];
            }

            return response.enemies ?? new EnemyConfigDto[0];
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EnemyConfigManager] Failed to parse JSON array: {ex.Message}");
            if (m_EnableLogging)
            {
                Debug.LogError($"[EnemyConfigManager] JSON array (first 200 chars): {jsonArray.Substring(0, Mathf.Min(200, jsonArray.Length))}...");
            }
            return new EnemyConfigDto[0];
        }
    }

    private IEnumerator LoadSingleEnemyAsync(string typeId)
    {
        if (m_IsLoading)
            yield break;

        m_IsLoading = true;

        var url = $"{GetBaseUrl()}/api/enemies/{typeId}";
        using (var req = UnityWebRequest.Get(url))
        {
            // No auth token needed - enemy configs are public data
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
            {
                try
                {
                    var json = req.downloadHandler.text;
                    var dto = JsonUtility.FromJson<EnemyConfigDto>(json);

                    if (dto == null)
                    {
                        Debug.LogError($"[EnemyConfigManager] Failed to parse enemy config for {typeId}: DTO is null");
                        if (m_EnableLogging)
                            Debug.LogError($"[EnemyConfigManager] JSON Response: {json}");
                    }
                    else if (string.IsNullOrEmpty(dto.typeId))
                    {
                        Debug.LogError($"[EnemyConfigManager] Enemy config for {typeId} has empty typeId");
                    }
                    else
                    {
                        m_ConfigCache[dto.typeId] = new EnemyConfig
                        {
                            typeId = dto.typeId,
                            expReward = dto.expReward,
                            goldReward = dto.goldReward,
                            maxHealth = dto.maxHealth,
                            damage = dto.damage,
                            speed = dto.speed,
                            detectRange = dto.detectRange,
                            attackRange = dto.attackRange,
                            attackCooldown = dto.attackCooldown,
                            weaponRange = dto.weaponRange,
                            knockbackForce = dto.knockbackForce,
                            stunTime = dto.stunTime,
                            respawnDelay = dto.respawnDelay
                        };

                        m_IsLoaded = true; // Mark as loaded even for single config

                        if (m_EnableLogging)
                            Debug.Log($"[EnemyConfigManager] Loaded config for {dto.typeId} from server: HP={dto.maxHealth}, Damage={dto.damage}, Speed={dto.speed}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EnemyConfigManager] Failed to parse enemy config for {typeId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                    if (m_EnableLogging && req.downloadHandler != null && !string.IsNullOrEmpty(req.downloadHandler.text))
                    {
                        var json = req.downloadHandler.text;
                        Debug.LogError($"[EnemyConfigManager] JSON Response: {json}");
                    }
                }
            }
            else
            {
                if (m_EnableLogging)
                    Debug.LogWarning($"[EnemyConfigManager] Failed to load config for {typeId}: {req.responseCode} {req.error}");
            }
        }

        m_IsLoading = false;
    }
    #endregion

    #region Data Classes
    [Serializable]
    public class EnemyConfig
    {
        public string typeId;
        public int expReward;
        public int goldReward;
        public int maxHealth;
        public int damage;
        public float speed;
        public float detectRange;
        public float attackRange;
        public float attackCooldown;
        public float weaponRange;
        public float knockbackForce;
        public float stunTime;
        public float respawnDelay;
    }


    [Serializable]
    private class EnemyConfigDto
    {
        public string typeId;
        public int expReward;
        public int goldReward;
        public int maxHealth;
        public int damage;
        public float speed;
        public float detectRange;
        public float attackRange;
        public float attackCooldown;
        public float weaponRange;
        public float knockbackForce;
        public float stunTime;
        public float respawnDelay;
    }

    [Serializable]
    private class EnemyConfigResponse
    {
        public EnemyConfigDto[] enemies;
    }
    #endregion
}

