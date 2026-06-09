using UnityEngine;

public class PyramidBlockController : MonoBehaviour
{
    private Vector3 originalPosition;
    private MeshRenderer meshRenderer;
    private bool initialized = false;

    // We no longer need Start or Update here because ConstructionEffectManager handles the movement loop.

    public void Initialize()
    {
        if (initialized) return;

        originalPosition = transform.position;
        meshRenderer = GetComponent<MeshRenderer>();
        initialized = true;
    }

    public float GetOriginalHeight()
    {
        // If not initialized, use current position as fallback
        if (!initialized) return transform.position.y;
        return originalPosition.y;
    }

    public void SetVisible(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }

        // If hiding, snap back to original position
        // This ensures that when the Manager starts moving it, the 'target' is correct
        if (!visible && initialized)
        {
            transform.position = originalPosition;
        }
    }
}