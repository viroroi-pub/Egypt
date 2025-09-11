using UnityEngine;

/// <summary>
/// A simple and smooth fly-through camera controller.
/// Controls:
/// - WASD to move horizontally.
/// - Q and E to move vertically.
/// - Hold Right Mouse Button to look around.
/// - Hold Left Shift to move faster.
/// - Mouse Scroll Wheel to adjust base movement speed.
/// </summary>
public class FlyCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Base speed of the camera movement.")]
    public float speed = 40.0f; // CHANGED: Increased for large scale

    [Tooltip("Multiplier for speed when holding the sprint key (Left Shift).")]
    public float sprintMultiplier = 3.0f;

    [Tooltip("Amount to increase/decrease speed with the scroll wheel.")]
    public float speedChangeAmount = 5.0f; // CHANGED: Faster speed adjustment

    [Header("Rotation Settings")]
    [Tooltip("Sensitivity of the mouse for looking around.")]
    public float sensitivity = 1.0f;

    [Tooltip("Smoothing factor for camera rotation. Lower values are smoother.")]
    public float rotationSmoothTime = 0.05f;

    private Vector3 currentRotation;
    private Vector3 rotationVelocity;
    private float yaw;
    private float pitch;

    void Start()
    {
        // Initialize rotation angles from the camera's starting orientation
        Vector3 startAngles = transform.eulerAngles;
        yaw = startAngles.y;
        pitch = startAngles.x;
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    /// <summary>
    /// Handles camera rotation based on mouse input when the right mouse button is held.
    /// </summary>
    private void HandleRotation()
    {
        // Only rotate when the right mouse button is held down
        if (Input.GetMouseButton(1))
        {
            // Lock and hide the cursor for a seamless experience
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Get mouse input
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;

            // Clamp the pitch to prevent the camera from flipping over
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            // Smooth the rotation
            currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref rotationVelocity, rotationSmoothTime);
            transform.eulerAngles = currentRotation;
        }
        else
        {
            // When the right mouse button is released, unlock and show the cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Handles camera movement based on keyboard input.
    /// </summary>
    private void HandleMovement()
    {
        // Adjust speed with the mouse scroll wheel
        speed += Input.mouseScrollDelta.y * speedChangeAmount;
        speed = Mathf.Max(speed, 1.0f); // Ensure speed doesn't go below 1

        // Determine current speed (sprint or normal)
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? speed * sprintMultiplier : speed;

        // Get movement input from WASD keys
        Vector3 moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;

        // Add vertical movement from Q and E keys
        if (Input.GetKey(KeyCode.E))
        {
            moveDirection.y = 1;
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            moveDirection.y = -1;
        }

        // Apply movement relative to the camera's orientation
        transform.position += transform.TransformDirection(moveDirection) * currentSpeed * Time.deltaTime;
    }
}
