using System;
using System.Collections;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This script creates a runtime GUI to control the GeneratePyramid script.
/// </summary>
public class PyramidGUIController : MonoBehaviour
{
    /// <summary>
    /// Assign the GeneratePyramid instance here from the Inspector.
    /// </summary>
    public GeneratePyramid generatePyramid;

    // Private variables to hold the string values from the GUI text fields.
    private string baseSizeStr, heightStr, angleStr, levelsStr;
    private string rampInclinationStr, blockHeightStr, blockWideStr;
    private string holeHeightStr, holeWideStr, drawRowStr, drawBlocksStr;
    private string numGranite1Str, numGranite2Str, minHeightGraniteStr, maxHeightGraniteStr;
    private string minBase2RampStr, minBase4RampStr, minBase8RampStr, minBase16RampStr;
    private string avgHeadwayStr, minHeadwayStr, maxHeadwayStr, workingMinutesStr;
    private string txtNameStr, csvIterNameStr, csvRowNameStr, csvHeadwayNameStr;
    private string exportSubFolderStr, outputFileNameStr;

    private Vector2 scrollPosition; // For the scrollbar
    private bool showGUI = true; // To toggle GUI visibility

    // Booleans to control the state of each collapsible panel
    private bool showCoreParams = true;
    private bool showRampDetails = false;
    private bool showGraniteSettings = false;
    private bool showAdaptiveRamps = false;
    private bool showHeadway = false;
    private bool showDrawingOptions = true;
    private bool showVisibility = false;
    private bool showLogging = false;

    void Start()
    {
        if (generatePyramid == null)
        {
            Debug.LogError("GeneratePyramid is not assigned in the PyramidGUIController. Please assign it in the Inspector.");
            this.enabled = false; // Disable this script if there's nothing to control.
            return;
        }

        // Initialize the text fields with the pyramid's current values.
        UpdateFieldsFromScript();
    }

    void Update()
    {
        // Press F1 to show or hide the menu
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showGUI = !showGUI;
        }
    }

    void OnGUI()
    {
        if (!showGUI || !generatePyramid.ShowGUI) return;

        // Style for the GUI box
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // Draw the main GUI window in the top-left corner.
        GUILayout.BeginArea(new Rect(10, 10, 550, 550), boxStyle);
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUILayout.Label("Pyramid Controller (F1 to toggle). Press 'P' for taking a screenshot.", headerStyle);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // --- ACTION BUTTONS ---
        if (GUILayout.Button("Apply Changes"))
        {
            ApplySettings();
            //generatePyramid.ClearPyramid(true);
            //generatePyramid.CreatePyramid();
            StartCoroutine(ShowPyramid());
        }

        if (GUILayout.Button("Reset to Defaults"))
        {
            generatePyramid.ResetValues();
            UpdateFieldsFromScript();
            //generatePyramid.ClearPyramid(true);
            //generatePyramid.CreatePyramid(); // Optional: rebuild immediately on reset
        }

        if (GUILayout.Button("Delete Pyramid"))
        {
            generatePyramid.ClearPyramid(true);
        }

        // --- PYRAMID TYPE SELECTION ---
        DrawMultiRowToolbarAndHandleClick();

        // Disable manual editing if a predefined type is selected
        //GUI.enabled = generatePyramid.selectedPyramid == PyramidType.Default;

        // --- MANUAL PARAMETERS ---
        DrawCollapsiblePanel("Core Parameters", ref showCoreParams, DrawCoreParameters);
        DrawCollapsiblePanel("Ramp Details", ref showRampDetails, DrawRampDetails);
        DrawCollapsiblePanel("Granite Settings", ref showGraniteSettings, DrawGraniteSettings);
        DrawCollapsiblePanel("Adaptive Ramp System", ref showAdaptiveRamps, DrawAdaptiveRampSystem);
        DrawCollapsiblePanel("Headway & Timings", ref showHeadway, DrawHeadwayAndTimings);
        DrawCollapsiblePanel("Drawing Options", ref showDrawingOptions, DrawDrawingOptions);
        DrawCollapsiblePanel("Element Visibility", ref showVisibility, DrawElementVisibility);
        DrawCollapsiblePanel("Logging & Export", ref showLogging, DrawLoggingOptions); // New Panel

        // --- exit ---
        GUILayout.FlexibleSpace(); 

        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); 

        if (GUILayout.Button("Quit Application"))
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else            
            Application.Quit();
