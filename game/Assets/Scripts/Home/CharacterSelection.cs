using UnityEngine;

/// <summary>
/// Represents a character selection option with name, description, and animator controller.
/// </summary>
[System.Serializable]
public class CharacterSelection
{
    #region Public Fields
    [Tooltip("Display name of the character")]
    public string name;

    [Tooltip("Description of the character")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Animator Controller for this character")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("Optional: Custom scale for this character (0 = use default)")]
    [Range(0f, 5f)]
    public float customScale = 0f;
    #endregion

    #region Public Methods
    /// <summary>
    /// Validates if this character selection has required data
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(name) && animatorController != null;
    }
    #endregion
}
















