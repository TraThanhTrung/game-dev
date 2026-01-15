using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject configuration for audio clips.
/// Contains BGM and SFX clips organized by name.
/// </summary>
[CreateAssetMenu(fileName = "AudioConfig", menuName = "Game/Audio Config")]
public class AudioConfig : ScriptableObject
{
    #region Private Fields
    [Header("Background Music")]
    [Tooltip("Background music clip (loops automatically)")]
    [SerializeField] private AudioClip m_BGMClip;

    [Header("Sound Effects")]
    [Tooltip("Combat hit sound effect")]
    [SerializeField] private AudioClip m_CombatHitSFX;

    [Tooltip("UI click sound effect")]
    [SerializeField] private AudioClip m_UIClickSFX;

    [Tooltip("Shop open sound effect")]
    [SerializeField] private AudioClip m_ShopOpenSFX;

    [Header("Additional SFX (Optional)")]
    [Tooltip("Additional sound effects organized by name")]
    [SerializeField] private List<SFXEntry> m_AdditionalSFX = new List<SFXEntry>();
    #endregion

    #region Public Properties
    public AudioClip BGMClip => m_BGMClip;
    #endregion

    #region Public Methods
    /// <summary>
    /// Get SFX clip by name. Supports both predefined clips and additional SFX entries.
    /// </summary>
    /// <param name="sfxName">Name of the SFX (e.g., "CombatHit", "UIClick")</param>
    /// <returns>AudioClip if found, null otherwise</returns>
    public AudioClip GetSFXClip(string sfxName)
    {
        // Check predefined SFX first
        switch (sfxName)
        {
            case "CombatHit":
                return m_CombatHitSFX;
            case "UIClick":
                return m_UIClickSFX;
            case "ShopOpen":
                return m_ShopOpenSFX;
        }

        // Check additional SFX entries
        foreach (var entry in m_AdditionalSFX)
        {
            if (entry.name == sfxName)
            {
                return entry.clip;
            }
        }

        return null;
    }
    #endregion

    #region Nested Classes
    [System.Serializable]
    public class SFXEntry
    {
        public string name;
        public AudioClip clip;
    }
    #endregion
}

