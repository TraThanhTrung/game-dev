using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetClient : MonoBehaviour
{
    #region Constants
    private const string ContentTypeJson = "application/json";
    private const string c_PrefKeyPlayerId = "mp_player_id";
    private const string c_PrefKeyToken = "mp_token";
    private const string c_PrefKeySessionId = "mp_session_id";
    #endregion

    #region Private Fields
    [SerializeField] private string m_BaseUrl = "http://localhost:5220";
    [SerializeField] private string m_DefaultSessionId = "default";

    private Coroutine pollRoutine;
    #endregion

    #region Public Properties
    public static NetClient Instance { get; private set; }

    public Guid PlayerId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string SessionId { get; private set; } = "default";
    public bool IsConnected => PlayerId != Guid.Empty && !string.IsNullOrEmpty(Token);
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
        
        // Clear any old saved session to avoid stale data
        ClearSavedSession();
    }
    #endregion

    #region Public Methods
    public void ConfigureBaseUrl(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            m_BaseUrl = baseUrl.TrimEnd('/');
        }
    }

    public IEnumerator RegisterPlayer(string playerName, Action onSuccess, Action<string> onError)
    {
        var payload = JsonUtility.ToJson(new RegisterRequest { playerName = playerName });
        using var req = BuildPost("/auth/register", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        if (string.IsNullOrEmpty(req.downloadHandler.text))
        {
            onError?.Invoke("Empty response");
            yield break;
        }

        RegisterResponse result;
        try
        {
            result = JsonUtility.FromJson<RegisterResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid register response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.playerId) || !Guid.TryParse(result.playerId, out var pid))
        {
            onError?.Invoke("Missing/invalid PlayerId in register response");
            yield break;
        }

        PlayerId = pid;
        Token = result.token;
        SessionId = string.IsNullOrEmpty(result.sessionId) ? m_DefaultSessionId : result.sessionId;
        // Session persistence disabled - server handles player data
        onSuccess?.Invoke();
    }

    public IEnumerator JoinSession(string playerName, Action onSuccess, Action<string> onError)
    {
        if (PlayerId == Guid.Empty)
        {
            onError?.Invoke("Player not registered");
            yield break;
        }

        var payload = JsonUtility.ToJson(new JoinSessionRequest
        {
            playerId = PlayerId.ToString(),
            playerName = playerName,
            sessionId = SessionId,
            token = Token
        });

        using var req = BuildPost("/sessions/join", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler?.text}");
            yield break;
        }

        var result = JsonUtility.FromJson<JoinSessionResponse>(req.downloadHandler.text);
        SessionId = string.IsNullOrEmpty(result.sessionId) ? SessionId : result.sessionId;
        onSuccess?.Invoke();
    }

    public IEnumerator SendInput(InputPayload input, Action<string> onError)
    {
        if (!IsConnected) yield break;

        input.playerId = PlayerId.ToString();
        input.sessionId = SessionId;
        input.token = Token;

        var payload = JsonUtility.ToJson(input);
        using var req = BuildPost("/sessions/input", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
        }
    }

    public void StartPolling(int? sinceVersion, float intervalSeconds, Action<StateResponse> onState, Action<string> onError)
    {
        StopPolling();
        pollRoutine = StartCoroutine(PollLoop(sinceVersion, intervalSeconds, onState, onError));
    }

    public void StopPolling()
    {
        if (pollRoutine != null)
        {
            StopCoroutine(pollRoutine);
            pollRoutine = null;
        }
    }

    /// <summary>
    /// Clears the current session data (playerId, token, sessionId).
    /// Also clears the saved session from PlayerPrefs.
    /// Call after disconnecting or logging out.
    /// </summary>
    public void ClearSession()
    {
        PlayerId = Guid.Empty;
        Token = string.Empty;
        SessionId = m_DefaultSessionId;
        ClearSavedSession();
    }
    #endregion

    #region Private Methods
    private void LoadSession()
    {
        var savedId = PlayerPrefs.GetString(c_PrefKeyPlayerId, "");
        var savedToken = PlayerPrefs.GetString(c_PrefKeyToken, "");
        var savedSession = PlayerPrefs.GetString(c_PrefKeySessionId, "");

        if (Guid.TryParse(savedId, out var pid) && !string.IsNullOrEmpty(savedToken))
        {
            PlayerId = pid;
            Token = savedToken;
            SessionId = string.IsNullOrEmpty(savedSession) ? m_DefaultSessionId : savedSession;
            Debug.Log($"[NetClient] Loaded saved session: PlayerId={PlayerId}");
        }
    }

    private void SaveSession()
    {
        PlayerPrefs.SetString(c_PrefKeyPlayerId, PlayerId.ToString());
        PlayerPrefs.SetString(c_PrefKeyToken, Token);
        PlayerPrefs.SetString(c_PrefKeySessionId, SessionId);
        PlayerPrefs.Save();
        Debug.Log($"[NetClient] Saved session: PlayerId={PlayerId}");
    }

    private void ClearSavedSession()
    {
        PlayerPrefs.DeleteKey(c_PrefKeyPlayerId);
        PlayerPrefs.DeleteKey(c_PrefKeyToken);
        PlayerPrefs.DeleteKey(c_PrefKeySessionId);
        PlayerPrefs.Save();
    }

    private IEnumerator PollLoop(int? sinceVersion, float intervalSeconds, Action<StateResponse> onState, Action<string> onError)
    {
        int? version = sinceVersion;
        while (true)
        {
            yield return PollState(version, state =>
            {
                if (state.version > 0)
                {
                    version = state.version;
                    onState?.Invoke(state);
                }
            }, onError);

            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    private IEnumerator PollState(int? sinceVersion, Action<StateResponse> onState, Action<string> onError)
    {
        var url = $"{m_BaseUrl}/sessions/{SessionId}/state";
        if (sinceVersion.HasValue)
        {
            url += $"?sinceVersion={sinceVersion.Value}";
        }

        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(req.downloadHandler.text))
        {
            var state = JsonUtility.FromJson<StateResponse>(req.downloadHandler.text);
            onState?.Invoke(state);
        }
        else if (req.result != UnityWebRequest.Result.Success && req.responseCode != 204)
        {
            onError?.Invoke(req.error);
        }
    }

    private UnityWebRequest BuildPost(string path, string payload)
    {
        var req = new UnityWebRequest($"{m_BaseUrl}{path}", UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", ContentTypeJson);
        return req;
    }

    /// <summary>
    /// Save current progress to database on the server.
    /// </summary>
    public IEnumerator SaveProgress(Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new SaveProgressRequest { playerId = PlayerId.ToString(), token = Token });
        using var req = BuildPost("/sessions/save", payload);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[NetClient] Progress saved to server.");
            onSuccess?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[NetClient] SaveProgress failed: {req.error}");
            onError?.Invoke(req.error);
        }
    }

    /// <summary>
    /// Disconnect from server (saves progress and cleans up).
    /// </summary>
    public IEnumerator Disconnect(Action onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onSuccess?.Invoke();
            yield break;
        }

        StopPolling();

        var payload = JsonUtility.ToJson(new DisconnectRequest { playerId = PlayerId.ToString(), token = Token });
        using var req = BuildPost("/sessions/disconnect", payload);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[NetClient] Disconnected and progress saved.");
        }
        else
        {
            Debug.LogWarning($"[NetClient] Disconnect failed: {req.error}");
        }

        // Clear local session
        ClearSession();
        onSuccess?.Invoke();
    }
    #endregion
}

