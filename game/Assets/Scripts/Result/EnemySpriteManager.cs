using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages enemy sprites based on type_id.
/// Uses EnemySpriteData ScriptableObject for sprite mapping.
/// </summary>
public class EnemySpriteManager : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private EnemySpriteData m_SpriteData;
    private Dictionary<string, Sprite> m_SpriteCache = new Dictionary<string, Sprite>();
    #endregion

    #region Public Properties
    public static EnemySpriteManager Instance { get; private set; }
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

        // Try to load EnemySpriteData from Resources if not assigned
        if (m_SpriteData == null)
        {
            m_SpriteData = Resources.Load<EnemySpriteData>("Config/EnemySpriteData");
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get sprite for enemy type_id. Uses EnemySpriteData ScriptableObject.
    /// Returns null if sprite not found.
    /// </summary>
    public Sprite GetEnemySprite(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        // Check cache first
        if (m_SpriteCache.TryGetValue(typeId, out var cachedSprite))
        {
            return cachedSprite;
        }

        Sprite sprite = null;

        // Try to get from EnemySpriteData
        if (m_SpriteData != null)
        {
            sprite = m_SpriteData.GetSprite(typeId);
        }

        // If not found in ScriptableObject, try Resources as fallback
        if (sprite == null)
        {
            // Try with _Idle suffix first (most common)
            string resourcePath = $"EnemySprites/{typeId}_Idle";
            sprite = Resources.Load<Sprite>(resourcePath);

            // If not found, try without suffix
            if (sprite == null)
            {
                resourcePath = $"EnemySprites/{typeId}";
                sprite = Resources.Load<Sprite>(resourcePath);
            }

            // If still not found, try with capitalized first letter
            if (sprite == null && !string.IsNullOrEmpty(typeId))
            {
                string capitalizedTypeId = char.ToUpper(typeId[0]) + typeId.Substring(1);
                resourcePath = $"EnemySprites/{capitalizedTypeId}_Idle";
                sprite = Resources.Load<Sprite>(resourcePath);

                if (sprite == null)
                {
                    resourcePath = $"EnemySprites/{capitalizedTypeId}";
                    sprite = Resources.Load<Sprite>(resourcePath);
                }
            }
        }

        // Cache the result (even if null, to avoid repeated lookups)
        m_SpriteCache[typeId] = sprite;

        if (sprite == null)
        {
            Debug.LogWarning($"[EnemySpriteManager] Sprite not found for typeId: {typeId}. " +
                $"Please create an EnemySpriteData ScriptableObject and assign sprites.");
        }

        return sprite;
    }

    /// <summary>
    /// Get display name for enemy type_id (converts "slime" to "Slime", etc.)
    /// </summary>
    public string GetEnemyDisplayName(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return "Unknown";

        // Capitalize first letter
        return char.ToUpper(typeId[0]) + typeId.Substring(1);
    }

    /// <summary>
    /// Clear sprite cache.
    /// </summary>
    public void ClearCache()
    {
        m_SpriteCache.Clear();
    }
    #endregion
}

