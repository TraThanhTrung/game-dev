using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the multiplayer login UI and orchestrates NetClient calls.
/// Handles username/password login and scene transition.
/// </summary>
public class MultiplayerUIManager : MonoBehaviour
{
    #region Constants
    private const string c_PrefKeyName = "mp_player_name";
    private const string c_DefaultName = "Player";

    // Status colors
    private static readonly Color c_ColorLoading = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray
    private static readonly Color c_ColorSuccess = new Color(0f, 1f, 0f, 1f); // Green
    private static readonly Color c_ColorError = new Color(1f, 0f, 0f, 1f); // Red
    #endregion

    #region Private Fields
    [SerializeField] private TMP_InputField m_PlayerNameInput;
    [SerializeField] private TMP_InputField m_PasswordInput;
    [SerializeField] private Button m_JoinButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private string m_NextSceneName = "Home";
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
        // Load saved player name from PlayerPrefs
        if (m_PlayerNameInput != null)
        {
            m_PlayerNameInput.text = PlayerPrefs.GetString(c_PrefKeyName, c_DefaultName);
            // Mount Enter key to trigger login
            m_PlayerNameInput.onSubmit.AddListener(_ => OnEnterPressed());
        }

        // Mount Enter key on password field to trigger login
        if (m_PasswordInput != null)
        {
            m_PasswordInput.onSubmit.AddListener(_ => OnEnterPressed());
        }

        // Setup button listener
        if (m_JoinButton != null)
            m_JoinButton.onClick.AddListener(ConnectViaLoginButton);
    }
    #endregion

    #region Private Methods
    private void SetStatus(string text, StatusType statusType = StatusType.Normal)
    {
        if (m_StatusText == null) return;

        // Set text
        if (statusType == StatusType.Loading)
        {
            m_StatusText.text = "Loading...";
            m_StatusText.color = c_ColorLoading;
        }
        else if (statusType == StatusType.Success)
        {
            m_StatusText.text = text;
            m_StatusText.color = c_ColorSuccess;
        }
        else if (statusType == StatusType.Error)
        {
            m_StatusText.text = text;
            m_StatusText.color = c_ColorError;
        }
        else
        {
            m_StatusText.text = text;
            // Keep default color for normal status
        }
    }

    private enum StatusType
    {
        Normal,
        Loading,
        Success,
        Error
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

    /// <summary>
    /// Called when Enter key is pressed in any input field.
    /// </summary>
    private void OnEnterPressed()
    {
        // Only trigger login if button is interactable
        if (m_JoinButton != null && m_JoinButton.interactable)
        {
            ConnectViaLoginButton();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Called by the Join button. Logs in with username and password.
    /// </summary>
    public void ConnectViaLoginButton()
    {
        var name = m_PlayerNameInput != null ? m_PlayerNameInput.text : c_DefaultName;
        var password = m_PasswordInput != null ? m_PasswordInput.text : string.Empty;

        // Validate inputs
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Error: Username is required", StatusType.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Error: Password is required", StatusType.Error);
            return;
        }

        // Check if password field is connected
        if (m_PasswordInput == null)
        {
            SetStatus("Error: Password field not connected in Unity Inspector", StatusType.Error);
            Debug.LogError("[MultiplayerUI] Password input field is not assigned in Inspector!");
            return;
        }

        // Save player name to PlayerPrefs
        PlayerPrefs.SetString(c_PrefKeyName, name);
        PlayerPrefs.Save();

        // Clear old session and configure base URL from NetClient
        NetClient.Instance.ClearSession();
        // BaseUrl is already configured via ServerConfig or default
        SetStatus("", StatusType.Loading);

        // Login with username and password
        StartCoroutine(NetClient.Instance.LoginPlayer(name, password, () =>
        {
            SetStatus("Logged in", StatusType.Success);
            Debug.Log($"[MultiplayerUI] Logged in: PlayerId={NetClient.Instance.PlayerId}");

            // No auto join session - user will create/join room in Home scene
            TryLoadNextScene();
        },
        err =>
        {
            // Parse error message to show user-friendly message
            var errorMsg = err ?? "Unknown error";

            // Handle expected errors gracefully (not as crashes)
            if (errorMsg.Contains("401") || errorMsg.Contains("Unauthorized"))
            {
                SetStatus("Sai tên đăng nhập hoặc mật khẩu", StatusType.Error);
                Debug.LogWarning("[MultiplayerUI] Login failed: Invalid credentials");
            }
            else if (errorMsg.Contains("404") || errorMsg.Contains("Not Found"))
            {
                SetStatus("Tài khoản không tồn tại", StatusType.Error);
                Debug.LogWarning("[MultiplayerUI] Login failed: Account not found");
            }
            else if (errorMsg.Contains("Cannot connect") || errorMsg.Contains("Connection"))
            {
                SetStatus("Không thể kết nối server", StatusType.Error);
                Debug.LogWarning($"[MultiplayerUI] Connection error: {errorMsg}");
            }
            else
            {
                SetStatus($"Lỗi đăng nhập: {errorMsg}", StatusType.Error);
                Debug.LogWarning($"[MultiplayerUI] Login failed: {errorMsg}");
            }
        }));
    }
    #endregion
}
