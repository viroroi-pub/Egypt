using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a UI progress bar. It is designed to work with a Canvas set to "Screen Space - Camera"
/// to ensure it is centered in the player's view.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class ProgressBarUI : MonoBehaviour
{
    [Tooltip("The UI Slider component for the progress bar.")]
    public Slider progressBar;
    [Tooltip("The UI Text component to display messages.")]
    public Text progressText;
    [Tooltip("The parent GameObject of the bar and text, used to show/hide everything at once.")]
    public GameObject container;

    private Canvas canvas;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (container == null || progressBar == null || progressText == null)
        {
            Debug.LogError("ProgressBarUI is not fully set up in the Inspector!");
            return;
        }

        // Ensure the Canvas is configured correctly to overlay on the camera view.
        SetupCanvas();

        // Start hidden
        Hide();
    }

    /// <summary>
    /// Configures the Canvas to render in camera space, ensuring it's always visible.
    /// </summary>
    private void SetupCanvas()
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;

        if (canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        canvas.planeDistance = 1; // A comfortable distance from the camera
    }

    /// <summary>
    /// Shows the progress bar and sets its initial message.
    /// </summary>
    public void Show(string message = "")
    {
        if (container) container.SetActive(true);
        //SetProgress(0, message);
        if (progressText) progressText.text = message;
        // This is the key line: Force the canvas to redraw its elements immediately.
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Hides the progress bar.
    /// </summary>
    public void Hide()
    {
        if (container) container.SetActive(false);
    }

    /// <summary>
    /// Updates the progress bar's value and text.
    /// </summary>
    /// <param name="progress">A value between 0.0 and 1.0.</param>
    /// <param name="message">The text to display.</param>
    public void SetProgress(float progress, string message)
    {
        if (progressBar) progressBar.value = Mathf.Clamp01(progress);
        if (progressText) progressText.text = message;

        // This is the key line: Force the canvas to redraw its elements immediately.
        Canvas.ForceUpdateCanvases();
    }

    public void IncProgress(string message)
    {
        if (progressBar) progressBar.value++;
        if (progressText) progressText.text = message;

        // This is the key line: Force the canvas to redraw its elements immediately.
        Canvas.ForceUpdateCanvases();
    }

    public void SetMaxProgress(float progress)
    {
        if (progressBar) progressBar.maxValue = progress;

        // This is the key line: Force the canvas to redraw its elements immediately.
        Canvas.ForceUpdateCanvases();
    }
}

