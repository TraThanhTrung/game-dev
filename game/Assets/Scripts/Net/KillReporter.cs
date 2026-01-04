using System.Collections;
using UnityEngine;

/// <summary>
/// Handles reporting enemy kills to the server.
/// Subscribes to Enemy_Health.OnMonsterDefeated event and reports kills to server.
/// </summary>
public class KillReporter : MonoBehaviour
{
    #region Unity Lifecycle
    private void Awake()
    {
        Debug.Log("[KillReporter] Awake called");
    }

    private void Start()
    {
        Debug.Log("[KillReporter] Start called");
    }

    private void OnEnable()
    {
        Debug.Log("[KillReporter] OnEnable called - subscribing to OnMonsterDefeated event");
        Enemy_Health.OnMonsterDefeated += OnEnemyDefeated;
        Debug.Log("[KillReporter] Subscribed to OnMonsterDefeated event");
    }

    private void OnDisable()
    {
        Debug.Log("[KillReporter] OnDisable called - unsubscribing from OnMonsterDefeated event");
        Enemy_Health.OnMonsterDefeated -= OnEnemyDefeated;
    }
    #endregion

    #region Private Methods
    private void OnEnemyDefeated(string enemyTypeId)
    {
        Debug.Log($"[KillReporter] OnEnemyDefeated called with enemyTypeId: {enemyTypeId}");
        
        if (NetClient.Instance == null)
        {
            Debug.LogError("[KillReporter] NetClient.Instance is null!");
            return;
        }
        
        if (!NetClient.Instance.IsConnected)
        {
            Debug.LogWarning($"[KillReporter] Not connected (PlayerId: {NetClient.Instance.PlayerId}, Token: {NetClient.Instance.Token}), cannot report kill");
            return;
        }

        Debug.Log($"[KillReporter] Reporting kill: {enemyTypeId} for player {NetClient.Instance.PlayerId}");
        StartCoroutine(NetClient.Instance.ReportKill(enemyTypeId,
            res =>
            {
                if (res != null && res.granted)
                {
                    Debug.Log($"[KillReporter] Kill reported successfully: {enemyTypeId} -> Exp: {res.exp}, Gold: {res.gold}, Level: {res.level}");
                    // State will be synced via polling, no need to update locally
                }
                else
                {
                    Debug.LogWarning($"[KillReporter] Kill report not granted (res: {res})");
                }
            },
            err => Debug.LogError($"[KillReporter] Kill report failed: {err}")));
    }
    #endregion
}

