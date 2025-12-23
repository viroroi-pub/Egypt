using UnityEngine;
using System.Collections;

public class HighlightEffect : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Material using the OutlineBlink shader.")]
    public Material highlightMaterial;

    [Header("Automatic Behavior")]
    [Tooltip("If true, the object will highlight on start.")]
    public bool highlightOnStart = false;
    [Tooltip("Duration of the highlight if triggered by time (0 = infinite/manual).")]
    public float flashDuration = 2.0f;
    
    // Store the original material to restore it later
    private Material originalMaterial;
    private Renderer objectRenderer;
    private bool isHighlighted = false;
    private Coroutine flashCoroutine;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            
            // If marked to highlight on start, enable it
            if (highlightOnStart)
            {
                EnableHighlight();
            }
        }
        else
        {
            Debug.LogError("HighlightEffect: No Renderer component found on this GameObject.");
            enabled = false;
        }
    }

    /// <summary>
    /// Enables the highlight effect indefinitely.
    /// </summary>
    public void EnableHighlight()
    {
        if (objectRenderer == null || highlightMaterial == null) return;
        
        // If a flash coroutine is already running, stop it to prevent premature disabling
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (!isHighlighted)
        {
            // Assign the highlight material, but keep the original texture if available
            Texture mainTexture = originalMaterial.mainTexture;
            objectRenderer.material = highlightMaterial;
            objectRenderer.material.mainTexture = mainTexture;
            isHighlighted = true;
        }
    }

    /// <summary>
    /// Disables the effect and restores the original material.
    /// </summary>
    public void DisableHighlight()
    {
        if (objectRenderer == null) return;

        // Stop any pending timer
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (isHighlighted)
        {
            objectRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    /// <summary>
    /// Activates the highlight for a specific duration in seconds.
    /// </summary>
    /// <param name="duration">Time in seconds the highlight will last.</param>
    public void Flash(float duration)
    {
        EnableHighlight(); // Turn on
        // Start countdown to turn off
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DisableHighlightAfterDelay(duration));
    }

    // Helper coroutine to wait and disable
    private IEnumerator DisableHighlightAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DisableHighlight();
    }

    // --- Example usage with mouse (you can remove or modify this) ---
    
    // On CLICK, flash for 2 seconds (or whatever is set in flashDuration)
    void OnMouseDown()
    {
        Flash(flashDuration);
    }

    // On mouse OVER, highlight while hovering
    void OnMouseEnter()
    {
        // Commented out to avoid interference if you only want to test click or auto-start
        // EnableHighlight(); 
    }

    void OnMouseExit()
    {
        // Commented out to avoid interference
        // DisableHighlight();
    }
}