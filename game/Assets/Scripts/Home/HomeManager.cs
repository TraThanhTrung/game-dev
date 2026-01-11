using System.Collections;
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
    [SerializeField] private UnityEngine.UI.RawImage m_PlayerAvatarImage;
    [SerializeField] private TMP_InputField m_RoomIdInput;
    [SerializeField] private Button m_StartButton;
    [SerializeField] private TMP_Text m_StatusText;
    [SerializeField] private string m_GameSceneName = c_GameSceneName;

    [Header("Character Selection")]
    [SerializeField] private CharacterPreview m_CharacterPreview;
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
                // Update UI elements - Name, Level, Gold
                if (m_PlayerNameText != null)
                    m_PlayerNameText.text = profile.name ?? "Unknown";

                if (m_PlayerLevelText != null)
                    m_PlayerLevelText.text = $"Level: {profile.level}";

                if (m_PlayerGoldText != null)
                    m_PlayerGoldText.text = $"Gold: {profile.gold}";

                Debug.Log($"[HomeManager] Profile loaded: {profile.name} Level={profile.level} Gold={profile.gold} AvatarPath={(profile.avatarPath ?? "null")}");

                // Load avatar image if path is provided
                if (!string.IsNullOrEmpty(profile.avatarPath) && m_PlayerAvatarImage != null)
                {
                    Debug.Log($"[HomeManager] Loading avatar from path: {profile.avatarPath}");
                    StartCoroutine(LoadAvatarImage(profile.avatarPath));
                }
                else
                {
                    // Clear avatar if no path provided
                    if (m_PlayerAvatarImage != null)
                    {
                        m_PlayerAvatarImage.texture = null;
                    }
                    Debug.Log($"[HomeManager] No avatar path provided. avatarPath={(profile.avatarPath ?? "null")}, m_PlayerAvatarImage={(m_PlayerAvatarImage != null ? "assigned" : "null")}");
                }

                SetStatus("Profile loaded", StatusType.Success);
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

    private IEnumerator LoadAvatarImage(string avatarPath)
    {
        yield return NetClient.Instance.DownloadAvatarImage(avatarPath,
            texture =>
            {
                if (m_PlayerAvatarImage != null && texture != null)
                {
                    // Set texture directly to RawImage (no need to convert to Sprite)
                    m_PlayerAvatarImage.texture = texture;
                    Debug.Log($"[HomeManager] Avatar loaded successfully: {avatarPath} ({texture.width}x{texture.height})");
                }
            },
            err =>
            {
                Debug.LogWarning($"[HomeManager] Failed to load avatar: {err}");
                // Don't set error status for avatar load failure - profile still loaded successfully
            });
    }

    private void LoadGameScene()
    {
        SetStatus("Starting game...", StatusType.Loading);
        var roomId = NetClient.Instance != null ? NetClient.Instance.SessionId : "Unknown";

        // Get selected character type from CharacterPreview
        string characterType = GetSelectedCharacterType();

        // Store character type in NetClient for later use
        if (NetClient.Instance != null)
        {
            NetClient.Instance.SelectedCharacterType = characterType;
        }

        Debug.Log($"[HomeManager] Starting game in room: {roomId}, character: {characterType}");

        // Load game scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(m_GameSceneName);
    }

    /// <summary>
    /// Get the currently selected character type from CharacterPreview.
    /// </summary>
    private string GetSelectedCharacterType()
    {
        if (m_CharacterPreview == null)
        {
            Debug.LogWarning("[HomeManager] CharacterPreview is NOT assigned! Using default: lancer");
            return "lancer";
        }

        var selection = m_CharacterPreview.GetCurrentSelection();
        if (selection == null)
        {
            Debug.LogWarning("[HomeManager] No character selection found! Using default: lancer");
            return "lancer";
        }

        if (string.IsNullOrEmpty(selection.name))
        {
            Debug.LogWarning("[HomeManager] Character selection has no name! Using default: lancer");
            return "lancer";
        }

        string characterType = selection.name.ToLower();
        Debug.Log($"[HomeManager] Selected character type: {characterType}");
        return characterType;
    }
    #endregion
}