#region DTOs
[Serializable]
public class RegisterRequest
{
    public string playerName;
}

[Serializable]
public class RegisterResponse
{
    public string playerId;
    public string token;
    public string sessionId;
}

[Serializable]
public class JoinSessionRequest
{
    public string playerId;
    public string playerName;
    public string sessionId;
    public string token;
}

[Serializable]
public class JoinSessionResponse
{
    public string sessionId;
}

[Serializable]
public class SaveProgressRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class DisconnectRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class InputPayload
{
    public string playerId;
    public string sessionId;
    public float moveX;
    public float moveY;
    public float aimX;
    public float aimY;
    public bool attack;
    public bool shoot;
    public int sequence;
    public string token;
}

[Serializable]
public class StateResponse
{
    public string sessionId;
    public int version;
    public PlayerSnapshot[] players;
    public EnemySnapshot[] enemies;
    public ProjectileSnapshot[] projectiles;
}

[Serializable]
public class PlayerSnapshot
{
    public string id;
    public string name;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public int sequence;
    public int level;
    public int exp;
    public int gold;
}

[Serializable]
public class EnemySnapshot
{
    public string id;
    public string typeId;
    public float x;
    public float y;
    public int hp;
    public int maxHp;
    public string status;
}

[Serializable]
public class ProjectileSnapshot
{
    public string id;
    public string ownerId;
    public float x;
    public float y;
    public float dirX;
    public float dirY;
    public float radius;
}
#endregion

