using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying a single player in the result list.
/// </summary>
public class PlayerListItem : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Image m_AvatarImage;
    [SerializeField] private TMP_Text m_NameText;
    [SerializeField] private TMP_Text m_LevelText;
    [SerializeField] private TMP_Text m_GoldText;
    [SerializeField] private Sprite m_DefaultAvatar;

    private Coroutine m_AvatarLoadCoroutine;
    #endregion

    #region Public Methods
    /// <summary>
    /// Initialize the player list item with player data.
    /// </summary>
    public void Initialize(GameResult.PlayerInfo playerInfo)
    {
        if (playerInfo == null)
        {
            Debug.LogWarning("[PlayerListItem] Player info is null");
            return;
        }

        // Display player name
        if (m_NameText != null)
        {
            m_NameText.text = playerInfo.name ?? "Unknown";
        }

        // Display level
        if (m_LevelText != null)
        {
            m_LevelText.text = $"Level: {playerInfo.level}";
        }

        // Display gold
        if (m_GoldText != null)
        {
            m_GoldText.text = $"Gold: {playerInfo.gold}";
        }

        // Load avatar
        LoadAvatar(playerInfo.avatarPath);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Load player avatar from URL or use default.
    /// </summary>
    private void LoadAvatar(string avatarPath)
    {
        if (m_AvatarImage == null)
        {
            return;
        }

        // Stop any existing avatar load coroutine
        if (m_AvatarLoadCoroutine != null)
        {
            StopCoroutine(m_AvatarLoadCoroutine);
        }

        // Set default avatar first
        if (m_DefaultAvatar != null)
        {
            m_AvatarImage.sprite = m_DefaultAvatar;
        }

        // Load avatar from path if provided
        if (!string.IsNullOrEmpty(avatarPath))
        {
            m_AvatarLoadCoroutine = StartCoroutine(LoadAvatarCoroutine(avatarPath));
        }
    }

    /// <summary>
    /// Coroutine to load avatar texture from URL.
    /// </summary>
    private IEnumerator LoadAvatarCoroutine(string avatarPath)
    {
        // Construct full URL if path is relative
        string url = avatarPath;
        if (NetClient.Instance != null)
        {
            string baseUrl = NetClient.Instance.BaseUrl;
            if (!string.IsNullOrEmpty(baseUrl) && !avatarPath.StartsWith("http"))
            {
                url = $"{baseUrl.TrimEnd('/')}/{avatarPath.TrimStart('/')}";
            }
        }

        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(www);
                if (texture != null && m_AvatarImage != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    m_AvatarImage.sprite = sprite;
                }
            }
            else
            {
                Debug.LogWarning($"[PlayerListItem] Failed to load avatar from {url}: {www.error}");
            }
        }
    }
    #endregion
}

