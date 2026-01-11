using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads game configuration from shared/game-config.json.
/// Only loads expCurve and polling settings.
/// Player stats and enemy configs are now loaded from the server database.
/// </summary>
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
                Debug.Log($"[GameConfigLoader] Config loaded from {path} (expCurve, polling only)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameConfigLoader] Failed to load config: {ex.Message}");
        }
    }
    #endregion
}

#region Config Models
/// <summary>
/// Game configuration data structure.
/// Only contains expCurve and polling settings.
/// Player stats and enemy configs are loaded from the server database.
/// </summary>
[Serializable]
public class GameConfigData
{
    public ExpCurveData expCurve = new ExpCurveData();
    public PollingSettingsData polling = new PollingSettingsData();
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
