using UnityEngine;
using TMPro;

public class CharacterPreview : MonoBehaviour
{
    #region Public Fields
    [Header("Character Selections")]
    [Tooltip("List of available character selections")]
    public CharacterSelection[] characterSelections;

    [Header("Animation Preview")]
    [Tooltip("Animator component for character preview")]
    public Animator characterAnimator;

    [Header("UI References")]
    [Tooltip("Text component to display character name")]
    public TextMeshProUGUI characterNameText;

    [Tooltip("Text component to display character description")]
    public TextMeshProUGUI characterDescText;

    [Header("Display Settings")]
    [Tooltip("Default scale of the character preview")]
    [Range(0.1f, 5f)]
    public float defaultCharacterScale = 2f;

    [Tooltip("If true, automatically fit character to preview area")]
    public bool autoFitToArea = false;

    [Tooltip("Maximum size for auto-fit (in pixels)")]
    public Vector2 maxPreviewSize = new Vector2(192f, 192f);
    #endregion

    #region Private Fields
    private int m_CurrentCharacterIndex = -1;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>();
        }

        // Try to find text components if not assigned
        if (characterNameText == null)
        {
            characterNameText = GetComponentInChildren<TextMeshProUGUI>();
            if (characterNameText != null)
            {
                Debug.Log("[CharacterPreview] Auto-found characterNameText component");
            }
        }

        // Try to find description text (look for second TextMeshProUGUI or one with specific name)
        if (characterDescText == null)
        {
            var allTexts = GetComponentsInChildren<TextMeshProUGUI>();
            if (allTexts.Length > 1)
            {
                // Use second text component as description
                characterDescText = allTexts[1];
                Debug.Log("[CharacterPreview] Auto-found characterDescText component");
            }
            else if (allTexts.Length == 1 && characterNameText == null)
            {
                // Only one text found, use it for name
                characterNameText = allTexts[0];
            }
        }
    }

    private void Start()
    {
        // Log initial state for debugging
        Debug.Log($"[CharacterPreview] Animator: {(characterAnimator != null ? characterAnimator.name : "NULL")}");
        Debug.Log($"[CharacterPreview] Character selections count: {(characterSelections != null ? characterSelections.Length : 0)}");

        if (characterSelections != null)
        {
            for (int i = 0; i < characterSelections.Length; i++)
            {
                var selection = characterSelections[i];
                if (selection != null)
                {
                    Debug.Log($"[CharacterPreview] Selection[{i}]: Name='{selection.name}', Description='{selection.description}', Controller={(selection.animatorController != null ? selection.animatorController.name : "NULL")}");
                }
                else
                {
                    Debug.LogWarning($"[CharacterPreview] Selection[{i}] is NULL!");
                }
            }
        }

        // Apply initial scale
        ApplyScale(defaultCharacterScale);

        // Set default character (index 0)
        if (characterSelections != null && characterSelections.Length > 0)
        {
            SelectCharacter(0);
        }
    }
    #endregion

    #region Private Methods
    private void ApplyScale(float scale)
    {
        if (characterAnimator == null)
            return;

        float finalScale = scale;
        if (autoFitToArea)
        {
            // Auto-fit logic can be implemented here if needed
            // For now, just use the manual scale
        }

        characterAnimator.transform.localScale = Vector3.one * finalScale;
        Debug.Log($"[CharacterPreview] Applied scale: {finalScale}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Select character by index
    /// </summary>
    public void SelectCharacter(int index)
    {
        Debug.Log($"[CharacterPreview] SelectCharacter called with index: {index}");

        if (characterAnimator == null)
        {
            Debug.LogError("[CharacterPreview] characterAnimator is NULL!");
            return;
        }

        if (characterSelections == null || characterSelections.Length == 0)
        {
            Debug.LogError("[CharacterPreview] characterSelections is NULL or empty!");
            return;
        }

        if (index < 0 || index >= characterSelections.Length)
        {
            Debug.LogError($"[CharacterPreview] Index {index} out of range (0-{characterSelections.Length - 1})!");
            return;
        }

        var selection = characterSelections[index];
        if (selection == null)
        {
            Debug.LogError($"[CharacterPreview] Selection at index {index} is NULL!");
            return;
        }

        if (!selection.IsValid())
        {
            Debug.LogError($"[CharacterPreview] Selection at index {index} is invalid! Name='{selection.name}', Controller={(selection.animatorController != null ? "SET" : "NULL")}");
            return;
        }

        Debug.Log($"[CharacterPreview] Selecting character: Name='{selection.name}', Description='{selection.description}', Controller='{selection.animatorController.name}'");

        // Update character name text
        if (characterNameText != null)
        {
            characterNameText.text = selection.name ?? string.Empty;
            Debug.Log($"[CharacterPreview] Updated character name text: '{selection.name}'");
        }
        else
        {
            Debug.LogWarning("[CharacterPreview] characterNameText is NULL! Character name will not be displayed.");
        }

        // Update character description text
        if (characterDescText != null)
        {
            characterDescText.text = selection.description ?? string.Empty;
            Debug.Log($"[CharacterPreview] Updated character description text: '{selection.description}'");
        }
        else
        {
            Debug.LogWarning("[CharacterPreview] characterDescText is NULL! Character description will not be displayed.");
        }

        // Change animator controller
        characterAnimator.runtimeAnimatorController = selection.animatorController;

        // Apply custom scale if specified, otherwise use default
        float scaleToApply = selection.customScale > 0f ? selection.customScale : defaultCharacterScale;
        ApplyScale(scaleToApply);

        // Force animator to refresh and play from start
        characterAnimator.Rebind();
        characterAnimator.Update(0f);

        m_CurrentCharacterIndex = index;
        Debug.Log($"[CharacterPreview] Character selection changed successfully");
    }

    /// <summary>
    /// Get current selected character information
    /// </summary>
    public CharacterSelection GetCurrentSelection()
    {
        if (m_CurrentCharacterIndex >= 0 &&
            m_CurrentCharacterIndex < characterSelections.Length &&
            characterSelections != null)
        {
            return characterSelections[m_CurrentCharacterIndex];
        }
        return null;
    }

    /// <summary>
    /// Get current selected character index
    /// </summary>
    public int GetCurrentCharacterIndex()
    {
        return m_CurrentCharacterIndex;
    }

    /// <summary>
    /// Manually set the default character scale
    /// </summary>
    public void SetDefaultCharacterScale(float scale)
    {
        defaultCharacterScale = Mathf.Clamp(scale, 0.1f, 5f);

        // Apply to current character if one is selected
        if (m_CurrentCharacterIndex >= 0)
        {
            var selection = GetCurrentSelection();
            if (selection != null && selection.customScale <= 0f)
            {
                ApplyScale(defaultCharacterScale);
            }
        }
    }
    #endregion
}