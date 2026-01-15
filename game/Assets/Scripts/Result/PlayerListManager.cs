using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the list of players in the match.
/// </summary>
public class PlayerListManager : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Transform m_PlayerListContainer;
    [SerializeField] private GameObject m_PlayerListItemPrefab;
    [SerializeField] private GameObject m_EmptyMessage;

    private List<PlayerListItem> m_PlayerListItems = new List<PlayerListItem>();
    #endregion

    #region Public Methods
    /// <summary>
    /// Populate the player list with player data from match result.
    /// </summary>
    public void PopulatePlayers(List<GameResult.PlayerInfo> players)
    {
        if (players == null || players.Count == 0)
        {
            ShowEmptyMessage(true);
            ClearPlayerList();
            return;
        }

        ShowEmptyMessage(false);

        // Clear existing items
        ClearPlayerList();

        // Create items for each player
        if (m_PlayerListItemPrefab != null && m_PlayerListContainer != null)
        {
            foreach (var player in players)
            {
                GameObject itemObj = Instantiate(m_PlayerListItemPrefab, m_PlayerListContainer);
                PlayerListItem item = itemObj.GetComponent<PlayerListItem>();
                if (item != null)
                {
                    item.Initialize(player);
                    m_PlayerListItems.Add(item);
                }
            }
        }
        else
        {
            Debug.LogWarning("[PlayerListManager] Prefab or container not assigned");
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Clear all player list items.
    /// </summary>
    private void ClearPlayerList()
    {
        foreach (var item in m_PlayerListItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        m_PlayerListItems.Clear();
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



