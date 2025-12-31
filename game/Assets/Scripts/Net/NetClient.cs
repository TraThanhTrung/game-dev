using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetClient : MonoBehaviour
{
    #region Constants
    private const string ContentTypeJson = "application/json";
    #endregion

    #region Private Fields
    [SerializeField] private string m_BaseUrl = "http://localhost:5000";
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
        var payload = JsonUtility.ToJson(new RegisterRequest { PlayerName = playerName });
        using var req = BuildPost("/auth/register", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
            yield break;
        }

        var result = JsonUtility.FromJson<RegisterResponse>(req.downloadHandler.text);
        PlayerId = Guid.Parse(result.PlayerId);
        Token = result.Token;
        SessionId = string.IsNullOrEmpty(result.SessionId) ? m_DefaultSessionId : result.SessionId;
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
            PlayerId = PlayerId.ToString(),
            PlayerName = playerName,
            SessionId = SessionId,
            Token = Token
        });

        using var req = BuildPost("/sessions/join", payload);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error);
            yield break;
        }

        var result = JsonUtility.FromJson<JoinSessionResponse>(req.downloadHandler.text);
        SessionId = string.IsNullOrEmpty(result.SessionId) ? SessionId : result.SessionId;
        onSuccess?.Invoke();
    }

    public IEnumerator SendInput(InputPayload input, Action<string> onError)
    {
        if (!IsConnected) yield break;

        input.PlayerId = PlayerId.ToString();
        input.SessionId = SessionId;
        input.Token = Token;

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
    #endregion

    #region Private Methods
    private IEnumerator PollLoop(int? sinceVersion, float intervalSeconds, Action<StateResponse> onState, Action<string> onError)
    {
        int? version = sinceVersion;
        while (true)
        {
            yield return PollState(version, state =>
            {
                if (state.Version > 0)
                {
                    version = state.Version;
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
    #endregion
}

#region DTOs
[Serializable]
public class RegisterRequest
{
    public string PlayerName;
}

[Serializable]
public class RegisterResponse
{
    public string PlayerId;
    public string Token;
    public string SessionId;
}

[Serializable]
public class JoinSessionRequest
{
    public string PlayerId;
    public string PlayerName;
    public string SessionId;
    public string Token;
}

[Serializable]
public class JoinSessionResponse
{
    public string SessionId;
}

[Serializable]
public class InputPayload
{
    public string PlayerId;
    public string SessionId;
    public float MoveX;
    public float MoveY;
    public float AimX;
    public float AimY;
    public bool Attack;
    public bool Shoot;
    public int Sequence;
    public string Token;
}

[Serializable]
public class StateResponse
{
    public string SessionId;
    public int Version;
    public PlayerSnapshot[] Players;
    public EnemySnapshot[] Enemies;
    public ProjectileSnapshot[] Projectiles;
}

[Serializable]
public class PlayerSnapshot
{
    public string Id;
    public string Name;
    public float X;
    public float Y;
    public int Hp;
    public int MaxHp;
    public int Sequence;
}

[Serializable]
public class EnemySnapshot
{
    public string Id;
    public string TypeId;
    public float X;
    public float Y;
    public int Hp;
    public int MaxHp;
    public string Status;
}

[Serializable]
public class ProjectileSnapshot
{
    public string Id;
    public string OwnerId;
    public float X;
    public float Y;
    public float DirX;
    public float DirY;
    public float Radius;
}
#endregion

