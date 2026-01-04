using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the multiplayer connection UI and orchestrates NetClient calls.
/// Supports remembering server URL and player name via PlayerPrefs.
/// </summary>
public class MultiplayerUIManager : MonoBehaviour
{
    #region Constants
    private const string c_PrefKeyUrl = "mp_server_url";
    private const string c_PrefKeyName = "mp_player_name";
    private const string c_DefaultUrl = "http://localhost:5220";
    private const string c_DefaultName = "Player";
    #endregion

    #region Private Fields
    [SerializeField] private TMP_InputField m_ServerUrlInput;
    [SerializeField] private TMP_InputField m_PlayerNameInput;
    [SerializeField] private Button m_JoinButton;
    [SerializeField] private Button m_DisconnectButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private float m_PollIntervalSeconds = 0.2f;
    [SerializeField] private StateLogger m_StateLogger;
    [SerializeField] private string m_NextSceneName = "RPG";
    [SerializeField] private ServerStateApplier m_LocalPlayerApplier;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (NetClient.Instance == null)
        {
            var go = new GameObject("NetClient");
            go.AddComponent<NetClient>();
        }
    }

    private void Start()
    {
        // Load saved values from PlayerPrefs
        if (m_ServerUrlInput != null)
            m_ServerUrlInput.text = PlayerPrefs.GetString(c_PrefKeyUrl, c_DefaultUrl);
        if (m_PlayerNameInput != null)
            m_PlayerNameInput.text = PlayerPrefs.GetString(c_PrefKeyName, c_DefaultName);

        // Setup button listeners
        if (m_JoinButton != null)
            m_JoinButton.onClick.AddListener(ConnectViaLoginButton);
        if (m_DisconnectButton != null)
            m_DisconnectButton.onClick.AddListener(Disconnect);
    }
    #endregion

    #region Private Methods
    private void SetStatus(string text)
    {
        if (m_StatusText != null)
        {
            m_StatusText.text = text;
        }
    }

    private void OnStateReceived(StateResponse state)
    {
        if (m_StateLogger != null)
        {
            m_StateLogger.OnState(state);
        }

        // Apply snapshot to local player
        if (m_LocalPlayerApplier != null && state.players != null && NetClient.Instance != null)
        {
            var myId = NetClient.Instance.PlayerId.ToString();
            foreach (var p in state.players)
            {
                if (p.id == myId)
                {
                    m_LocalPlayerApplier.ApplySnapshot(p);
                    break;
                }
            }
        }
    }

    private void TryLoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(m_NextSceneName))
            return;

        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (active != m_NextSceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(m_NextSceneName);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Called by the Join button. Registers, joins session, and starts polling.
    /// </summary>
    public void ConnectViaLoginButton()
    {
        var url = m_ServerUrlInput != null ? m_ServerUrlInput.text : c_DefaultUrl;
        var name = m_PlayerNameInput != null ? m_PlayerNameInput.text : c_DefaultName;

        // Save to PlayerPrefs
        PlayerPrefs.SetString(c_PrefKeyUrl, url);
        PlayerPrefs.SetString(c_PrefKeyName, name);
        PlayerPrefs.Save();

        // Clear old session and register fresh
        NetClient.Instance.ClearSession();
        NetClient.Instance.ConfigureBaseUrl(url);
        SetStatus("Connecting...");

        StartCoroutine(NetClient.Instance.RegisterPlayer(name, () =>
        {
            SetStatus("Registered");
            Debug.Log($"[MultiplayerUI] Registered: PlayerId={NetClient.Instance.PlayerId}");

            StartCoroutine(NetClient.Instance.JoinSession(name, () =>
            {
                SetStatus("Joined session - Loading game...");
                Debug.Log($"[MultiplayerUI] Joined session: {NetClient.Instance.SessionId}");

                // Don't start polling here - ServerStateApplier in game scene will handle it
                TryLoadNextScene();
            },
            err => SetStatus($"Join error: {err}")));
        },
        err => SetStatus($"Register error: {err}")));
    }

    /// <summary>
    /// Called by the Disconnect button. Stops polling and clears session.
    /// </summary>
    public void Disconnect()
    {
        if (NetClient.Instance != null)
        {
            NetClient.Instance.StopPolling();
            NetClient.Instance.ClearSession();
        }
        SetStatus("Disconnected");
    }
    #endregion
}
