using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the list of enemy types encountered in the match.
/// </summary>
public class EnemyListManager : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Transform m_EnemyListContainer;
    [SerializeField] private GameObject m_EnemyListItemPrefab;
    [SerializeField] private GameObject m_EnemyListItemAvatarPrefab;
    [SerializeField] private GameObject m_EmptyMessage;
    [SerializeField] private bool m_UseAvatarPrefab = true;

    private List<MonoBehaviour> m_EnemyListItems = new List<MonoBehaviour>();
    #endregion

    #region Public Methods
    /// <summary>
    /// Populate the enemy list with enemy data from match result.
    /// </summary>
    public void PopulateEnemies(List<GameResult.EnemyTypeInfo> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            ShowEmptyMessage(true);
            ClearEnemyList();
            return;
        }

        ShowEmptyMessage(false);

        // Clear existing items
        ClearEnemyList();

        // Create items for each enemy
        GameObject prefabToUse = m_UseAvatarPrefab && m_EnemyListItemAvatarPrefab != null 
            ? m_EnemyListItemAvatarPrefab 
            : m_EnemyListItemPrefab;

        if (prefabToUse != null && m_EnemyListContainer != null)
        {
            foreach (var enemy in enemies)
            {
                GameObject itemObj = Instantiate(prefabToUse, m_EnemyListContainer);
                
                // Try EnemyListItemAvatar first (new prefab)
                EnemyListItemAvatar avatarItem = itemObj.GetComponent<EnemyListItemAvatar>();
                if (avatarItem != null)
                {
                    avatarItem.Initialize(enemy);
                    m_EnemyListItems.Add(avatarItem);
                    continue;
                }

                // Fallback to EnemyListItem (old prefab)
                EnemyListItem item = itemObj.GetComponent<EnemyListItem>();
                if (item != null)
                {
                    item.Initialize(enemy);
                    m_EnemyListItems.Add(item);
                }
                else
                {
                    Debug.LogWarning("[EnemyListManager] Prefab does not have EnemyListItemAvatar or EnemyListItem component");
                }
            }
        }
        else
        {
            Debug.LogWarning("[EnemyListManager] Prefab or container not assigned");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Clear all enemy list items.
    /// </summary>
    private void ClearEnemyList()
    {
        foreach (var item in m_EnemyListItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        m_EnemyListItems.Clear();
    }

    /// <summary>
    /// Show or hide empty message.
    /// </summary>
    private void ShowEmptyMessage(bool show)
    {
        if (m_EmptyMessage != null)
        {
            m_EmptyMessage.SetActive(show);
        }
    }
    #endregion
}

