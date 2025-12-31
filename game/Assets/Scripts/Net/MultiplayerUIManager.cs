using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerUIManager : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private TMP_InputField m_PlayerNameInput;
    [SerializeField] private Button m_JoinButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private float m_PollIntervalSeconds = 0.2f;
    [SerializeField] private StateLogger m_StateLogger;
    [SerializeField] private string m_NextSceneName = "RPG";
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
        m_JoinButton.onClick.AddListener(ConnectViaLoginButton);
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
    }
    public void ConnectViaLoginButton()
    {
        var name = m_PlayerNameInput ? m_PlayerNameInput.text : "Player";

        StartCoroutine(NetClient.Instance.RegisterPlayer(name, () =>
        {
            SetStatus("Registered");
            StartCoroutine(NetClient.Instance.JoinSession(name, () =>
            {
                SetStatus("Joined session");
                NetClient.Instance.StartPolling(null, m_PollIntervalSeconds, OnStateReceived, err => SetStatus($"Poll error: {err}"));
                TryLoadNextScene();
            },
            err => SetStatus($"Join error: {err}")));
        },
        err => SetStatus($"Register error: {err}")));
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
}

