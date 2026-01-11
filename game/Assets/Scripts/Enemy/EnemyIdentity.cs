using System;
using UnityEngine;

/// <summary>
/// Component that stores the server-assigned enemy ID for an enemy GameObject.
/// Used to link client-side enemies with server state.
/// </summary>
public class EnemyIdentity : MonoBehaviour
{
    #region Public Properties
    public Guid EnemyId { get; private set; }
    public string EnemyTypeId { get; private set; }
    public bool IsInitialized => EnemyId != Guid.Empty;
    #endregion

    #region Public Methods
    /// <summary>
    /// Initialize enemy identity with server-assigned ID and typeId.
    /// Called by EnemySpawner when spawning enemies.
    /// </summary>
    public void Initialize(Guid enemyId, string typeId)
    {
        if (enemyId == Guid.Empty)
        {
            Debug.LogError("[EnemyIdentity] Cannot initialize with empty Guid");
            return;
        }

        EnemyId = enemyId;
        EnemyTypeId = typeId ?? "unknown";
        
        Debug.Log($"[EnemyIdentity] Initialized: {enemyId} ({typeId})");
    }
    #endregion
}

