using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying a single enemy type in the result list with avatar sprite.
/// </summary>
public class EnemyListItemAvatar : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Image m_AvatarImage;
    [SerializeField] private TMP_Text m_EnemyTypeText;
    [SerializeField] private TMP_Text m_SectionNameText;
    [SerializeField] private TMP_Text m_CheckpointNameText;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Ensure EnemySpriteManager exists
        if (EnemySpriteManager.Instance == null)
        {
            var go = new GameObject("EnemySpriteManager");
            go.AddComponent<EnemySpriteManager>();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Initialize the enemy list item with enemy data.
    /// </summary>
    public void Initialize(GameResult.EnemyTypeInfo enemyInfo)
    {
        if (enemyInfo == null)
        {
            Debug.LogWarning("[EnemyListItemAvatar] Enemy info is null");
            return;
        }

        // Get display name from server, fallback to capitalized typeId if name is empty
        string displayName = "Unknown";
        if (!string.IsNullOrEmpty(enemyInfo.name))
        {
            displayName = enemyInfo.name;
        }
        else if (!string.IsNullOrEmpty(enemyInfo.enemyTypeId))
        {
            // Fallback: use EnemySpriteManager or capitalize first letter
            if (EnemySpriteManager.Instance != null)
            {
                displayName = EnemySpriteManager.Instance.GetEnemyDisplayName(enemyInfo.enemyTypeId);
            }
            else
            {
                if (enemyInfo.enemyTypeId.Length > 1)
                {
                    displayName = char.ToUpper(enemyInfo.enemyTypeId[0]) + enemyInfo.enemyTypeId.Substring(1);
                }
                else
                {
                    displayName = enemyInfo.enemyTypeId.ToUpper();
                }
            }
        }

        // Display enemy name (user-readable)
        if (m_EnemyTypeText != null)
        {
            m_EnemyTypeText.text = displayName;
        }

        // Load and display sprite based on type_id
        if (m_AvatarImage != null && EnemySpriteManager.Instance != null)
        {
            Sprite enemySprite = EnemySpriteManager.Instance.GetEnemySprite(enemyInfo.enemyTypeId);
            if (enemySprite != null)
            {
                m_AvatarImage.sprite = enemySprite;
            }
            else
            {
                // Hide image if sprite not found
                m_AvatarImage.gameObject.SetActive(false);
            }
        }

        // Display section name
        if (m_SectionNameText != null)
        {
            m_SectionNameText.text = enemyInfo.sectionName ?? "N/A";
        }

        // Display checkpoint name (optional)
        if (m_CheckpointNameText != null)
        {
            if (!string.IsNullOrEmpty(enemyInfo.checkpointName))
            {
                m_CheckpointNameText.text = enemyInfo.checkpointName;
                m_CheckpointNameText.gameObject.SetActive(true);
            }
            else
            {
                m_CheckpointNameText.gameObject.SetActive(false);
            }
        }
    }
    #endregion
}

