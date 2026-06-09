using System.Collections;
using UnityEngine;

public class BlockLevitation : MonoBehaviour
{
    private Vector3 originalPosition;
    private Vector3 targetPosition;
    private bool isFloating = false;

    // Random configuration so it doesn't look robotic
    private float floatSpeed = 2.0f;
    private float randomHeightOffset;
    private float hoverFrequency;
    private float hoverAmplitude;

    private void Awake()
    {
        // We save the original position when the script is created
        originalPosition = transform.position;

        // We assigned random variations for the visual effect
        randomHeightOffset = Random.Range(5.0f, 20.0f); // Altura extra aleatoria
        hoverFrequency = Random.Range(0.5f, 1.5f);
        hoverAmplitude = Random.Range(0.1f, 0.3f);
        floatSpeed = Random.Range(1.5f, 3.5f);
    }

    /// Start the deconstruction sequence (up).
    public void Levitate(float delay, float baseLevitationHeight)
    {
        StopAllCoroutines();
        // The destination position is the original position + base height + a little randomness
        targetPosition = originalPosition + Vector3.up * (baseLevitationHeight + randomHeightOffset);
        StartCoroutine(MoveToPosition(targetPosition, delay, true));
    }

    /// Start the reconstruction sequence (download).
    public void ReturnToBase(float delay)
    {
        StopAllCoroutines();
        StartCoroutine(MoveToPosition(originalPosition, delay, false));
    }

    private IEnumerator MoveToPosition(Vector3 target, float delay, bool floatingState)
    {
        // Wait the allotted time (the cascade effect)
        yield return new WaitForSeconds(delay);

        isFloating = floatingState;
        Vector3 startPos = transform.position;
        float time = 0;
        float duration = Vector3.Distance(startPos, target) / floatSpeed;

        // Move smoothly towards the destination
        while (time < duration)
        {
            transform.position = Vector3.Lerp(startPos, target, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = target;

        // If we are floating, add a small "breathing" motion (Idle)
        if (isFloating)
        {
            StartCoroutine(HoverEffect());
        }
    }

    private IEnumerator HoverEffect()
    {
        float startY = transform.position.y;
        while (isFloating)
        {
            // Smooth sinusoidal motion
            float newY = startY + Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }
    }
}