using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages background music (BGM) and sound effects (SFX) for the game.
/// Singleton pattern ensures only one AudioManager exists.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Constants
    private const string c_LogPrefix = "[AudioManager]";
    #endregion

    #region Private Fields
    [Header("Audio Configuration")]
    [SerializeField] private AudioConfig m_AudioConfig;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource m_BGMSource;
    [SerializeField] private AudioSource m_SFXSource;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float m_BGMVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float m_SFXVolume = 1f;

    private static AudioManager s_Instance;
    #endregion

    #region Public Properties
    public static AudioManager Instance => s_Instance;

    public float BGMVolume
    {
        get => m_BGMVolume;
        set
        {
            m_BGMVolume = Mathf.Clamp01(value);
            if (m_BGMSource != null)
            {
                m_BGMSource.volume = m_BGMVolume;
            }
        }
    }

    public float SFXVolume
    {
        get => m_SFXVolume;
        set
        {
            m_SFXVolume = Mathf.Clamp01(value);
            if (m_SFXSource != null)
            {
                m_SFXSource.volume = m_SFXVolume;
            }
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (s_Instance != null && s_Instance != this)
        {
            Debug.LogWarning($"{c_LogPrefix} Duplicate AudioManager found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
    }

    private void Start()
    {
        // Start playing BGM if available
        if (m_AudioConfig != null && m_AudioConfig.BGMClip != null)
        {
            PlayBGM();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Initialize AudioSource components if they don't exist.
    /// </summary>
    private void InitializeAudioSources()
    {
        // Create BGM AudioSource if not assigned
        if (m_BGMSource == null)
        {
            GameObject bgmObject = new GameObject("BGM Source");
            bgmObject.transform.SetParent(transform);
            m_BGMSource = bgmObject.AddComponent<AudioSource>();
            m_BGMSource.loop = true;
            m_BGMSource.playOnAwake = false;
            m_BGMSource.volume = m_BGMVolume;
        }

        // Create SFX AudioSource if not assigned
        if (m_SFXSource == null)
        {
            GameObject sfxObject = new GameObject("SFX Source");
            sfxObject.transform.SetParent(transform);
            m_SFXSource = sfxObject.AddComponent<AudioSource>();
            m_SFXSource.loop = false;
            m_SFXSource.playOnAwake = false;
            m_SFXSource.volume = m_SFXVolume;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Play background music. Stops current BGM if playing.
    /// </summary>
    public void PlayBGM()
    {
        if (m_AudioConfig == null || m_AudioConfig.BGMClip == null)
        {
            Debug.LogWarning($"{c_LogPrefix} Cannot play BGM: AudioConfig or BGMClip is null.");
            return;
        }

        if (m_BGMSource == null)
        {
            Debug.LogError($"{c_LogPrefix} BGM AudioSource is null.");
            return;
        }

        m_BGMSource.clip = m_AudioConfig.BGMClip;
        m_BGMSource.volume = m_BGMVolume;
        m_BGMSource.Play();
        Debug.Log($"{c_LogPrefix} Playing BGM: {m_AudioConfig.BGMClip.name}");
    }

    /// <summary>
    /// Stop background music.
    /// </summary>
    public void StopBGM()
    {
        if (m_BGMSource != null && m_BGMSource.isPlaying)
        {
            m_BGMSource.Stop();
            Debug.Log($"{c_LogPrefix} BGM stopped.");
        }
    }

    /// <summary>
    /// Play a sound effect by name.
    /// </summary>
    /// <param name="sfxName">Name of the SFX clip (e.g., "CombatHit", "UIClick")</param>
    public void PlaySFX(string sfxName)
    {
        if (m_AudioConfig == null)
        {
            Debug.LogWarning($"{c_LogPrefix} Cannot play SFX: AudioConfig is null.");
            return;
        }

        AudioClip clip = m_AudioConfig.GetSFXClip(sfxName);
        if (clip == null)
        {
            Debug.LogWarning($"{c_LogPrefix} SFX clip '{sfxName}' not found in AudioConfig.");
            return;
        }

        PlaySFX(clip);
    }

    /// <summary>
    /// Play a sound effect directly from an AudioClip.
    /// </summary>
    /// <param name="clip">AudioClip to play</param>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning($"{c_LogPrefix} Cannot play SFX: AudioClip is null.");
            return;
        }

        if (m_SFXSource == null)
        {
            Debug.LogError($"{c_LogPrefix} SFX AudioSource is null.");
            return;
        }

        m_SFXSource.PlayOneShot(clip, m_SFXVolume);
    }

    /// <summary>
    /// Play combat hit sound effect.
    /// </summary>
    public void PlayCombatHit()
    {
        PlaySFX("CombatHit");
    }

    /// <summary>
    /// Play UI click sound effect.
    /// </summary>
    public void PlayUIClick()
    {
        PlaySFX("UIClick");
    }

    /// <summary>
    /// Play shop open sound effect.
    /// </summary>
    public void PlayShopOpen()
    {
        PlaySFX("ShopOpen");
    }
    #endregion
}

