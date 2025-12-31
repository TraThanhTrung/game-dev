using System.Text;
using UnityEngine;

public class StateLogger : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private bool m_LogToConsole = true;
    #endregion

    #region Public Methods
    public void OnState(StateResponse state)
    {
        if (!m_LogToConsole || state == null) return;

        var sb = new StringBuilder();
        sb.Append($"State v{state.version} players:{state.players?.Length ?? 0} enemies:{state.enemies?.Length ?? 0} projectiles:{state.projectiles?.Length ?? 0}");

        if (state.players != null)
        {
            foreach (var p in state.players)
            {
                sb.Append($"\nP {p.name} pos=({p.x:0.0},{p.y:0.0}) hp={p.hp}/{p.maxHp} seq={p.sequence}");
            }
        }

        Debug.Log(sb.ToString());
    }
    #endregion
}