#endif
        }

        GUI.backgroundColor = Color.white; // Restaurar color por defecto

        GUI.enabled = true; // Re-enable the GUI for the buttons

        //GUILayout.Space(10);        

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Draws a grid of buttons and handles the click logic internally.
    /// This function now has side effects: it modifies generatePyramid directly.
    /// </summary>
    private void DrawMultiRowToolbarAndHandleClick()
    {
        string[] pyramidNames = Enum.GetNames(typeof(PyramidType));
        int buttonsPerRow = 4;
        int currentSelectionIndex = (int)generatePyramid.selectedPyramid;

        GUILayout.BeginVertical();

        for (int i = 0; i < pyramidNames.Length; i += buttonsPerRow)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < buttonsPerRow; j++)
            {
                int index = i + j;
                if (index < pyramidNames.Length)
                {
                    // Highlight the selected button
                    GUI.backgroundColor = (index == currentSelectionIndex) ? Color.cyan : Color.white;

                    if (GUILayout.Button(pyramidNames[index]))
                    {
                        // Check if a *different* button was clicked
                        if (index != currentSelectionIndex)
                        {
                            generatePyramid.selectedPyramid = (PyramidType)index;
                            generatePyramid.onChangePyramidType();
                            UpdateFieldsFromScript();

                            // If a preset is chosen, regenerate the pyramid immediately
                            if (generatePyramid.selectedPyramid != PyramidType.Default)
                            {
                                //generatePyramid.CreatePyramid();
                                //UpdateFieldsFromScript();
                            }
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        GUI.backgroundColor = Color.white; // Reset to default color
        GUILayout.EndVertical();
    }

    private void DrawCoreParameters()
    {
        DrawLabeledTextField("Base Size (m)", ref baseSizeStr);
        DrawLabeledTextField("Height (m)", ref heightStr);
        DrawLabeledTextField("Pyramid Incl. (°)", ref angleStr);
    }

    private void DrawRampDetails()
    {
        DrawLabeledTextField("Ramp Inclination (°)", ref rampInclinationStr);
        DrawLabeledTextField("Passage Height (blocks)", ref holeHeightStr);
        DrawLabeledTextField("Passage Width (blocks)", ref holeWideStr);        
        generatePyramid.showRamps = GUILayout.Toggle(generatePyramid.showRamps, "Show Ramps");
        GUILayout.BeginHorizontal();
        generatePyramid.Method4Ramp = GUILayout.Toggle(generatePyramid.Method4Ramp, "4-Ramp Method");
        generatePyramid.MethodInsideRamp = GUILayout.Toggle(generatePyramid.MethodInsideRamp, "Inside Ramp");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.Method8Ramp = GUILayout.Toggle(generatePyramid.Method8Ramp, "8-Ramp Method");
        generatePyramid.Method16Ramp = GUILayout.Toggle(generatePyramid.Method16Ramp, "16-Ramp Method");
        GUILayout.EndHorizontal();
    }

    private void DrawGraniteSettings()
    {
        DrawLabeledTextField("Num Granite Blocks 1", ref numGranite1Str);
        DrawLabeledTextField("Num Granite Blocks 2", ref numGranite2Str);
        DrawLabeledTextField("Min Height Granite (m)", ref minHeightGraniteStr);
        DrawLabeledTextField("Max Height Granite (m)", ref maxHeightGraniteStr);
    }

    private void DrawAdaptiveRampSystem()
    {
        DrawLabeledTextField("Min Base for 2 Ramps", ref minBase2RampStr);
        DrawLabeledTextField("Min Base for 4 Ramps", ref minBase4RampStr);
        DrawLabeledTextField("Min Base for 8 Ramps", ref minBase8RampStr);
        DrawLabeledTextField("Min Base for 16 Ramps", ref minBase16RampStr);
    }

    private void DrawHeadwayAndTimings()
    {
        generatePyramid.PyramidHeadwayType = (PyramidHeadwayType)GUILayout.Toolbar((int)generatePyramid.PyramidHeadwayType, Enum.GetNames(typeof(PyramidHeadwayType)));
        DrawLabeledTextField("Average Headway", ref avgHeadwayStr);
        DrawLabeledTextField("Min Headway", ref minHeadwayStr);
        DrawLabeledTextField("Max Headway", ref maxHeadwayStr);
        DrawLabeledTextField("Working Mins/Year", ref workingMinutesStr);
    }

    private void DrawDrawingOptions()
    {
        GUILayout.BeginHorizontal();
        generatePyramid.DrawUntilRow = GUILayout.Toggle(generatePyramid.DrawUntilRow, "Draw Until Row");
        generatePyramid.DrawOnlyRow = GUILayout.Toggle(generatePyramid.DrawOnlyRow, "Draw Only Row");
        GUILayout.EndHorizontal();
        DrawLabeledTextField("Target Row", ref drawRowStr);
        DrawLabeledTextField("Outer Layers", ref drawBlocksStr);
    }

    private void DrawElementVisibility()
    {
        GUILayout.BeginHorizontal();
        generatePyramid.DrawWall = GUILayout.Toggle(generatePyramid.DrawWall, "Walls");
        generatePyramid.DrawFloor = GUILayout.Toggle(generatePyramid.DrawFloor, "Floors");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.DrawCover = GUILayout.Toggle(generatePyramid.DrawCover, "Cover");
        generatePyramid.DrawAll = GUILayout.Toggle(generatePyramid.DrawAll, "Draw All");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.DrawWoodenCyl = GUILayout.Toggle(generatePyramid.DrawWoodenCyl, "Cylinders");
        generatePyramid.DrawEgyptians = GUILayout.Toggle(generatePyramid.DrawEgyptians, "Workers");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.DrawGranite = GUILayout.Toggle(generatePyramid.DrawGranite, "Granite");        
        generatePyramid.halfPyramid = GUILayout.Toggle(generatePyramid.halfPyramid, "Half Pyramid");        
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.ShowKhufuNotchs = GUILayout.Toggle(generatePyramid.ShowKhufuNotchs, "Show Khufu Notchs");
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUIStyle subHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("Camera Position", subHeaderStyle);
        generatePyramid.cameraPositionFace = (CameraPositionFace)GUILayout.Toolbar((int)generatePyramid.cameraPositionFace, Enum.GetNames(typeof(CameraPositionFace)));
    }

    private void DrawLoggingOptions()
    {
        GUIStyle subHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("Log Files", subHeaderStyle);
        DrawLabeledTextField("TXT Filename", ref txtNameStr);
        DrawLabeledTextField("CSV Iter Filename", ref csvIterNameStr);
        DrawLabeledTextField("CSV Row Filename", ref csvRowNameStr);
        DrawLabeledTextField("CSV Headway Filename", ref csvHeadwayNameStr);

        GUILayout.Space(5);

        generatePyramid.showInfoLevel = GUILayout.Toggle(generatePyramid.showInfoLevel, "Log Level Info");
        generatePyramid.showInfoLevelTotal = GUILayout.Toggle(generatePyramid.showInfoLevelTotal, "Log Level Totals");
        generatePyramid.showInfoLevelDec = GUILayout.Toggle(generatePyramid.showInfoLevelDec, "Log Level Decrements");
        generatePyramid.showInfoRow = GUILayout.Toggle(generatePyramid.showInfoRow, "Log Row Info");

        GUILayout.Space(10);
        GUILayout.Label("OBJ Export", subHeaderStyle);
        generatePyramid.exportPyramidObj = GUILayout.Toggle(generatePyramid.exportPyramidObj, "Enable OBJ Export");

        bool wasEnabled = GUI.enabled;
        GUI.enabled = generatePyramid.exportPyramidObj;

        generatePyramid.exportCombineMeshes = GUILayout.Toggle(generatePyramid.exportCombineMeshes, "Combine Meshes");
        DrawLabeledTextField("Export Subfolder", ref exportSubFolderStr);
        DrawLabeledTextField("Export Filename", ref outputFileNameStr);

        GUI.enabled = wasEnabled;
    }


    /// <summary>
    /// Helper function to draw a collapsible panel.
    /// </summary>
    private void DrawCollapsiblePanel(string title, ref bool showPanel, Action drawContent)
    {
        string arrow = showPanel ? "▼" : "►";
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.alignment = TextAnchor.MiddleLeft;

        if (GUILayout.Button(arrow + " " + title, buttonStyle))
        {
            showPanel = !showPanel;
        }

        if (showPanel)
        {
            // Indent the content for better visual structure
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            GUILayout.BeginVertical();

            drawContent();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

    private IEnumerator ShowPyramid()
    {
        generatePyramid.ClearPyramid(true);

        yield return new WaitForSeconds(1.0f);

        yield return new WaitForEndOfFrame(); // Wait until the end of the frame to ensure the pyramid is cleared
        generatePyramid.CreatePyramid();
    }

    /// <summary>
    /// Helper function to draw a label and a text field on the same line.
    /// </summary>
    private void DrawLabeledTextField(string label, ref string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(120));
        value = GUILayout.TextField(value);
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Applies the values from the text fields to the GeneratePyramid script.
    /// </summary>
    private void ApplySettings()
    {
        if (float.TryParse(baseSizeStr, out float baseSize)) generatePyramid.BaseSize = baseSize;
        if (float.TryParse(heightStr, out float height)) generatePyramid.Height = height;
        if (float.TryParse(angleStr, out float angle)) generatePyramid.PyramidInclination = angle;
        if (int.TryParse(levelsStr, out int levels)) generatePyramid.DrawRow = levels;

        // Apply new settings
        if (float.TryParse(rampInclinationStr, out float rampInclination)) generatePyramid.RampInclination = rampInclination;
        if (float.TryParse(blockHeightStr, out float blockH)) generatePyramid.blockheight = blockH;
        if (float.TryParse(blockWideStr, out float blockW)) generatePyramid.blockwide = blockW;

        // Ramp Details & Drawing
        if (int.TryParse(holeHeightStr, out int hH)) generatePyramid.holeHeight = hH;
        if (int.TryParse(holeWideStr, out int hW)) generatePyramid.holeWide = hW;
        if (int.TryParse(drawRowStr, out int dR)) generatePyramid.DrawRow = dR;
        if (int.TryParse(drawBlocksStr, out int dB)) generatePyramid.DrawBlocks = dB;

        if (int.TryParse(numGranite1Str, out int intVal)) generatePyramid.numOfGraniteRock1 = intVal;
        if (int.TryParse(numGranite2Str, out int intVal2)) generatePyramid.numOfGraniteRock2 = intVal2;
        if (int.TryParse(minHeightGraniteStr, out int intVal3)) generatePyramid.minHeightGraniteRock = intVal3;
        if (int.TryParse(maxHeightGraniteStr, out int intVal4)) generatePyramid.maxHeightGraniteRock = intVal4;
        if (int.TryParse(minBase2RampStr, out int intVal5)) generatePyramid.minBaseSize2Ramps = intVal5;
        if (int.TryParse(minBase4RampStr, out int intVal6)) generatePyramid.minBaseSize4Ramps = intVal6;
        if (int.TryParse(minBase8RampStr, out int intVal7)) generatePyramid.minBaseSize8Ramps = intVal7;
        if (int.TryParse(minBase16RampStr, out int intVal8)) generatePyramid.minBaseSize16Ramps = intVal8;
        if (float.TryParse(avgHeadwayStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float val)) generatePyramid.AverageHeadway = val;
        if (float.TryParse(minHeadwayStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float val1)) generatePyramid.MinHeadway = val1;
        if (float.TryParse(maxHeadwayStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float val2)) generatePyramid.MaxHeadway = val2;
        if (float.TryParse(workingMinutesStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float val3)) generatePyramid.WorkingYearMinutes = val3;

        generatePyramid.txtname = txtNameStr;
        generatePyramid.csvitername = csvIterNameStr;
        generatePyramid.csvrowname = csvRowNameStr;
        generatePyramid.csvheadway = csvHeadwayNameStr;

        generatePyramid.exportSubFolder = exportSubFolderStr;
        generatePyramid.outputFileName = outputFileNameStr;
    }

    /// <summary>
    /// Updates the GUI text fields with the values from the GeneratePyramid script.
    /// </summary>
    private void UpdateFieldsFromScript()
    {
        baseSizeStr = generatePyramid.BaseSize.ToString();
        heightStr = generatePyramid.Height.ToString();
        angleStr = generatePyramid.PyramidInclination.ToString();
        levelsStr = generatePyramid.DrawRow.ToString();

        // Update new fields
        rampInclinationStr = generatePyramid.RampInclination.ToString();
        blockHeightStr = generatePyramid.blockheight.ToString();
        blockWideStr = generatePyramid.blockwide.ToString();

        // Ramp Details & Drawing
        holeHeightStr = generatePyramid.holeHeight.ToString();
        holeWideStr = generatePyramid.holeWide.ToString();
        drawRowStr = generatePyramid.DrawRow.ToString();
        drawBlocksStr = generatePyramid.DrawBlocks.ToString();

        numGranite1Str = generatePyramid.numOfGraniteRock1.ToString();
        numGranite2Str = generatePyramid.numOfGraniteRock2.ToString();
        minHeightGraniteStr = generatePyramid.minHeightGraniteRock.ToString();
        maxHeightGraniteStr = generatePyramid.maxHeightGraniteRock.ToString();
        minBase2RampStr = generatePyramid.minBaseSize2Ramps.ToString();
        minBase4RampStr = generatePyramid.minBaseSize4Ramps.ToString();
        minBase8RampStr = generatePyramid.minBaseSize8Ramps.ToString();
        minBase16RampStr = generatePyramid.minBaseSize16Ramps.ToString();
        avgHeadwayStr = generatePyramid.AverageHeadway.ToString("F2", CultureInfo.InvariantCulture);
        minHeadwayStr = generatePyramid.MinHeadway.ToString("F2", CultureInfo.InvariantCulture);
        maxHeadwayStr = generatePyramid.MaxHeadway.ToString("F2", CultureInfo.InvariantCulture);
        workingMinutesStr = generatePyramid.WorkingYearMinutes.ToString("F2", CultureInfo.InvariantCulture);

        txtNameStr = generatePyramid.txtname;
        csvIterNameStr = generatePyramid.csvitername;
        csvRowNameStr = generatePyramid.csvrowname;
        csvHeadwayNameStr = generatePyramid.csvheadway;

        exportSubFolderStr = generatePyramid.exportSubFolder;
        outputFileNameStr = generatePyramid.outputFileName;
    }

    // Utility to create a texture for the GUI background
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

}

