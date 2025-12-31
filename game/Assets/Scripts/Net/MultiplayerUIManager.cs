using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerUIManager : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private TMP_InputField m_ServerUrlInput;
    [SerializeField] private TMP_InputField m_PlayerNameInput;
    [SerializeField] private Button m_RegisterButton;
    [SerializeField] private Button m_JoinButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private float m_PollIntervalSeconds = 0.2f;
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
        m_RegisterButton.onClick.AddListener(OnRegisterClicked);
        m_JoinButton.onClick.AddListener(OnJoinClicked);
    }
    #endregion

    #region Private Methods
    private void OnRegisterClicked()
    {
        var name = m_PlayerNameInput != null ? m_PlayerNameInput.text : "Player";
        var url = m_ServerUrlInput != null ? m_ServerUrlInput.text : string.Empty;

        NetClient.Instance.ConfigureBaseUrl(url);
        StartCoroutine(NetClient.Instance.RegisterPlayer(name, () =>
        {
            SetStatus("Registered");
        },
        error => SetStatus($"Register error: {error}")));
    }

    private void OnJoinClicked()
    {
        var name = m_PlayerNameInput != null ? m_PlayerNameInput.text : "Player";
        StartCoroutine(NetClient.Instance.JoinSession(name, () =>
        {
            SetStatus("Joined session");
            NetClient.Instance.StartPolling(null, m_PollIntervalSeconds, _ => { }, err => SetStatus($"Poll error: {err}"));
        },
        error => SetStatus($"Join error: {error}")));
    }

    private void SetStatus(string text)
    {
        if (m_StatusText != null)
        {
            m_StatusText.text = text;
        }
    }
    #endregion
}

