using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject to map enemy type_id to sprites.
/// Create an instance and assign sprites in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "EnemySpriteData", menuName = "Game/Enemy Sprite Data")]
public class EnemySpriteData : ScriptableObject
{
    #region Private Fields
    [System.Serializable]
    public class EnemySpriteEntry
    {
        public string typeId;
        public Sprite sprite;
    }

    [SerializeField] private List<EnemySpriteEntry> m_EnemySprites = new List<EnemySpriteEntry>();
    #endregion

    #region Public Methods
    /// <summary>
    /// Get sprite for enemy type_id.
    /// </summary>
    public Sprite GetSprite(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId) || m_EnemySprites == null)
            return null;

        foreach (var entry in m_EnemySprites)
        {
            if (entry != null && entry.typeId == typeId && entry.sprite != null)
            {
                return entry.sprite;
            }
        }

        return null;
    }
    #endregion
}

