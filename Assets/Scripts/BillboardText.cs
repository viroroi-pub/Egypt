using UnityEngine;

/// <summary>
/// Makes the object (like a TextMeshPro in World Space) always face the main camera.
// Also known as the "Billboard" effect.
/// </summary>
public class BillboardText : MonoBehaviour
{
    [Tooltip("Si es true, el texto solo rotará en el eje Y (horizontal). Ideal si no quieres que el texto se incline hacia arriba o abajo.")]
    public bool lockYAxis = false;

    public Camera mainCamera;

    // We use LateUpdate to ensure the camera has already moved before rotating the text
    void LateUpdate()
    {
        // Just in case the main camera changes or is destroyed
        if (mainCamera == null) return;        

        if (lockYAxis)
        {
            // Calculate the direction towards the camera but cancel the height difference (Y-axis)
            Vector3 directionToCamera = mainCamera.transform.position - transform.position;
            directionToCamera.y = 0;

            // We use `-directionToCamera` so that the front of the text faces the camera. (otherwise, the text would be mirrored/upside down)
            if (directionToCamera != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
        else
        {
            // We simply match the text rotation to the camera rotation.
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}