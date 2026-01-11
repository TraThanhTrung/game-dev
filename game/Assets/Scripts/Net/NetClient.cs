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
    private float m_PollInterval = 0.2f; // Default polling interval
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

        // Ensure KillReporter component exists
        if (GetComponent<KillReporter>() == null)
        {
            gameObject.AddComponent<KillReporter>();
            Debug.Log("[NetClient] Added KillReporter component");
        }

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

    public IEnumerator GetPlayerProfile(Action<PlayerProfileResponse> onSuccess, Action<string> onError)
    {
        if (PlayerId == Guid.Empty)
        {
            onError?.Invoke("Player not logged in");
            yield break;
        }

        var url = $"{m_BaseUrl}/auth/profile/{PlayerId}";
        using var req = UnityWebRequest.Get(url);
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

        // Debug: log raw JSON response
        Debug.Log($"[NetClient] Profile response JSON: {req.downloadHandler.text}");

        PlayerProfileResponse result;
        try
        {
            result = JsonUtility.FromJson<PlayerProfileResponse>(req.downloadHandler.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetClient] Failed to parse profile JSON: {ex.Message}");
            onError?.Invoke($"Invalid profile response JSON: {ex.Message}");
            yield break;
        }

        if (result == null)
        {
            onError?.Invoke("Null profile response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Downloads avatar image from server and converts it to Texture2D.
    /// </summary>
    public IEnumerator DownloadAvatarImage(string avatarPath, Action<Texture2D> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(avatarPath))
        {
            onError?.Invoke("Avatar path is empty");
            yield break;
        }

        // Build full URL - avatarPath should be relative (e.g., "/avatars/xxx.jpg" or "avatars/xxx.jpg")
        string url;
        if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
        {
            url = avatarPath; // Already a full URL
        }
        else
        {
            // Ensure path starts with "/" for proper URL construction
            var path = avatarPath.StartsWith("/") ? avatarPath : $"/{avatarPath}";
            url = $"{m_BaseUrl}{path}";
        }
        
        Debug.Log($"[NetClient] Downloading avatar from: {url}");
        
        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        var texture = DownloadHandlerTexture.GetContent(req);
        if (texture == null)
        {
            onError?.Invoke("Failed to download texture");
            yield break;
        }

        Debug.Log($"[NetClient] Avatar downloaded successfully: {texture.width}x{texture.height}");
        onSuccess?.Invoke(texture);
    }

    public IEnumerator LoginPlayer(string playerName, string password, Action onSuccess, Action<string> onError)
    {
        var payload = JsonUtility.ToJson(new LoginRequest { playerName = playerName, password = password });
        using var req = BuildPost("/auth/login", payload);
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
            onError?.Invoke("Invalid login response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.playerId) || !Guid.TryParse(result.playerId, out var pid))
        {
            onError?.Invoke("Missing/invalid PlayerId in login response");
            yield break;
        }

        PlayerId = pid;
        Token = result.token;
        SessionId = string.IsNullOrEmpty(result.sessionId) ? m_DefaultSessionId : result.sessionId;
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

    public void SetPollInterval(float intervalSeconds)
    {
        if (intervalSeconds > 0f)
        {
            m_PollInterval = intervalSeconds;
        }
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

    /// <summary>
    /// Report a kill so the server can grant rewards based on enemy type.
    /// </summary>
    public IEnumerator ReportKill(string enemyTypeId, Action<KillReportResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new KillReportRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            enemyTypeId = enemyTypeId,
            token = Token
        });

        using var req = BuildPost("/sessions/kill", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        KillReportResponse result = null;
        try
        {
            result = JsonUtility.FromJson<KillReportResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid kill report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Report damage taken from enemy so server can update HP authoritatively.
    /// </summary>
    public IEnumerator ReportDamage(int damageAmount, Action<DamageReportResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new DamageReportRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            damageAmount = damageAmount,
            token = Token
        });

        using var req = BuildPost("/sessions/damage", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        DamageReportResponse result = null;
        try
        {
            result = JsonUtility.FromJson<DamageReportResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid damage report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Report damage from player to enemy so server can update enemy HP authoritatively.
    /// </summary>
    public IEnumerator ReportEnemyDamage(System.Guid enemyId, int damageAmount, Action<EnemyDamageResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new EnemyDamageRequest
        {
            playerId = PlayerId.ToString(),
            enemyId = enemyId.ToString(),
            sessionId = SessionId,
            damageAmount = damageAmount,
            token = Token
        });

        using var req = BuildPost("/sessions/enemy-damage", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        EnemyDamageResponse result = null;
        try
        {
            result = JsonUtility.FromJson<EnemyDamageResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid enemy damage report response");
            yield break;
        }

        onSuccess?.Invoke(result);
    }

    /// <summary>
    /// Request respawn from server (resets position to spawn and sets HP to 50%).
    /// </summary>
    public IEnumerator CreateRoom(Action<CreateRoomResponse> onSuccess, Action<string> onError)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new CreateRoomRequest
        {
            playerId = PlayerId.ToString(),
            token = Token
        });

        using var req = BuildPost("/rooms/create", payload);
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

        CreateRoomResponse result;
        try
        {
            result = JsonUtility.FromJson<CreateRoomResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid create room response JSON");
            yield break;
        }

        if (result == null || string.IsNullOrEmpty(result.roomId))
        {
            onError?.Invoke("Missing/invalid RoomId in create room response");
            yield break;
        }

        SessionId = result.roomId;
        onSuccess?.Invoke(result);
    }

    public IEnumerator JoinRoom(string roomId, Action<JoinRoomResponse> onSuccess, Action<string> onError)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            onError?.Invoke("Room ID is required");
            yield break;
        }

        var payload = JsonUtility.ToJson(new JoinRoomRequest
        {
            playerId = PlayerId.ToString(),
            roomId = roomId,
            token = Token
        });

        using var req = BuildPost("/rooms/join", payload);
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

        JoinRoomResponse result;
        try
        {
            result = JsonUtility.FromJson<JoinRoomResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid join room response JSON");
            yield break;
        }

        if (result == null || !result.success)
        {
            onError?.Invoke("Failed to join room");
            yield break;
        }

        SessionId = result.roomId;
        onSuccess?.Invoke(result);
    }

    public IEnumerator RequestRespawn(Action<RespawnResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!IsConnected)
        {
            onError?.Invoke("Not connected");
            yield break;
        }

        var payload = JsonUtility.ToJson(new RespawnRequest
        {
            playerId = PlayerId.ToString(),
            sessionId = SessionId,
            token = Token
        });

        using var req = BuildPost("/sessions/respawn", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        RespawnResponse result = null;
        try
        {
            result = JsonUtility.FromJson<RespawnResponse>(req.downloadHandler.text);
        }
        catch
        {
            onError?.Invoke("Invalid respawn response");
            yield break;
        }

        onSuccess?.Invoke(result);
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
public class LoginRequest
{
    public string playerName;
    public string password;
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
    public int expToLevel;
    public int gold;
    // Player stats (synced from database)
    public int damage;
    public float range;
    public float speed;
    public float weaponRange;
    public float knockbackForce;
    public float knockbackTime;
    public float stunTime;
    public float bonusDamagePercent;
    public float damageReductionPercent;
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

[Serializable]
public class KillReportRequest
{
    public string playerId;
    public string enemyTypeId;
    public string sessionId;
    public string token;
}

[Serializable]
public class KillReportResponse
{
    public bool granted;
    public int level;
    public int exp;
    public int gold;
}

[Serializable]
public class DamageReportRequest
{
    public string playerId;
    public int damageAmount;
    public string sessionId;
    public string token;
}

[Serializable]
public class DamageReportResponse
{
    public bool accepted;
    public int currentHp;
    public int maxHp;
}

[Serializable]
public class EnemyDamageRequest
{
    public string playerId;
    public string enemyId;
    public int damageAmount;
    public string sessionId;
    public string token;
}

[Serializable]
public class EnemyDamageResponse
{
    public bool accepted;
    public int currentHp;
    public int maxHp;
    public bool isDead;
}

[Serializable]
public class RespawnRequest
{
    public string playerId;
    public string sessionId;
    public string token;
}

[Serializable]
public class RespawnResponse
{
    public bool accepted;
    public float x;
    public float y;
    public int currentHp;
    public int maxHp;
}

[Serializable]
public class PlayerProfileResponse
{
    public string playerId;
    public string name;
    public int level;
    public int exp;
    public int gold;
    public string avatarPath;
}

[Serializable]
public class CreateRoomRequest
{
    public string playerId;
    public string token;
}

[Serializable]
public class CreateRoomResponse
{
    public string roomId;
}

[Serializable]
public class JoinRoomRequest
{
    public string playerId;
    public string roomId;
    public string token;
}

[Serializable]
public class JoinRoomResponse
{
    public bool success;
    public string roomId;
}
#endregion

