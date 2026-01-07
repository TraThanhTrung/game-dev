using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Home scene: displays player profile, handles room creation/joining, and starts game.
/// </summary>
public class HomeManager : MonoBehaviour
{
    #region Constants
    private const string c_GameSceneName = "RPG";
    #endregion

    #region Private Fields
    [SerializeField] private TMP_Text m_PlayerNameText;
    [SerializeField] private TMP_Text m_PlayerLevelText;
    [SerializeField] private TMP_Text m_PlayerGoldText;
    [SerializeField] private TMP_InputField m_RoomIdInput;
    [SerializeField] private Button m_StartButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private string m_GameSceneName = c_GameSceneName;
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
        LoadPlayerProfile();
        SetupRoomUI();
    }
    #endregion

    #region Private Methods
    private void LoadPlayerProfile()
    {
        if (!NetClient.Instance.IsConnected)
        {
            SetStatus("Error: Not logged in", StatusType.Error);
            Debug.LogError("[HomeManager] Player not logged in");
            return;
        }

        SetStatus("Loading profile...", StatusType.Loading);
        StartCoroutine(NetClient.Instance.GetPlayerProfile(
            profile =>
            {
                // Update UI elements
                if (m_PlayerNameText != null)
                    m_PlayerNameText.text = profile.name ?? "Unknown";

                if (m_PlayerLevelText != null)
                    m_PlayerLevelText.text = $"Level: {profile.level}";

                if (m_PlayerGoldText != null)
                    m_PlayerGoldText.text = $"Gold: {profile.gold}";

                SetStatus("Profile loaded", StatusType.Success);
                Debug.Log($"[HomeManager] Profile loaded: {profile.name} Level={profile.level} Gold={profile.gold}");
            },
            err =>
            {
                SetStatus($"Error loading profile: {err}", StatusType.Error);
                Debug.LogError($"[HomeManager] Failed to load profile: {err}");
            }));
    }

    private void SetupRoomUI()
    {
        // Setup Start button listener
        if (m_StartButton != null)
        {
            m_StartButton.onClick.AddListener(OnStartGame);
        }
    }

    private void SetStatus(string text, StatusType statusType = StatusType.Normal)
    {
        if (m_StatusText == null) return;

        m_StatusText.text = text;

        // Set color based on status type
        switch (statusType)
        {
            case StatusType.Loading:
                m_StatusText.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray
                break;
            case StatusType.Success:
                m_StatusText.color = new Color(0f, 1f, 0f, 1f); // Green
                break;
            case StatusType.Error:
                m_StatusText.color = new Color(1f, 0f, 0f, 1f); // Red
                break;
            default:
                // Keep default color
                break;
        }
    }

    private enum StatusType
    {
        Normal,
        Loading,
        Success,
        Error
    }

    private void OnStartGame()
    {
        if (!NetClient.Instance.IsConnected)
        {
            SetStatus("Error: Not logged in", StatusType.Error);
            return;
        }

        var roomId = m_RoomIdInput != null ? m_RoomIdInput.text.Trim() : string.Empty;
        bool isEmpty = string.IsNullOrWhiteSpace(roomId);

        if (isEmpty)
        {
            // Create and join a new room
            SetStatus("Creating room...", StatusType.Loading);
            StartCoroutine(NetClient.Instance.CreateRoom(
                createResponse =>
                {
                    // Update input field with the new room ID
                    if (m_RoomIdInput != null)
                        m_RoomIdInput.text = createResponse.roomId;

                    SetStatus("Joining room...", StatusType.Loading);
                    // Join the room we just created
                    StartCoroutine(NetClient.Instance.JoinRoom(createResponse.roomId,
                        joinResponse =>
                        {
                            SetStatus($"Joined room: {joinResponse.roomId}", StatusType.Success);
                            Debug.Log($"[HomeManager] Created and joined room: {joinResponse.roomId}");
                            LoadGameScene();
                        },
                        err =>
                        {
                            SetStatus($"Error joining room: {err}", StatusType.Error);
                            Debug.LogError($"[HomeManager] Failed to join created room: {err}");
                        }));
                },
                err =>
                {
                    SetStatus($"Error creating room: {err}", StatusType.Error);
                    Debug.LogError($"[HomeManager] Failed to create room: {err}");
                }));
        }
        else
        {
            // Join existing room
            SetStatus("Joining room...", StatusType.Loading);
            StartCoroutine(NetClient.Instance.JoinRoom(roomId,
                response =>
                {
                    SetStatus($"Joined room: {response.roomId}", StatusType.Success);
                    Debug.Log($"[HomeManager] Joined room: {response.roomId}");
                    LoadGameScene();
                },
                err =>
                {
                    SetStatus($"Error joining room: {err}", StatusType.Error);
                    Debug.LogError($"[HomeManager] Failed to join room: {err}");
                }));
        }
    }

    private void LoadGameScene()
    {
        SetStatus("Starting game...", StatusType.Loading);
        var roomId = NetClient.Instance != null ? NetClient.Instance.SessionId : "Unknown";
        Debug.Log($"[HomeManager] Starting game in room: {roomId}");

        // Load game scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(m_GameSceneName);
    }
    #endregion
}

