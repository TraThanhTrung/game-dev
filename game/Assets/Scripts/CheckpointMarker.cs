using UnityEngine;

/// <summary>
/// Component để đánh dấu GameObject là Checkpoint marker trong scene.
/// Sử dụng với CheckpointExporter để export vào database.
/// </summary>
public class CheckpointMarker : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[CheckpointMarker]";
    #endregion

    #region Public Fields
    [Header("Checkpoint Info")]
    [Tooltip("Tên checkpoint (sẽ dùng làm CheckpointName trong DB)")]
    public string checkpointName = "New Checkpoint";

    [Tooltip("Section ID mà checkpoint này thuộc về")]
    public int sectionId = 1;

    [Tooltip("Enemy types sẽ spawn tại checkpoint này (JSON array format)")]
    public string enemyPool = "[\"slime\"]";

    [Tooltip("Số lượng enemies tối đa tại checkpoint này")]
    [Range(1, 10)]
    public int maxEnemies = 1;

    [Tooltip("Checkpoint có active không")]
    public bool isActive = true;

    [Header("Auto Settings")]
    [Tooltip("Tự động lấy tên từ GameObject name")]
    public bool autoNameFromGameObject = true;

    [Tooltip("Tự động lấy vị trí từ Transform")]
    public bool autoPositionFromTransform = true;
    #endregion

    #region Private Fields
    private Vector2 m_LastPosition;
    #endregion

    #region Unity Lifecycle
    private void OnValidate()
    {
        // Auto-update name from GameObject
        if (autoNameFromGameObject && !string.IsNullOrEmpty(gameObject.name))
        {
            checkpointName = gameObject.name;
        }

        // Auto-update position from Transform
        if (autoPositionFromTransform)
        {
            m_LastPosition = new Vector2(transform.position.x, transform.position.y);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw gizmo để dễ nhìn trong Scene view
        Gizmos.color = isActive ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw line to show direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1f);
    }

    private void OnDrawGizmosSelected()
    {
        // Highlight khi selected
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.7f);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Lấy vị trí checkpoint (X, Y) từ Transform.
    /// </summary>
    public Vector2 GetPosition()
    {
        return new Vector2(transform.position.x, transform.position.y);
    }

    /// <summary>
    /// Validate checkpoint data trước khi export.
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(checkpointName))
        {
            errorMessage = "CheckpointName không được để trống";
            return false;
        }

        if (sectionId <= 0)
        {
            errorMessage = "SectionId phải > 0";
            return false;
        }

        if (string.IsNullOrWhiteSpace(enemyPool))
        {
            errorMessage = "EnemyPool không được để trống";
            return false;
        }

        if (maxEnemies <= 0)
        {
            errorMessage = "MaxEnemies phải > 0";
            return false;
        }

        // Validate JSON format (simple check)
        if (!enemyPool.TrimStart().StartsWith("[") || !enemyPool.TrimEnd().EndsWith("]"))
        {
            errorMessage = "EnemyPool phải là JSON array format (ví dụ: [\"slime\", \"goblin\"])";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
    #endregion
}


