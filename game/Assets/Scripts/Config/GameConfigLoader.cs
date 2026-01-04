using System;
using System.IO;
using UnityEngine;

public class GameConfigLoader : MonoBehaviour
{
    #region Constants
    private const string c_DefaultRelativePath = "../../shared/game-config.json";
    #endregion

    #region Private Fields
    [SerializeField] private string m_ConfigRelativePath = c_DefaultRelativePath;
    #endregion

    #region Public Properties
    public static GameConfigLoader Instance { get; private set; }
    public GameConfigData Config { get; private set; }
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
        LoadConfig();
    }

    /// <summary>
    /// Ensures GameConfigLoader instance exists. Creates one if it doesn't exist.
    /// Can be called from other scripts that need config before Awake/Start.
    /// </summary>
    public static void EnsureInstance()
    {
        if (Instance != null) return;

        var go = new GameObject("GameConfigLoader");
        go.AddComponent<GameConfigLoader>();
        // Awake will be called automatically, which sets Instance and loads config
    }
    #endregion

    #region Private Methods
    private void LoadConfig()
    {
        Config = new GameConfigData();

        var path = Path.GetFullPath(Path.Combine(Application.dataPath, m_ConfigRelativePath));
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameConfigLoader] Config not found at {path}. Using defaults.");
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var parsed = JsonUtility.FromJson<GameConfigData>(json);
            if (parsed != null)
            {
                Config = parsed;
                Debug.Log($"[GameConfigLoader] Config loaded from {path}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameConfigLoader] Failed to load config: {ex.Message}");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get enemy config by typeId. Returns null if not found.
    /// </summary>
    public EnemyConfigData GetEnemyConfig(string typeId)
    {
        if (Config == null || Config.enemyStats == null)
            return null;

        foreach (var enemy in Config.enemyStats)
        {
            if (enemy.typeId == typeId)
                return enemy;
        }

        return null;
    }
    #endregion
}

#region Config Models
[Serializable]
public class GameConfigData
{
    public PlayerDefaultsData playerDefaults = new PlayerDefaultsData();
    public ExpCurveData expCurve = new ExpCurveData();
    public PollingSettingsData polling = new PollingSettingsData();
    public EnemyConfigData[] enemyStats = Array.Empty<EnemyConfigData>();
}

[Serializable]
public class PlayerDefaultsData
{
    public int level = 1;
    public int exp = 0;
    public int gold = 100;
    public float spawnX = -16f;
    public float spawnY = 12f;
    public PlayerStatBlockData stats = new PlayerStatBlockData();
}

[Serializable]
public class PlayerStatBlockData
{
    public int damage = 10;
    public float weaponRange = 1.5f;
    public float knockbackForce = 5f;
    public float knockbackTime = 0.2f;
    public float stunTime = 0.3f;
    public float speed = 4f;
    public int maxHealth = 50;
    public int currentHealth = 50;
    public float bonusDamagePercent = 0f;
    public float damageReductionPercent = 0f;
}

[Serializable]
public class EnemyConfigData
{
    public string typeId = "enemy";
    public int expReward = 25;
    public int goldReward = 10;
    public int maxHealth = 30;
    public int damage = 5;
    public float speed = 2f;
    public float detectRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 2f;
    public float weaponRange = 1.2f;
    public float knockbackForce = 5f;
    public float stunTime = 0.3f;
    public float respawnDelay = 5f;
}

[Serializable]
public class ExpCurveData
{
    public int basePerLevel = 10;
    public float growthMultiplier = 0.2f;
    public int levelCap = 100;
}

[Serializable]
public class PollingSettingsData
{
    public float stateIntervalSeconds = 0.2f;
    public float lerpSpeed = 15f;
    public float positionChangeThreshold = 0.01f;
}
#endregion

