using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays GameSession information (StartTime, EndTime, PlayerCount, Status).
/// </summary>
public class GameSessionInfoDisplay : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private TMP_Text m_StartTimeText;
    [SerializeField] private TMP_Text m_EndTimeText;
    [SerializeField] private TMP_Text m_PlayerCountText;
    [SerializeField] private TMP_Text m_StatusText;
    #endregion

    #region Public Methods
    /// <summary>
    /// Display GameSession information from match result data.
    /// </summary>
    public void DisplaySessionInfo(GameResult.MatchResultData resultData)
    {
        if (resultData == null)
        {
            Debug.LogWarning("[GameSessionInfoDisplay] Result data is null");
            return;
        }

        // Display start time
        if (m_StartTimeText != null)
        {
            m_StartTimeText.text = FormatDateTime(resultData.startTime);
        }

        // Display end time
        if (m_EndTimeText != null)
        {
            if (!string.IsNullOrEmpty(resultData.endTime))
            {
                m_EndTimeText.text = FormatDateTime(resultData.endTime);
            }
            else
            {
                m_EndTimeText.text = "N/A";
            }
        }

        // Display player count
        if (m_PlayerCountText != null)
        {
            m_PlayerCountText.text = resultData.playerCount.ToString();
        }

        // Display status
        if (m_StatusText != null)
        {
            m_StatusText.text = resultData.status ?? "Unknown";
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Format ISO datetime string to readable format.
    /// </summary>
    private string FormatDateTime(string isoDateTime)
    {
        if (string.IsNullOrEmpty(isoDateTime))
        {
            return "N/A";
        }

        try
        {
            if (DateTime.TryParse(isoDateTime, out DateTime dateTime))
            {
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameSessionInfoDisplay] Failed to parse datetime: {isoDateTime}, Error: {ex.Message}");
        }

        return isoDateTime; // Return original if parsing fails
    }
    #endregion
}



