using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridges Animator sprite updates to UI Image component.
/// Attach this to a GameObject that has both Animator and Image components.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Image))]
public class AnimatorToImage : MonoBehaviour
{
    #region Private Fields
    private Animator m_Animator;
    private Image m_Image;
    private SpriteRenderer m_SpriteRenderer;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
        m_Image = GetComponent<Image>();

        // Create a hidden SpriteRenderer to receive animator updates
        m_SpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        m_SpriteRenderer.enabled = false; // Hide it, we only use it to get sprite from animator
    }

    private void LateUpdate()
    {
        // Copy sprite from SpriteRenderer (updated by Animator) to Image
        if (m_SpriteRenderer != null && m_Image != null && m_SpriteRenderer.sprite != null)
        {
            m_Image.sprite = m_SpriteRenderer.sprite;
        }
    }
    #endregion
}













