using UnityEngine;

/// <summary>
/// Logs game state snapshots to the console.
/// Focused on logging only the local player's state to reduce spam.
/// </summary>
public class StateLogger : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private bool m_LogToConsole = true;
    #endregion

    #region Public Methods
    public void OnState(StateResponse state)
    {
        if (!m_LogToConsole || state == null) return;

        var myId = NetClient.Instance?.PlayerId.ToString();
        PlayerSnapshot me = null;

        if (state.players != null && !string.IsNullOrEmpty(myId))
        {
            foreach (var p in state.players)
            {
                if (p.id == myId)
                {
                    me = p;
                    break;
                }
            }
        }

        if (me != null)
        {
            Debug.Log($"v{state.version} | Me: pos=({me.x:0.0},{me.y:0.0}) hp={me.hp}/{me.maxHp} seq={me.sequence} | P:{state.players?.Length ?? 0} E:{state.enemies?.Length ?? 0}");
        }
        else
        {
            Debug.Log($"v{state.version} | Me not found | P:{state.players?.Length ?? 0} E:{state.enemies?.Length ?? 0}");
        }
    }
    #endregion
}
