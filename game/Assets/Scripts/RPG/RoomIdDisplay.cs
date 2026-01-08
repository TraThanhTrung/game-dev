using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current room ID in the RPG scene.
/// Automatically creates UI elements if not assigned.
/// </summary>
public class RoomIdDisplay : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private TMP_Text m_RoomIdText;
    [SerializeField] private bool m_AutoCreateUI = true;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        EnsureUIExists();
        UpdateRoomIdDisplay();
    }

    private void OnEnable()
    {
        EnsureUIExists();
        UpdateRoomIdDisplay();
    }
    #endregion

    #region Private Methods
    private void EnsureUIExists()
    {
        if (m_RoomIdText != null) return;

        if (!m_AutoCreateUI)
        {
            Debug.LogWarning("[RoomIdDisplay] Room ID text component is not assigned and auto-create is disabled");
            return;
        }

        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("RoomIdCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create text GameObject
        GameObject textObj = new GameObject("RoomIdText");
        textObj.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(10f, -10f);
        rectTransform.sizeDelta = new Vector2(300f, 30f);

        m_RoomIdText = textObj.AddComponent<TextMeshProUGUI>();
        m_RoomIdText.text = "Room ID: Loading...";
        m_RoomIdText.fontSize = 18f;
        m_RoomIdText.color = Color.white;
        m_RoomIdText.alignment = TextAlignmentOptions.TopLeft;

        Debug.Log("[RoomIdDisplay] Auto-created Room ID UI element");
    }

    private void UpdateRoomIdDisplay()
    {
        if (m_RoomIdText == null)
        {
            return;
        }

        if (NetClient.Instance == null)
        {
            m_RoomIdText.text = "Room ID: Not Connected";
            Debug.LogWarning("[RoomIdDisplay] NetClient instance is null");
            return;
        }

        var roomId = NetClient.Instance.SessionId;
        if (string.IsNullOrEmpty(roomId))
        {
            m_RoomIdText.text = "Room ID: None";
        }
        else
        {
            m_RoomIdText.text = $"Room ID: {roomId}";
        }

        Debug.Log($"[RoomIdDisplay] Room ID displayed: {roomId ?? "None"}");
    }
    #endregion
}

