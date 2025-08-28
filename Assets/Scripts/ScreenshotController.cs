// ScreenshotController.cs
// This component captures a screenshot when a specified key is pressed and saves it
// to a platform-independent, persistent data path with a timestamped filename.

// Required for handling system-level functionalities like DateTime.
using System;
// Required for file and directory operations, such as Path.Combine.
using System.IO;
// Required for all Unity-specific functionalities, including MonoBehaviour, Input, and ScreenCapture.
using UnityEngine;
// Required for managing asynchronous operations like coroutines.
using System.Collections;

public class ScreenshotController : MonoBehaviour
{

    public KeyCode screenshotKey = KeyCode.P;

    /// <summary>
    /// Called once per frame by the Unity engine.
    /// This method is used to check for user input.
    /// </summary>
    void Update()
    {
        // Input.GetKeyDown returns true only during the single frame the user
        // starts pressing down the specified key. This prevents multiple
        // screenshots from being taken if the key is held down.
        if (Input.GetKeyDown(screenshotKey))
        {
            // Initiates the coroutine that handles the screen capture process.
            // A coroutine is used to delay the capture until the end of the frame,
            // ensuring the entire scene has been rendered.
            StartCoroutine(CaptureScreenshotCoroutine());
        }
    }

    /// <summary>
    /// A coroutine that waits for the end of the current frame, captures the screen,
    /// and saves it to a file.
    /// </summary>
    /// <returns>An IEnumerator used by Unity to manage the coroutine's execution.</returns>
    private IEnumerator CaptureScreenshotCoroutine()
    {
        // The 'yield return new WaitForEndOfFrame()' statement pauses the coroutine's
        // execution until all rendering for the current frame is complete. This is
        // crucial for capturing the final, fully rendered image, including UI.
        yield return new WaitForEndOfFrame();

        // --- 1. Define the File Path ---

        // Application.persistentDataPath provides a safe, writable directory path
        // that persists between application updates and is consistent across
        // different operating systems (Windows, macOS, iOS, Android, etc.).
        string folderPath = Application.persistentDataPath;

        // --- 2. Generate a Unique Filename ---

        // A timestamp is used to create a unique filename for each screenshot,
        // preventing files from being overwritten. The format "yyyy-MM-dd_HH-mm-ss"
        // is chosen because it is chronologically sortable and uses characters
        // that are safe for all major file systems.
        string fileName = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";

        // --- 3. Combine Path and Filename ---

        // System.IO.Path.Combine is the recommended method for constructing file paths.
        // It automatically uses the correct directory separator character ('\' or '/')
        // for the target operating system, preventing cross-platform compatibility issues.
        string filePath = Path.Combine(folderPath, fileName);

        // --- 4. Capture and Save the Screenshot ---

        // ScreenCapture.CaptureScreenshot takes the full file path and saves the
        // current game view as a PNG file. This operation is asynchronous and
        // may take a few frames to complete, especially for high-resolution captures.
        ScreenCapture.CaptureScreenshot(filePath);

        // --- 5. Provide User Feedback ---

        // A debug log confirms that the action was triggered and shows the developer
        // exactly where the file has been saved, which is invaluable for testing
        // and debugging across different platforms.
        Debug.Log($"Screenshot saved to: {filePath}");
    }
}