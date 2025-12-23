using NUnit.Framework;
using System;
using System.Collections;
using System.Globalization;
using System.Security.Claims;
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
    private string decommissioningTimeLapseStr, decommissioningStepStr;
    private string sideSlopeAngleStr;
    private string spiralRampSeparationStr, internalRampStraightRampHighStr;
    private string numGranite50tStr, numGranite60tStr, numGranite70tStr, numGranite80tStr;
    private string startCourseKingsStr, endCourseKingsStr, forcePerPullerStr, mezzanineRampAngleStr;
    private string mezzanineFrictionCoefStr, horizontalTransferStr, setupTimeStr, setupGroupsStr;
    private string frictionCapstanStr, capstanWrapAngleStr, pullingSpeedRampStr, pullingSpeedTerraceStr;

    private Vector2 scrollPosition; // For the scrollbar
    private bool showGUI = true; // To toggle GUI visibility

    // Booleans to control the state of each collapsible panel
    private bool showCoreParams = false;
    private bool showRampDetails = false;
    private bool showGraniteSettings = false;
    private bool showAdaptiveRamps = false;
    private bool showHeadway = false;
    private bool showDrawingOptions = false;
    private bool showVisibility = false;
    private bool showGraniteProject = false;
    private bool showLogging = false;
    private bool showDecommissioning = false;
    private bool showRampMethod = false;

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
        GUIStyle helpTextStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, fontSize = 10 };
        GUILayout.Label("Fly Cam: WASD/QE to move. Hold RMB to look. Shift to sprint.", helpTextStyle);
        GUILayout.Space(5); // add space after the header

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
        DrawCollapsiblePanel("Ramp Method", ref showRampMethod, DrawRampMethodPanel);
        DrawCollapsiblePanel("Core Parameters", ref showCoreParams, DrawCoreParameters);
        DrawCollapsiblePanel("Ramp Details", ref showRampDetails, DrawRampDetails);
        DrawCollapsiblePanel("Granite Settings", ref showGraniteSettings, DrawGraniteSettings);
        DrawCollapsiblePanel("Adaptive Ramp System", ref showAdaptiveRamps, DrawAdaptiveRampSystem);
        DrawCollapsiblePanel("Headway & Timings", ref showHeadway, DrawHeadwayAndTimings);
        DrawCollapsiblePanel("Drawing Options", ref showDrawingOptions, DrawDrawingOptions);
        DrawCollapsiblePanel("Element Visibility", ref showVisibility, DrawElementVisibility);
        DrawCollapsiblePanel("Granite Megalith Project", ref showGraniteProject, DrawGraniteProjectPanel);
        DrawCollapsiblePanel("Decommissioning", ref showDecommissioning, DrawDecommissioningPanel);
        DrawCollapsiblePanel("Logging & Export", ref showLogging, DrawLoggingOptions);

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

    private void DrawRampMethodPanel()
    {
        generatePyramid.rampMethod = (RampMethodType)GUILayout.Toolbar((int)generatePyramid.rampMethod, Enum.GetNames(typeof(RampMethodType)));
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
        generatePyramid.MethodInsideRamp = GUILayout.Toggle(generatePyramid.MethodInsideRamp, "Inside Ramp");
        GUILayout.BeginHorizontal();
        generatePyramid.Method2Ramp = GUILayout.Toggle(generatePyramid.Method2Ramp, "2-Ramp Method");
        generatePyramid.Method4Ramp = GUILayout.Toggle(generatePyramid.Method4Ramp, "4-Ramp Method");        
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.Method8Ramp = GUILayout.Toggle(generatePyramid.Method8Ramp, "8-Ramp Method");
        generatePyramid.Method16Ramp = GUILayout.Toggle(generatePyramid.Method16Ramp, "16-Ramp Method");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUIStyle subHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("Single Ramp Start Face", subHeaderStyle);
        generatePyramid.SingleRampFaceStart = (RampPositionFace)GUILayout.Toolbar((int)generatePyramid.SingleRampFaceStart, Enum.GetNames(typeof(RampPositionFace)));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Straight Ramp Orientation", subHeaderStyle);
        generatePyramid.StraightRampFace = (RampPositionFace)GUILayout.Toolbar((int)generatePyramid.StraightRampFace, Enum.GetNames(typeof(RampPositionFace)));
        DrawLabeledTextField("Side Slope Angle (°)", ref sideSlopeAngleStr);
        DrawLabeledTextField("Spiral Sep. (m)", ref spiralRampSeparationStr);
        DrawLabeledTextField("Internal Ramp H (m)", ref internalRampStraightRampHighStr);
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
        generatePyramid.DrawCasing = GUILayout.Toggle(generatePyramid.DrawCasing, "Casing");
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
        generatePyramid.DrawPyramidInterior = GUILayout.Toggle(generatePyramid.DrawPyramidInterior, "Pyramid interior");
        generatePyramid.DrawPyramidInteriorTransparent = GUILayout.Toggle(generatePyramid.DrawPyramidInteriorTransparent, "Pyramid interior transparent");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        generatePyramid.ShowKhufuNotchs = GUILayout.Toggle(generatePyramid.ShowKhufuNotchs, "Show Khufu Notchs");
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUIStyle subHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("Camera Position", subHeaderStyle);
        generatePyramid.cameraPositionFace = (CameraPositionFace)GUILayout.Toolbar((int)generatePyramid.cameraPositionFace, Enum.GetNames(typeof(CameraPositionFace)));       
    }

    private void DrawGraniteProjectPanel()
    {
        generatePyramid.showInfoGranite = GUILayout.Toggle(generatePyramid.showInfoGranite, "Log Granite Calculations (CSV)");

        GUILayout.Space(5);
        DrawLabeledTextField("Num. 50-ton Blocks", ref numGranite50tStr);
        DrawLabeledTextField("Num. 60-ton Blocks", ref numGranite60tStr);
        DrawLabeledTextField("Num. 70-ton Blocks", ref numGranite70tStr);
        DrawLabeledTextField("Num. 80-ton Blocks", ref numGranite80tStr);
        GUILayout.Space(5);
        DrawLabeledTextField("Start Course", ref startCourseKingsStr);
        DrawLabeledTextField("End Course", ref endCourseKingsStr);
        GUILayout.Space(5);
        DrawLabeledTextField("Force per Puller (N)", ref forcePerPullerStr);
        DrawLabeledTextField("Mezzanine Ramp (°)", ref mezzanineRampAngleStr);
        DrawLabeledTextField("Mezzanine Friction (μ)", ref mezzanineFrictionCoefStr);
        DrawLabeledTextField("Horizontal Dist (m)", ref horizontalTransferStr);
        DrawLabeledTextField("Ramp Pull Speed (m/s)", ref pullingSpeedRampStr);
        DrawLabeledTextField("Terrace Pull Speed (m/s)", ref pullingSpeedTerraceStr);
        DrawLabeledTextField("Setup Time (h)", ref setupTimeStr);
        DrawLabeledTextField("Setup Groups", ref setupGroupsStr);
        GUILayout.Space(5);
        generatePyramid.useCapstan = GUILayout.Toggle(generatePyramid.useCapstan, "Use Capstan");

        bool wasEnabled = GUI.enabled;
        GUI.enabled = generatePyramid.useCapstan;
        DrawLabeledTextField("Capstan Friction (μ)", ref frictionCapstanStr);
        DrawLabeledTextField("Capstan Wrap (rad)", ref capstanWrapAngleStr);
        GUI.enabled = wasEnabled;
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

    private void DrawDecommissioningPanel()
    {
        generatePyramid.Decomisioning = GUILayout.Toggle(generatePyramid.Decomisioning, "Enable Decommissioning");
        generatePyramid.AnimateDecommissioning = GUILayout.Toggle(generatePyramid.AnimateDecommissioning, "Animate Decommissioning");

        bool wasEnabled = GUI.enabled;
        GUI.enabled = generatePyramid.AnimateDecommissioning;
        DrawLabeledTextField("Time Lapse (s)", ref decommissioningTimeLapseStr);
        DrawLabeledTextField("Step (%)", ref decommissioningStepStr);
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

        if (float.TryParse(decommissioningTimeLapseStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float timeLapse)) generatePyramid.DecommissioningTimeLapse = timeLapse;
        if (float.TryParse(decommissioningStepStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float step)) generatePyramid.DecommissioningStep = step / 100.0f;

        // Apply Ramp Geometry
        if (float.TryParse(rampInclinationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val)) generatePyramid.RampInclination = val;
        if (float.TryParse(sideSlopeAngleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val)) generatePyramid.SideSlopeAngle = val;
        if (int.TryParse(spiralRampSeparationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out int valVal9)) generatePyramid.spiralRampSeparation = valVal9;
        if (float.TryParse(internalRampStraightRampHighStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val)) generatePyramid.internalRampStraightRampHigh = val;

        // Apply Granite Project Settings
        if (int.TryParse(numGranite50tStr, out intVal)) generatePyramid.numberOfGranite50tons = intVal;
        if (int.TryParse(numGranite60tStr, out intVal)) generatePyramid.numberOfGranite60tons = intVal;
        if (int.TryParse(numGranite70tStr, out intVal)) generatePyramid.numberOfGranite70tons = intVal;
        if (int.TryParse(numGranite80tStr, out intVal)) generatePyramid.numberOfGranite80tons = intVal;
        if (int.TryParse(startCourseKingsStr, out intVal)) generatePyramid.startCourseKingsChamber = intVal;
        if (int.TryParse(endCourseKingsStr, out intVal)) generatePyramid.endCourseKingsChamber = intVal;
        if (int.TryParse(setupGroupsStr, out intVal)) generatePyramid.setupTimePerCourseGroups = intVal;
        if (float.TryParse(forcePerPullerStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal1)) generatePyramid.forcePerPullerNewtons = floatVal1;
        if (float.TryParse(mezzanineRampAngleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal2)) generatePyramid.mezzanineRampAngleDegrees = floatVal2;
        if (float.TryParse(mezzanineFrictionCoefStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal3)) generatePyramid.mezzanineFrictionCoef = floatVal3;
        if (float.TryParse(horizontalTransferStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal4)) generatePyramid.horizontalTransferDistanceMeters = floatVal4;
        if (float.TryParse(setupTimeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal5)) generatePyramid.setupTimePerCourseHours = floatVal5;
        if (float.TryParse(pullingSpeedRampStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal6)) generatePyramid.pullingSpeedRampMetersPerSecond = floatVal6;
        if (float.TryParse(pullingSpeedTerraceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal7)) generatePyramid.pullingSpeedTerraceMetersPerSecond = floatVal7;
        if (float.TryParse(frictionCapstanStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal8)) generatePyramid.frictionCoefCapstan = floatVal8;
        if (float.TryParse(capstanWrapAngleStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal9)) generatePyramid.capstanWrapAngleRadians = floatVal9;
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

        decommissioningTimeLapseStr = generatePyramid.DecommissioningTimeLapse.ToString("F2", CultureInfo.InvariantCulture);
        decommissioningStepStr = (generatePyramid.DecommissioningStep * 100.0f).ToString("F2", CultureInfo.InvariantCulture);

        // Update Ramp Geometry
        rampInclinationStr = generatePyramid.RampInclination.ToString("F2", CultureInfo.InvariantCulture);
        sideSlopeAngleStr = generatePyramid.SideSlopeAngle.ToString("F2", CultureInfo.InvariantCulture);
        spiralRampSeparationStr = generatePyramid.spiralRampSeparation.ToString("F2", CultureInfo.InvariantCulture);
        internalRampStraightRampHighStr = generatePyramid.internalRampStraightRampHigh.ToString("F2", CultureInfo.InvariantCulture);

        // Update Granite Project Fields
        numGranite50tStr = generatePyramid.numberOfGranite50tons.ToString();
        numGranite60tStr = generatePyramid.numberOfGranite60tons.ToString();
        numGranite70tStr = generatePyramid.numberOfGranite70tons.ToString();
        numGranite80tStr = generatePyramid.numberOfGranite80tons.ToString();
        startCourseKingsStr = generatePyramid.startCourseKingsChamber.ToString();
        endCourseKingsStr = generatePyramid.endCourseKingsChamber.ToString();
        setupGroupsStr = generatePyramid.setupTimePerCourseGroups.ToString();
        forcePerPullerStr = generatePyramid.forcePerPullerNewtons.ToString("F2", CultureInfo.InvariantCulture);
        mezzanineRampAngleStr = generatePyramid.mezzanineRampAngleDegrees.ToString("F2", CultureInfo.InvariantCulture);
        mezzanineFrictionCoefStr = generatePyramid.mezzanineFrictionCoef.ToString("F2", CultureInfo.InvariantCulture);
        horizontalTransferStr = generatePyramid.horizontalTransferDistanceMeters.ToString("F2", CultureInfo.InvariantCulture);
        setupTimeStr = generatePyramid.setupTimePerCourseHours.ToString("F2", CultureInfo.InvariantCulture);
        pullingSpeedRampStr = generatePyramid.pullingSpeedRampMetersPerSecond.ToString("F2", CultureInfo.InvariantCulture);
        pullingSpeedTerraceStr = generatePyramid.pullingSpeedTerraceMetersPerSecond.ToString("F2", CultureInfo.InvariantCulture);
        frictionCapstanStr = generatePyramid.frictionCoefCapstan.ToString("F2", CultureInfo.InvariantCulture);
        capstanWrapAngleStr = generatePyramid.capstanWrapAngleRadians.ToString("F2", CultureInfo.InvariantCulture);
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

