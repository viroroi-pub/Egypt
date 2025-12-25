using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;
using static UnityEngine.UI.GridLayoutGroup;

/// <summary>
/// Defines the type of ramp construction method to be used.
/// </summary>
public enum RampMethodType
{
    /// <summary>
    /// A straight ramp approaching one face of the pyramid. Arnold (1991), Lehner (1997)
    /// </summary>
    Straight,
    /// <summary>
    /// A spiral ramp that wraps around the pyramid.
    /// </summary>
    Spiral,
    /// <summary>
    /// A ramp built inside the body of the pyramid (Houdin).
    /// </summary>
    Internal,
    /// <summary>
    /// Ramps integrated into the main axis or edges of the pyramid structure.
    /// </summary>
    Integrated
}

// Defines the pyramid types for the dropdown menu in the Inspector.
public enum PyramidType
{
    Default, // Uses the manual values from the Inspector.
    Khufu,
    Khafre,
    Menkaure,
    Bent_bottom,    // Sneferu's Bent Pyramid Bottom
    Bent_top,    // Sneferu's Bent Pyramid 
    Red      // Sneferu's Red Pyramid.
}

// Defines the pyramid algorithm headway for the dropdown menu in the Inspector.
public enum PyramidHeadwayType
{
    Single_Ramp,
    Double_Ramp,
    Four_Ramp,
    Adaptative
}

// Defines the camera position face
public enum CameraPositionFace
{
    Default,
    NorthFace,
    EastFace,
    SouthFace,
    WestFace,
    Zenit,
    InfrontRamp
}

// Defines the camera position face
public enum RampPositionFace
{
    NorthFace,
    EastFace,
    SouthFace,
    WestFace
}

/// <summary>
/// Helper class to compare GameObjects based on their Y position in descending order.
/// This is necessary for the BinarySearch method to work correctly.
/// </summary>
public class GameObjectYComparer : IComparer<GameObject>
{
    public int Compare(GameObject a, GameObject b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        // Use CompareTo for robust comparison and sort in descending order (highest first).
        return b.transform.position.y.CompareTo(a.transform.position.y);
    }
}

/// <summary>
/// Data structure for storing turning points (iterations)
/// del modelo IER.
/// </summary>
public struct TurningPoint
{
    public int Iteration; // iteration (1, 2, 3...)
    public float Height;  // height in meters
    public int Course;    // Course (row) number
    public int Blocks; // Course blocks number

    public TurningPoint(int iteration, float height, int course, int blocks)
    {
        Iteration = iteration;
        Height = height;
        Course = course;
        Blocks = blocks;
    }

    public override string ToString()
    {
        return $"Turn {Iteration}: Height {Height:F2} m (Course {Course}, blocks {Blocks})";
    }
}

/// <summary>
/// Structure to return the results of the previous ramp calculation.
// </summary>
public struct RampTargetMetrics
{
    public Vector3 Position; // The central position of the ramp in the 3D world
    public float Height; // The exact height of the ramp's ground at that point
    public int FaceIndex; // 0=North, 1=West, 2=South, 3=East
    public string FaceName; // Human-readable name of the face
    public bool IsValid; // If the calculation was successful (the row exists)
    public int Level;    // level of iteration IER
}

// Defines the granite pullers to draw
public enum DrawGranitePullers
{
    pullers10t,
    pullers40t,
    pullers50t,
    pullers60t,
    pullers70t,
    pullers80t,
    pullersmean
}

public class GeneratePyramid : MonoBehaviour
{
    public RampMethodType rampMethod = RampMethodType.Integrated;
    // --- Pyramid and Ramp Geometry ---
    /// <summary>
    /// Select a predefined pyramid type to automatically set its dimensions.
    /// </summary>
    public PyramidType selectedPyramid = PyramidType.Default;
    /// <summary>
    /// The size of the pyramid's base in meters.
    /// </summary>
    public float BaseSize = 230;
    /// <summary>
    /// The total height of the pyramid in meters. 147m is the height of the Great Pyramid of Khufu.
    /// </summary>
    public float Height = 147; // 147 is the height of the pyramid of Khufu
    /// <summary>
    /// The inclination angle of the pyramid's faces in degrees.
    /// </summary>
    public float PyramidInclination = 51.84f;
    /// <summary>
    /// The inclination angle of the construction ramp in degrees.
    /// </summary>
    public float RampInclination = 7;
    /// </summary>
    /// block average height in meters.
    public float blockheight = 0.71f;
    /// </summary>
    /// block average wide in meters.
    public float blockwide = 1.27f;
    /// <summary>
    /// The width of the path or ramp.
    public float PathWide = 0;
    /// <summary>
    /// The separation of the path from the pyramid's edge.
    /// </summary>
    public float PathSeparation = 0;
    /// <summary>
    /// The height of the ramp's passage in block units.
    /// </summary>
    public int holeHeight = 6;
    /// <summary>
    /// The width of the ramp's passage in block units.
    /// </summary>
    public int holeWide = 3;
    /// <summary>
    /// The separation between individual blocks for visual clarity.
    /// </summary>
    public float blockSeparation = 0.0f; // separation between blocks
    /// <summary>
    /// Used setback for the width of the path or ramp.
    /// </summary>
    public bool setback = false;
    /// <summary>
    /// SideSloped, for straight and spiral rmaps
    /// </summary>
    public float SideSlopeAngle = 30.0f;
    /// <summary>
    /// spiral/internal ramp separation in block units.
    /// </summary>
    public int spiralRampSeparation = 2;
    /// <summary>
    /// internal ramp with straight ramp maximum height.
    /// </summary>
    public float internalRampStraightRampHigh = 40.0f;
    /// <summary>
    /// Transparency for see internal ramp
    /// </summary>
    public float pyramidTransparency = 0.25f;

    // --- Block and Calculation Data ---
    /// <summary>
    /// The total number of blocks calculated for the pyramid.
    /// </summary>
    public int numberOfBlocks = 0;
    /// <summary>
    /// The number of blocks actually rendered in the scene.
    /// </summary>
    public int numberOfBlocksDrawn = 0;
    /// <summary>
    /// A limit for the maximum number of blocks to draw, for performance reasons.
    /// </summary>
    public int maxBlocks = 100;
    /// <summary>
    /// The calculated total length of the ramp path.
    /// </summary>
    public float path_length = 0;
    /// <summary>
    /// The calculated total height of the pyramid after adjusting for block height.
    /// </summary>
    public float total_height = 0;
    /// <summary>
    /// The average mass of a single block in kilograms.
    /// </summary>
    public float massBlock = 2267.96f;
    /// <summary>
    /// The coefficient of friction used in physics calculations.
    /// </summary>
    public float frictionCoef = 0.7f;
    /// <summary>
    /// An option to select different ramp calculation methods.
    /// </summary>
    public int optionRamp = 0;
    /// <summary>
    /// The total calculated force required for construction.
    /// </summary>
    public float totalForce = 0;
    /// <summary>
    /// The portion of the total force expended on the ramp.
    /// </summary>
    public float totalForceRamp = 0;
    /// <summary>
    /// The total distance all blocks are moved.
    /// </summary>
    public float totalLength = 0;
    /// <summary>
    /// The portion of the total distance traveled on the ramp.
    /// </summary>
    public float totalLengthRamp = 0;
    /// <summary>
    /// Pyramid Volume
    /// </summary>
    public float PyramidVolume = 0f;
    /// <summary>
    /// Embankment Volume
    /// </summary>
    public float EmbankmentVolume = 0f;
    /// <summary>
    /// Single ramp IER face start
    /// </summary>
    public RampPositionFace SingleRampFaceStart = RampPositionFace.NorthFace;

    // --- Materials and GameObjects ---
    /// <summary>
    /// Material for the standard pyramid blocks.
    /// </summary>
    public Material m_Material;
    /// <summary>
    /// Material for the corner blocks.
    /// </summary>
    public Material m_Material_corner;
    /// <summary>
    /// Material for wooden elements like cylinders.
    /// </summary>
    public Material m_Material_wood;
    /// <summary>
    /// A blank or placeholder material.
    /// </summary>
    public Material m_Material_Blank;
    /// <summary>
    /// Material for the ramp floor.
    /// </summary>
    public Material m_Material_floor;
    /// <summary>
    /// Material for the adobe.
    /// </summary>
    public Material m_Material_adobe;
    /// <summary>
    /// Reference to the main camera in the scene.
    /// </summary>
    public Camera cam;
    /// <summary>
    /// Prefab for a palm tree GameObject.
    /// </summary>
    public GameObject Palm;
    /// <summary>
    /// Prefab for a dromedary (camel) GameObject.
    /// </summary>
    public GameObject Dromader;
    /// <summary>
    /// Prefab for the Eiffel Tower, likely for scale comparison.
    /// </summary>
    public GameObject Eiffel;
    /// <summary>
    /// Prefab for a human figure, for scale.
    /// </summary>
    public GameObject Man;
    /// <summary>
    /// Array of prefabs for standard blocks.
    /// </summary>
    public GameObject[] RockPrefab;
    /// <summary>
    /// Prefab for a divisible block.
    /// </summary>
    public GameObject RockDivPrefab;
    /// <summary>
    /// Prefab for corner pieces.
    /// </summary>
    public GameObject CornerPrefab;
    /// <summary>
    /// Parent GameObject to organize the generated blocks.
    /// </summary>
    public GameObject objParent;
    /// <summary>
    /// Prefab for granite block type 1.
    /// </summary>
    public GameObject graniteRockPrefab1;
    /// <summary>
    /// Prefab for granite block type 2.
    /// </summary>
    public GameObject graniteRockPrefab2;
    /// Prefab for granite block type 3. Limestone
    /// </summary>
    public GameObject graniteRockPrefab3;
    /// <summary>
    /// Prefab for the pyramidion (capstone).
    /// </summary>
    public GameObject piramidon;
    /// <summary>
    /// Prefab for terrace ramp
    /// </summary>
    public GameObject courseRampPrefab;
    /// <summary>
    /// Draw half terrace for granite blocks
    /// </summary>
    public bool DrawHalfCourseForGraniteBlocks=false;
    /// <summary>
    /// Draw pullers according to the block size
    /// </summary>
    public DrawGranitePullers DrawGranitePullers = DrawGranitePullers.pullersmean;
    /// <summary>
    /// Draw pullers according to the block size
    /// </summary>
    public int NumberOfRopesGroups = 12;
    /// <summary>
    /// Prefab for a stone sledge.
    /// </summary>
    public GameObject stone_sled;
    /// <summary>
    /// Prefab for an Egyptian worker figure.
    /// </summary>
    public GameObject Egyptian_body;

    // --- Generation and Display Options ---
    /// <summary>
    /// Toggles the visibility of the Eiffel Tower.
    /// </summary>
    public bool showEiffel = false;
    /// <summary>
    /// Toggles the visibility of the human figure at the base.
    /// </summary>
    public bool showMan = false;
    /// <summary>
    /// Toggles visibility of the human figure at the top of the ramp.
    /// </summary>
    public bool showManFinalRamp = false;
    /// <summary>
    /// Toggles the 2-ramp construction method.
    /// </summary>
    public bool Method2Ramp = false;
    /// <summary>
    /// Toggles the 4-ramp construction method.
    /// </summary>
    public bool Method4Ramp = false;
    /// <summary>
    /// Toggles the inside ramp construction method.
    /// </summary>
    public bool MethodInsideRamp = false;
    /// <summary>
    /// Toggles the 8-ramp construction method.
    /// </summary>
    public bool Method8Ramp = false;
    /// <summary>
    /// Toggles the 16-ramp construction method.
    /// </summary>
    public bool Method16Ramp = false;
    /// <summary>
    /// If true, draws the pyramid only up to a specific row.
    /// </summary>
    public bool DrawUntilRow = false;
    /// <summary>
    /// If true, draws only a specific row.
    /// </summary>
    public bool DrawOnlyRow = false;
    /// <summary>
    /// If true, draws the pyramid only up to a specific iteration (turn).
    /// </summary>
    public bool DrawUntilTurn = false;
    /// <summary>
    /// Toggles drawing the outer casing stones.
    /// </summary>
    public bool DrawCasing = false;
    /// <summary>
    /// The specific row to draw for DrawUntilRow or DrawOnlyRow.
    /// </summary>
    public int DrawRow = 0;
    /// <summary>
    /// The specific iter (turn) to draw for DrawUntilTurn or DrawOnlyTurn.
    /// </summary>
    public int DrawTurn = 0;
    /// <summary>
    /// The number of outer block layers to draw.
    /// </summary>
    public int DrawBlocks = 1;
    /// <summary>
    /// A counter for deleted blocks.
    /// </summary>
    public int DeletedBlocks = 0;
    /// <summary>
    /// Toggles drawing the ramp walls.
    /// </summary>
    public bool DrawWall = true;
    /// <summary>
    /// Toggles drawing the ramp floor.
    /// </summary>
    public bool DrawFloor = true;
    /// <summary>
    /// Toggles drawing wooden cylinders at corners.
    /// </summary>
    public bool DrawWoodenCyl = true;
    /// <summary>
    /// Toggles drawing Egyptian worker figures.
    /// </summary>
    public bool DrawEgyptians = true;
    /// <summary>
    /// Toggles drawing the granite blocks for the King's Chamber.
    /// </summary>
    public bool DrawGranite = true;
    /// <summary>
    /// A master toggle to draw all elements.
    /// </summary>
    public bool DrawAll = false;
    /// <summary>
    /// Toggles the visibility of the ramp structures.
    /// </summary>
    public bool showRamps = true;
    /// <summary>
    /// If true, only draws half of the pyramid for inspection.
    /// </summary>
    public bool halfPyramid = false;
    /// <summary>
    /// If true, Draw Pyramid interior
    /// </summary>
    public bool DrawPyramidInterior = false;
    /// <summary>
    /// If true, Draw Pyramid interior transparent
    /// </summary>
    public bool DrawPyramidInteriorTransparent = true;
    /// <summary>
    /// Pyramid interior GameObject
    /// </summary>
    public GameObject PyramidInterior;
    /// <summary>
    /// decomisioning Pyramid 
    /// </summary>
    public bool Decomisioning = false;
    /// <summary>
    /// decomisioning Pyramid timelapse
    /// </summary>
    public bool AnimateDecommissioning = false;
    /// <summary>
    /// decomisioning Pyramid timelapse
    /// </summary>
    public float DecommissioningTimeLapse = 0.1f;
    /// <summary>
    /// % decomisioning Pyramid step
    /// </summary>
    public float DecommissioningStep = 0.05f; 
    /// <summary>
    /// Ommited Pyramid blocks 
    /// </summary>
    public List<GameObject> DetectedDeletedBlocks;
    /// <summary>
    /// Straight Ramp Face starting position
    /// </summary>
    public RampPositionFace StraightRampFace = RampPositionFace.NorthFace;

    // --- Logging and Export Options ---
    /// <summary>
    /// The filename for the CSV iteration output log.
    /// </summary>
    public string csvitername = "pyramid_iter.csv";
    /// <summary>
    /// The filename for the CSV row output log.
    /// </summary>
    public string csvrowname = "pyramid_row.csv";
    /// <summary>
    /// The filename for the CSV headway output log.
    /// </summary>
    public string csvheadway = "pyramid_headway.csv";
    /// <summary>
    /// The filename for the text output log.
    /// </summary>
    public string txtname = "pyramid.txt";
    /// <summary>
    /// Toggles logging info for each level.
    /// </summary>
    public bool showInfoLevel = true;
    /// <summary>
    /// Toggles logging total info for each level.
    /// </summary>
    public bool showInfoLevelTotal = true;
    /// <summary>
    /// Toggles logging decrement info between levels.
    /// </summary>
    public bool showInfoLevelDec = true;
    /// <summary>
    /// Toggles logging info for each row.
    /// </summary>
    public bool showInfoRow = true;
    /// <summary>
    /// If true, exports the generated pyramid as an OBJ file on start.
    /// </summary>
    public bool exportPyramidObj = false;
    /// <summary>
    /// If true, combines meshes during OBJ export for better performance.
    /// </summary>
    public bool exportCombineMeshes = false;
    /// <summary>
    /// The name of the subfolder for OBJ exports.
    /// </summary>
    public string exportSubFolder = "PyramidModels"; // The name of the subfolder for exporting
    /// <summary>
    /// The name of the output OBJ file (without extension).
    /// </summary>
    public string outputFileName = "MyExportedPyramid"; // Name of the output OBJ file (without the extension)


    // --- Physics and Performance Options ---
    /// <summary>
    /// Sets the generated GameObjects as static for performance optimization.
    /// </summary>
    public bool isStatic = true;
    /// <summary>
    /// Adds a Rigidbody component to blocks for physics simulation.
    /// </summary>
    public bool isRigidBody = false;
    /// <summary>
    /// Adds FixedJoint components between blocks if they have Rigidbodies.
    /// </summary>
    public bool useFixedJoints = false;
    /// <summary>
    /// The physics layer for the blocks.
    /// </summary>
    public LayerMask blockLayer;

    // --- Advanced Method Parameters ---
    /// <summary>
    /// Number of granite blocks of type 1 < 50 tons.
    /// </summary>
    public int numOfGraniteRock1 = 6;
    /// <summary>
    /// Number of granite blocks of type 2 > 50 tons.
    /// </summary>
    public int numOfGraniteRock2 = 45;
    /// <summary>
    /// Number of granite blocks of type 2 > 50 tons. Limestone
    /// </summary>
    public int numOfGraniteRock3 = 24;
    /// <summary>
    /// Minimum height (in meters) to start placing granite blocks.
    /// </summary>
    public int minHeightGraniteRock = 43;
    /// <summary>
    /// Maximum height to place granite blocks.
    /// </summary>
    public int maxHeightGraniteRock = 60;
    /// <summary>
    /// Maximum height to place limetone blocks.
    /// </summary>
    public int maxHeightGraniteRock2 = 68;
    /// <summary>
    /// Minimum base size to use a 2-ramp system.
    /// </summary>
    public int minBaseSize2Ramps = 20;
    /// <summary>
    /// Minimum base size to use a 4-ramp system.
    /// </summary>
    public int minBaseSize4Ramps = 40;
    /// <summary>
    /// Minimum base size to use an 8-ramp system.
    /// </summary>
    public int minBaseSize8Ramps = 80;
    /// <summary>
    /// Minimum base size to use a 16-ramp system.
    /// </summary>
    public int minBaseSize16Ramps = 160;
    /// <summary>
    /// average headway
    /// </summary>
    public float AverageHeadway = 4.0f;
    /// <summary>
    /// min headway
    /// </summary>
    public float MinHeadway = 2.0f;
    /// <summary>
    /// max headway
    /// </summary>
    public float MaxHeadway = 13.0f;
    /// <summary>
    /// Working Year Minutes
    /// </summary>
    public float WorkingYearMinutes = 187200f;
    /// <summary>
    /// Headway Type 
    /// </summary>
    public PyramidHeadwayType PyramidHeadwayType = PyramidHeadwayType.Single_Ramp;
    /// <summary>
    /// Sequenced 
    /// </summary>
    public Boolean Sequenced = false;
    /// <summary>
    /// Show GUI 
    /// </summary>
    public Boolean ShowGUI = true;
    /// <summary>
    /// Notch N1
    /// </summary>
    public GameObject notchN1;
    /// <summary>
    /// Notch N2
    /// </summary>
    public GameObject notchN2;
    /// <summary>
    /// Notch N3
    /// </summary>
    public GameObject notchN3;
    /// <summary>
    /// Cavity N1
    /// </summary>
    public GameObject CavityC1;
    /// <summary>
    /// Cavity N2
    /// </summary>
    public GameObject CavityC2;
    /// <summary>
    /// Show Notches and Cavities 
    /// </summary>
    public Boolean ShowKhufuNotchs = false;
    /// <summary>
    /// display game object on finish
    /// </summary>
    public GameObject DisplayGameObjectOnFinish;
    /// <summary>
    /// Camera position face
    /// </summary>
    public CameraPositionFace cameraPositionFace = CameraPositionFace.NorthFace;
    /// <summary>
    /// Orbit camera on finish 
    /// </summary>
    public bool OrbitCameraOnFinish = false;
    /// <summary>
    /// Speed of orbit camera on finish
    /// </summary>
    public float OrbitSpeed = 10.0f;
    /// <summary>
    /// Camera orbit distance factor
    /// </summary>
    public float camOrbitDistanceFactor = 1.5f;
    /// <summary>
    /// Camera orbit heigh offset factor
    /// </summary>
    public float camOrbitHeightOffsetFactor = 0.6f;
    /// <summary>
    /// Progress bar UI
    /// </summary>
    public ProgressBarUI progressBar;
    /// <summary>
    /// Point in the middle of the ramp in the lastest iteration
    /// </summary>
    public Vector3 lastRampMidPoint = Vector3.zero;
    /// <summary>
    /// use blocks course thickness
    /// </summary>
    public bool useKhufuCourseHeights = false;
    /// <summary>
    /// Granite Megaliths project
    /// </summary>
    // --- NEW GRANITE PROJECT VARIABLES ---
    [Header("Granite Megalith Project")]
    [Tooltip("Toggle to enable logging for the megalith calculations.")]
    public bool showInfoGranite = false;
    [Tooltip("Number of 10-ton granite megalith blocks.")]
    public int numberOfGranite10tons = 6;
    [Tooltip("Number of 40-ton limestone megalith blocks.")]
    public int numberOfLimestone40tons = 24;
    [Tooltip("Number of 50-ton granite megalith blocks.")]
    public int numberOfGranite50tons = 0;
    [Tooltip("Number of 60-ton granite megalith blocks.")]
    public int numberOfGranite60tons = 0;
    [Tooltip("Number of 70-ton granite megalith blocks.")]
    public int numberOfGranite70tons = 45;
    [Tooltip("Number of 80-ton granite megalith blocks.")]
    public int numberOfGranite80tons = 0;
    [Tooltip("The course (row) number where the King's Chamber blocks start.")]
    public int startCourseKingsChamber = 60;
    [Tooltip("The course (row) number where the King's Chamber blocks end.")]
    public int endCourseKingsChamber = 85;
    [Tooltip("The course (row) number where the King's Chamber Gablete blocks end.")]
    public int endCourseGableteKingsChamber = 96;
    [Tooltip("Average force per puller in Newtons.")]
    public float forcePerPullerNewtons = 250.0f;
    [Tooltip("Angle of the mezzanine ramps in degrees.")]
    public float mezzanineRampAngleDegrees = 3.0f;
    [Tooltip("Friction coeficient of the mezzanine ramps and ground.")]
    public float mezzanineFrictionCoef = 0.2f;
    [Tooltip("Horizontal transfer distance on the terrace in meters.")]
    public float horizontalTransferDistanceMeters = 10.0f;
    [Tooltip("Setup time per course in hours.")]
    public float setupTimePerCourseHours = 2.0f;
    [Tooltip("Number of setup groups working in parallel.")]
    public int setupTimePerCourseGroups = 6;
    [Tooltip("The average speed of the pullers ramp in m/s.")]
    public float pullingSpeedRampMetersPerSecond = 0.15f;
    [Tooltip("The average speed of the pullers terrace in m/s.")]
    public float pullingSpeedTerraceMetersPerSecond = 0.20f;
    [Tooltip("Use a capstan for force multiplication.")]
    public bool useCapstan = true;
    [Tooltip("Friction coefficient for capstan calculations (μ).")]
    public float frictionCoefCapstan = 0.3f;
    [Tooltip("Wrap angle of the rope on the capstan in radians (e.g., PI for 180 degrees).")]
    public float capstanWrapAngleRadians = Mathf.PI;
    [Tooltip("Filename for the granite calculation CSV log.")]
    public string csvGraniteName = "pyramid_granite.csv";
    public float totalGraniteMoveTimeWorkingYears = 0f;
    // --- END NEW ---    

    // Static, read-only list of the heights of the Cheops courses.
    private static readonly List<float> khufuCourseHeights = new List<float>
    {
        1.5f, 1.24f, 1.2f, 1.02f, 0.99f, 0.9f, 1.0f, 0.97f, 0.93f, 0.915f, 0.865f, 0.76f, 0.76f, 0.75f, 0.75f, 0.735f, 0.75f, 0.83f, 0.95f, 0.62f, 0.58f, 0.87f, 0.89f, 0.83f, 0.8f, 0.74f, 0.78f, 0.69f, 0.65f, 0.64f, 0.73f, 0.72f, 0.54f, 0.66f, 1.27f, 1.0f, 0.97f, 0.95f, 0.84f, 0.84f, 0.83f, 0.72f, 0.83f, 1.06f, 0.97f, 0.73f, 0.9f, 0.905f, 0.86f, 0.7f, 0.725f, 0.61f, 0.68f, 0.63f, 0.69f, 0.55f, 0.62f, 0.685f, 0.75f, 0.7f, 0.675f, 0.635f, 0.655f, 0.675f, 0.66f, 0.605f, 0.86f, 0.835f, 0.78f, 0.64f, 0.71f, 0.675f, 0.66f, 0.8f, 0.76f, 0.62f, 0.64f, 0.6f, 0.595f, 0.625f, 0.6f, 0.6f, 0.6f, 0.67f, 0.575f, 0.67f, 0.66f, 0.58f, 0.61f, 0.97f, 0.905f, 0.835f, 0.775f, 0.68f, 0.63f, 0.605f, 0.61f, 1.0f, 0.995f, 0.905f, 0.85f, 0.74f, 0.76f, 0.68f, 0.675f, 0.64f, 0.635f, 0.755f, 0.68f, 0.59f, 0.605f, 0.595f, 0.6f, 0.58f, 0.575f, 0.675f, 0.58f, 0.905f, 0.83f, 0.75f, 0.745f, 0.67f, 0.66f, 0.63f, 0.58f, 0.605f, 0.595f, 0.59f, 0.7f, 0.65f, 0.605f, 0.565f, 0.555f, 0.545f, 0.61f, 0.58f, 0.68f, 0.65f, 0.57f, 0.56f, 0.56f, 0.565f, 0.74f, 0.685f, 0.6f, 0.59f, 0.56f, 0.56f, 0.7f, 0.635f, 0.595f, 0.59f, 0.55f, 0.55f, 0.54f, 0.545f, 0.545f, 0.545f, 0.545f, 0.545f, 0.6f, 0.59f, 0.645f, 0.56f, 0.54f, 0.54f, 0.545f, 0.525f, 0.535f, 0.54f, 0.53f, 0.52f, 0.495f, 0.53f, 0.53f, 0.525f, 0.53f, 0.53f, 0.675f, 0.635f, 0.6f, 0.58f, 0.56f, 0.545f, 0.53f, 0.52f, 0.525f, 0.52f, 0.525f, 0.54f, 0.515f, 0.52f, 0.52f, 0.52f, 0.585f, 0.605f, 0.565f, 0.55f, 0.575f, 0.565f, 0.572f, 0.544f, 0.565f,0.565f
    };

    private float pyramid_inclination_tg = 0;
    private float ramp_inclination_tg;   
    private float ramp_inclination_atg;
    private float ramp_total_length=0;
    private float g = 9.80665f;
    private string dir;   
    private string textPath;
    private float setbackWide = 0.0f;

    private float x;
    private float z;

    private StreamWriter writer;
    private StreamWriter csviterwriter;
    private StreamWriter csvrowwriter;
    private StreamWriter csvheadwaywriter;
    private StreamWriter csvgranitewriter;

    private int indexblock = 0;
    private int lastLevel = 0;
    private int lastLevelBlocks = 0;
    private int numberOfBlocksFinish = 0;

    private List<GameObject> blocksMidle;
    private List<GameObject> blocksMidle2;   
    
    private GameObject cube1,cube2,cube3,cube4;
    private GameObject cubefloor1, cubefloor2, cubefloor3, cubefloor4;
    private GameObject cubewall1, cubewall2, cubewall3, cubewall4;

    private bool isGenerating = false;

    // Static instance of the comparer to avoid creating a new one for each insertion.
    private static readonly GameObjectYComparer yComparer = new GameObjectYComparer();

    /// <summary>
    /// List of the turning points (iterations) of the IER ramp.
    /// </summary>
    private List<TurningPoint> _ierTurningNodes;
    /// <summary>
    /// Safety limit to prevent infinite loops if the slope is 0 or at the apex
    /// </summary>
    private const int MAX_TURNING_ITERATIONS = 500;

    // TotalPullers is the team size needed *without* a capstan
    private int totalPullers10t;
    private int totalPullers40t;
    private int totalPullers50t;
    private int totalPullers60t;
    private int totalPullers70t;
    private int totalPullers80t;

    // CapstanOperators is the smaller team applying the *reduced* force
    private int capstanOperators10t;
    private int capstanOperators40t;
    private int capstanOperators50t;
    private int capstanOperators60t;
    private int capstanOperators70t;
    private int capstanOperators80t;

    // This runs in the editor whenever a value is changed in the Inspector.
    private void OnValidate()
    {
        // If the selected option is not 'Default', update the dimensions.
        if (selectedPyramid != PyramidType.Default)
        {
            switch (selectedPyramid)
            {
                case PyramidType.Khufu:
                    BaseSize = 230.36f;
                    Height = 146.50f;
                    PyramidInclination = 51.85f;
                    blockheight = 0.71f;
                    break;

                case PyramidType.Khafre:
                    BaseSize = 215.25f;
                    Height = 143.50f;
                    PyramidInclination = 53.17f;
                    blockheight = 0.70f;
                    break;

                case PyramidType.Menkaure:
                    BaseSize = 108.5f;
                    Height = 65.5f;
                    PyramidInclination = 51.34f;
                    blockheight = 0.65f;
                    break;

                case PyramidType.Bent_bottom:
                    BaseSize = 188.0f;
                    Height = 47f; // Total height of the pyramid.
                    PyramidInclination = 54.5f;
                    blockheight = 0.66f; // An average value.
                    break;

                case PyramidType.Bent_top:
                    BaseSize = 124.5f;
                    Height = 58f; // Total height of the pyramid.
                    PyramidInclination = 43.3f;
                    blockheight = 0.66f; // An average value.
                    break;

                case PyramidType.Red:
                    BaseSize = 220.0f;
                    Height = 104.4f;
                    PyramidInclination = 43.3f;
                    blockheight = 0.6f;
                    break;
            }
        }
    }

    void Awake()
    {
        // Initialize the list to prevent errors
        DetectedDeletedBlocks = new List<GameObject>();
    }

    /// <summary>
    /// Public method to safely start the pyramid generation process.
    /// </summary>
    public void StartGeneration()
    {
        if (!isGenerating)
        {
            StartCoroutine(CreatePyramidCoroutine());
        }
        else
        {
            Debug.LogWarning("Generation is already in progress.");
        }
    }

    private void OpenLogFiles()
    {
        try
        {
            // Clear previous pyramid if any
            dir = Application.dataPath + "/../";
            textPath = Path.Combine(dir, txtname);
            Debug.Log("File : " + textPath);
            writer = new StreamWriter(textPath, false);
            if (showInfoLevel || showInfoLevelTotal || showInfoLevelDec)
            {
                textPath = Path.Combine(dir, csvitername);
                Debug.Log("File : " + textPath);
                csviterwriter = new StreamWriter(textPath, false);
                csviterwriter.WriteLine("Course;Height;blocks;Separation;New base size;Length;Ramp inclination;Start height;% total height;Ramp length (m)");
            }
            else
                csviterwriter = null;
            if (showInfoRow)
            {
                textPath = Path.Combine(dir, csvrowname);
                Debug.Log("File : " + textPath);
                csvrowwriter = new StreamWriter(textPath, false);
                csvrowwriter.WriteLine("Row;blocks;ramp inclination;Ramp length (m);Ramp length total (m);ramp internal inclination iter;ramp height inclination iter;Ramp length (m) iter;distance blocks (Km);distance blocks Ramp (Km);distance blocks Horiz (Km);Sum force blocks (MJ);Sum Vert. force blocks (MJ);Sum Horiz. force blocks (MJ);Vert. force blocks row (MJ);Horiz. force blocks row (MJ);Total force blocks row (MJ);% Decrement blocks;% increase Distance;% increase Force");

                textPath = Path.Combine(dir, csvheadway);
                Debug.Log("File : " + textPath);
                csvheadwaywriter = new StreamWriter(textPath, false);
                csvheadwaywriter.WriteLine("Row;blocks;up ramps;blocks per ramp;fixed headway(min);adaptative headway(min);total time(min);adaptative total time(min);total time(working years);adaptativive total time(working years)");
            }
            else
            {
                csvrowwriter = null;
                csvheadwaywriter = null;
            }
            if (showInfoGranite)
            {
                textPath = Path.Combine(dir, csvGraniteName);
                Debug.Log("File : " + textPath);
                csvgranitewriter = new StreamWriter(textPath, false);
                csvgranitewriter.WriteLine("row;Percentage;Curse heigh(m);ramp slope(degrees);ramp distance(m);horiz distance(m);total blocks;total displacement time (h);setup time (h);total time (h);total (working years);pullers x blocks;total Work (MJ)");
            }
            else
            {
                csvgranitewriter = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to open log files: {e.Message}");
        }
    }

    private void CloseLogFiles()
    {
        // This method is now safer. It checks if a writer is null before trying to close it.
        try
        {
            writer.Flush();
            writer.Close();
            if (showInfoLevel)
            {
                csviterwriter.Flush();
                csviterwriter.Close();
            }
            if (showInfoRow)
            {
                csvrowwriter.Flush();
                csvrowwriter.Close();
                csvheadwaywriter.Flush();
                csvheadwaywriter.Close();
            }
            if (showInfoGranite)
            {
                csvgranitewriter.Flush();
                csvgranitewriter.Close();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to close log files: {e.Message}");
        }
    }

    /// <summary>
    /// The main coroutine that handles the entire generation process without freezing the application.
    /// </summary>
    private IEnumerator CreatePyramidCoroutine()
    {
        isGenerating = true;
        if (progressBar) progressBar.Show("Initializing...");
        yield return null; // Wait a frame for UI to update

        OpenLogFiles();

        // Notchs
        if (notchN1)
        {
            notchN1.SetActive(ShowKhufuNotchs);
        }
        if (notchN2)
        {
            notchN2.SetActive(ShowKhufuNotchs);
        }
        if (notchN3)
        {
            notchN3.SetActive(ShowKhufuNotchs);
        }
        if (CavityC1)
        {
            CavityC1.SetActive(ShowKhufuNotchs);
        }
        if (CavityC2)
        {
            CavityC2.SetActive(ShowKhufuNotchs);
        }

        // Pyramid interior
        if (PyramidInterior)
        {
            PyramidInterior.SetActive(DrawPyramidInterior);
            if (DrawPyramidInterior)
                SetInteriorVisibility(!DrawPyramidInteriorTransparent);
        }

        path_length = 0;

        // hide Pyramide during generation
        if (objParent) objParent.SetActive(false);

        // --- 0. DYNAMICALLY CALCULATE TURNING POINTS ---
        if (rampMethod == RampMethodType.Integrated && (Method2Ramp || Method4Ramp))
        {
            Debug.Log("-------------------------------------------------");
            Debug.Log(" Calculating IER turning points based on ramp inclination...");

            // This REPLACES the previous static list
            _ierTurningNodes = CalculateIERTurningPoints();

            Debug.Log($" IER turning points dynamically calculated for {RampInclination}°:");
            foreach (var turn in _ierTurningNodes)
            {
                Debug.Log($"==> {turn.ToString()}");
            }
            Debug.Log("-------------------------------------------------");

            // --- 1. Calculate the location of the block terrace ---
            int blocksInTerrace = (int) (minBaseSize2Ramps * minBaseSize2Ramps / (blockwide * blockwide));
            float terraceHeight = FindHeightForTerraceBlockCount(blocksInTerrace);
            int terraceCourse = GetCourseAtHeight(terraceHeight);

            Debug.Log($" A terrace with ~{blocksInTerrace} blocks is located at:");
            Debug.Log($"==> Height: {terraceHeight:F2} m");
            Debug.Log($"==> Course: {terraceCourse}");
            Debug.Log("-------------------------------------------------");

            // --- 2. Find the nearest IER turn to that location ---
            TurningPoint nearest = FindNearestIERTurn(terraceHeight);

            if (nearest.Iteration > 0) // Check if a turn was found
            {
                Debug.Log($" The nearest IER turn (iteration) to {terraceHeight:F2} m is:");
                Debug.Log($"==> {nearest.ToString()}");
            }
            else
            {
                Debug.LogWarning($" No IER turning points were found for height {terraceHeight:F2} m.");
            }
            Debug.Log("-------------------------------------------------");

            minBaseSize2Ramps = (int)(Mathf.Sqrt(GetBlockCountForCourse(nearest.Course)) * blockwide);
            Debug.Log($" Adjusted minimum base size for 2-ramps method to: {minBaseSize2Ramps}");
            Debug.Log("-------------------------------------------------");

            // 4-ramps method
            if (Method4Ramp)
            {
                blocksInTerrace = (int)(minBaseSize4Ramps * minBaseSize4Ramps / (blockwide * blockwide));
                terraceHeight = FindHeightForTerraceBlockCount(blocksInTerrace);
                terraceCourse = GetCourseAtHeight(terraceHeight);

                Debug.Log($" A terrace with ~{blocksInTerrace} blocks is located at:");
                Debug.Log($"==> Height: {terraceHeight:F2} m");
                Debug.Log($"==> Course: {terraceCourse}");
                Debug.Log("-------------------------------------------------");

                // --- 2. Find the nearest IER turn to that location ---
                nearest = FindNearestIERTurn(terraceHeight);

                if (nearest.Iteration > 0) // Check if a turn was found
                {
                    Debug.Log($" The nearest IER turn (iteration) to {terraceHeight:F2} m is:");
                    Debug.Log($"==> {nearest.ToString()}");
                }
                else
                {
                    Debug.LogWarning($" No IER turning points were found for height {terraceHeight:F2} m.");
                }
                Debug.Log("-------------------------------------------------");

                minBaseSize4Ramps = (int)(Mathf.Sqrt(GetBlockCountForCourse(nearest.Course)) * blockwide);
                Debug.Log($" Adjusted minimum base size for 4-ramps method to: {minBaseSize4Ramps}");
                Debug.Log("-------------------------------------------------");
            }
        }

        // start generation
        yield return StartCoroutine(compute_size());

        // Show Pyramide during generation
        if (objParent) objParent.SetActive(true);

        CloseLogFiles();

        // half pyramid
        if (halfPyramid)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "HalfPyramidCut";
            cube.transform.position = objParent.transform.position + new Vector3(BaseSize / 2, Height / 2, 0);
            cube.transform.localScale = new Vector3(BaseSize, Height, BaseSize);
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;            
            cube.GetComponent<MeshRenderer>().enabled = false;
            cube.GetComponent<BoxCollider>().isTrigger = true;
        }

        if (progressBar) progressBar.Show("Finalizing...");
        yield return null;

        // instanciate palms and dromer
        if (!exportPyramidObj)
        {
            for (int i = 0; i < 10; i++)
                if (Palm)
                    Instantiate(Palm, objParent.transform.position + new Vector3(BaseSize / 2 + 10.0f + UnityEngine.Random.Range(0, 30), 0, BaseSize / 2 - 10.0f + UnityEngine.Random.Range(0, 30)), Quaternion.identity);
            for (int i = 0; i < 10; i++)
                if (Dromader)
                    Instantiate(Dromader, objParent.transform.position + new Vector3(BaseSize / 2 + 10.0f, 0, -BaseSize / 2 - 10.0f + i * 4), Quaternion.identity);
            if (showEiffel)
                Instantiate(Eiffel, objParent.transform.position + new Vector3(-BaseSize / 2 - 75.0f, 163, -BaseSize / 2 - 75.0f), Quaternion.identity);
            if (showEiffel)
                Instantiate(Eiffel, objParent.transform.position + new Vector3(-BaseSize / 2 - 75.0f, 163, -BaseSize / 2 - 75.0f), Quaternion.identity);
            if (showMan)
            {
                Man.transform.position = objParent.transform.position + new Vector3(BaseSize / 2, 0, BaseSize / 2);
                Man.SetActive(true);
            }
            else
            if (Man)
                Man.SetActive(false);
        }

        if (exportPyramidObj)
            StartCoroutine(ExportObj());

        blocksMidle = new List<GameObject>();
        blocksMidle2 = new List<GameObject>();

        if (progressBar) progressBar.Hide();

        if (cameraPositionFace == CameraPositionFace.InfrontRamp && DrawUntilRow)
        {
            Debug.Log("Ramp Target Position : " + lastRampMidPoint.ToString());
            Vector3 lastRampMidPointCam = GetTargetPositionFromCenter(lastRampMidPoint, BaseSize);
            Debug.Log("Camera Ramp Target Position : " + lastRampMidPointCam.ToString());
            cam.transform.localPosition = lastRampMidPointCam;
            cam.transform.LookAt(lastRampMidPoint);
        }

        // After generation, check if we should run the decommissioning animation.
        if (AnimateDecommissioning && DetectedDeletedBlocks.Count > 0)
        {
            yield return StartCoroutine(DecommissionCoroutine());
        }

        isGenerating = false;
        Debug.Log("Pyramid generation complete.");

        if (DisplayGameObjectOnFinish)
        {
            DisplayGameObjectOnFinish.SetActive(true);
        }

        if (OrbitCameraOnFinish && cam != null)
        {
            yield return StartCoroutine(OrbitCameraAroundPyramid());
        }        
    }

    // Start is called before the first frame update
    void Start()
    {
        if (progressBar) progressBar.Hide();

        string baseExportPath = Application.persistentDataPath;

        // Combines the base path with your subfolder name for the
        string fullExportPath = Path.Combine(baseExportPath, exportSubFolder);

        pyramid_inclination_tg = getTanFromDegrees(PyramidInclination);
        ramp_inclination_tg = getTanFromDegrees(RampInclination);
        ramp_inclination_atg = Mathf.Atan(ramp_inclination_tg);
        total_height = 0;
        int rh = Mathf.CeilToInt(Height / blockheight);  // adjust to block height
        Height = rh * blockheight;
        float rampInclinationRadians = RampInclination * Mathf.Deg2Rad;
        ramp_total_length = Height / Mathf.Sin(rampInclinationRadians);

        // base on base size
        GameObject base_cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        base_cube.name = "Base";
        base_cube.isStatic = true;
        base_cube.transform.position = objParent.transform.position + new Vector3(0, -0.5f, 0);
        base_cube.transform.parent = objParent.transform;
        base_cube.transform.localScale = new Vector3(BaseSize, 1.0f, BaseSize);
        base_cube.GetComponent<MeshRenderer>().enabled = false;

        // look
        if (cam)
        {
            RampTargetMetrics RampTarget;
            RampTarget.IsValid = false;
            if (cameraPositionFace == CameraPositionFace.NorthFace)               
                cam.transform.localPosition = new Vector3(-BaseSize * 4 / 5, Height * 3 / 4, -BaseSize * 4 / 5);
            if (cameraPositionFace == CameraPositionFace.EastFace)
                cam.transform.localPosition = new Vector3(BaseSize * 4 / 5, Height * 3 / 4, -BaseSize * 4 / 5);
            if (cameraPositionFace == CameraPositionFace.SouthFace)
                cam.transform.localPosition = new Vector3(BaseSize * 4 / 5, Height * 3 / 4, BaseSize * 4 / 5);
            if (cameraPositionFace == CameraPositionFace.WestFace)
                cam.transform.localPosition = new Vector3(-BaseSize * 4 / 5, Height * 3 / 4, BaseSize * 4 / 5);
            if (cameraPositionFace == CameraPositionFace.InfrontRamp)
            {
                if (DrawUntilRow)
                {
                    RampTarget = CalculateRampTargetMetrics(DrawRow);
                    Debug.Log("Ramp Target Position : " + RampTarget.Position.ToString() + ", Valid : " + RampTarget.IsValid);
                    cam.transform.localPosition = new Vector3(RampTarget.Position.x * 2, RampTarget.Position.y * 2, RampTarget.Position.z * 2);
                    if (RampTarget.IsValid)
                        cam.transform.LookAt(RampTarget.Position);
                    else
                        cam.transform.LookAt(new Vector3(0, 0, 0));
                }    
                else
                    cam.transform.localPosition = new Vector3(0, Height * 3 / 4, -(BaseSize / 2 + (Height) / ramp_inclination_tg + 20));
            }
            if (cameraPositionFace == CameraPositionFace.Zenit)
                cam.transform.localPosition = new Vector3(0, Height * 1.5f, 0);
            //cam.transform.localPosition = new Vector3(BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, -BaseSize);
            if (cameraPositionFace != CameraPositionFace.InfrontRamp || !DrawUntilRow)
                cam.transform.LookAt(new Vector3(0, Height / 12, 0));
        }

        // generate pyramid
        StartGeneration();
    }

    void Update()
    {
        /*if ((indexblock < 132000) && (numberOfBlocksFinish == 0))
        {
            numberOfBlocks = lastLevelBlocks;
            draw_one_size_level(lastLevel, BaseSize, PathWide, PathSeparation, 0, indexblock);
            indexblock++;
        }*/        
    }

    // create Pyramid
    public void CreatePyramid()
    {
        // Notchs
        if (notchN1) notchN1.SetActive(false);
        if (notchN2) notchN2.SetActive(false);
        if (notchN3) notchN3.SetActive(false);
        if (CavityC1) CavityC1.SetActive(false);
        if (CavityC2) CavityC2.SetActive(false);
        // Pyramid interior
        if (PyramidInterior) PyramidInterior.SetActive(false);        

        StartGeneration();
    }


    private float calcAngle(float opposite, float adjacent)
    {
        return Mathf.Atan(opposite / adjacent);
    }

    private float degrees_to_radians(float degrees)
    {
        float pi = Mathf.PI;
        return degrees * (pi / 180);
    }

    private float radians_to_degrees(float radians)
    {
        float pi = Mathf.PI;
        return radians * (180 / pi);
    }

    private float getTanFromDegrees(float degrees)
    {
        return Mathf.Tan(degrees * Mathf.PI / 180);
    }

    public IEnumerator compute_size()
    {
        if (showInfoLevel || showInfoLevelDec || showInfoLevelTotal || showInfoRow)
        {
            Debug.Log("Start with : Base size (m) = " + BaseSize + ", Height (m) = " + Height);
            writer.WriteLine("Start with : Base size (m) = " + BaseSize + ", Height (m) = " + Height);
            Debug.Log("Path wide (m) = " + PathWide + ", Separation (m) = " + PathSeparation);
            writer.WriteLine("Path wide (m) = " + PathWide + ", Separation (m) = " + PathSeparation);
            Debug.Log("Pyramid inclination (degrees) = " + PyramidInclination + ", Ramp inclination (degrees) = " + RampInclination);
            writer.WriteLine("Pyramid inclination (degrees) = " + PyramidInclination + ", Ramp inclination (degrees) = " + RampInclination);
            Debug.Log("Pyramid inclination tangent : " + pyramid_inclination_tg + ", Ramp inclination tangent : " + ramp_inclination_tg);
            writer.WriteLine("Pyramid inclination tangent : " + pyramid_inclination_tg + ", Ramp inclination tangent : " + ramp_inclination_tg);
        }

        if (progressBar && !Sequenced)
        {
            if (DrawUntilRow || DrawOnlyRow)
                progressBar.SetMaxProgress(DrawRow);
            else
                progressBar.SetMaxProgress(Height/blockheight);
            progressBar.SetProgress(0, "Starting...");
            yield return null;
        }

        path_length = 0;

        yield return StartCoroutine(compute_size_level(0, BaseSize, PathWide, PathSeparation, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        if (DrawUntilRow && DrawRow > 0)
            CalculateVolumeUntilRow(DrawRow);
        else 
            CalculateTotalVolume();
        if (rampMethod == RampMethodType.Straight && DrawUntilRow)
        {
            DrawStraightRamp();
        }
        if (rampMethod == RampMethodType.Internal)
        {
            // 40 meters frontal ramp
            if (DrawUntilRow && DrawRow< internalRampStraightRampHigh / blockheight)
                DrawStraightRamp();
            SetPyramidTransparency(pyramidTransparency);
        }

        if (showInfoLevel || showInfoLevelDec || showInfoLevelTotal || showInfoRow)
        {
            Debug.Log("Total length : " + path_length + ", Total block distance : " + totalLength + ", Total block force : " + totalForce + ", Total block force ramp : " + totalForceRamp + ", % force ramp : " + totalForceRamp * 100 / totalForce);
            writer.WriteLine("Total length : " + path_length + ", Total block distance : " + totalLength + ", Total block force : " + totalForce + ", Total block force ramp : " + totalForceRamp + ", % force ramp : " + totalForceRamp * 100 / totalForce);
        }
    }

    private IEnumerator compute_size_level(int level, float base_size, float path_wide, float separation, float height, 
            float old_length, float beforeBlocks, float beforeDistance, float beforeForce, 
            float force_old_length, float force_old_vert, float force_old_horiz, 
            int row, float old_length_spiral)
    {
        if (DrawUntilRow && row > DrawRow)
        {
            yield break;            
        }

        if (DrawUntilTurn && level >= DrawTurn)
        {
            yield break;
        }

        if (height > Height)
        {
            Debug.Log("Good solution! Total height: " + total_height);
            writer.WriteLine("Total height: " + total_height);
            yield break;
        }        

        //float h = base_size * ramp_inclination_tg;  // height
        float h = base_size * ramp_inclination_tg * pyramid_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        // divide by height of block
        int ch = Mathf.RoundToInt(h / blockheight);
        h = ch * blockheight; // adjust
        //float sep = h / pyramid_inclination_tg; // separation
        float sep = base_size * ramp_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);

        float currentCourseHeight = blockheight;
        float setbackKhufuCourseHeights = 0.0f;
        // uses thickness from Khufu courses
        if (selectedPyramid==PyramidType.Khufu && useKhufuCourseHeights)
        {
            sep = 0;
            // Now, calculate the *actual* total height of this level by summing the real course heights.
            float h1 = 0;
            float h_course = 0;
            int last_i = 0;
            for (int i = 0; i < 10000 ; i++)
            {
                last_i = i;
                h_course = GetBlockHeightForRow(row + i); // default height
                h1 += h_course;
                setbackWide = h_course / Mathf.Tan(degrees_to_radians(PyramidInclination));
                setbackKhufuCourseHeights += setbackWide;
                if (DrawUntilRow && row > DrawRow || h1>h)
                {
                    if (h1 > h)
                    {
                        ch = last_i+1;
                        //h_course = GetBlockHeightForRow(row + last_i);
                        //h1 -= h_course;
                        //setbackWide = h_course / Mathf.Tan(degrees_to_radians(PyramidInclination));
                        //setbackKhufuCourseHeights -= setbackWide;
                    }
                    break;
                }
            }            
            h = h1;
            sep = setbackKhufuCourseHeights;
        }

        total_height += h;
        float heightGranite = 0;
        setbackWide = currentCourseHeight / Mathf.Tan(degrees_to_radians(PyramidInclination));

        GameObject lastCubeDrawn = null;
        int numberOfBlocksX = 0;
        int numberOfBlocksZ = 0;
        int lastNumberOfBlockDrawnX = -1;
        int lastNumberOfBlockDrawnZ = -1;

        GameObject iter_gameObject = new GameObject();
        iter_gameObject.name = "Iter_" + level;
        iter_gameObject.transform.parent = objParent.transform;
        iter_gameObject.isStatic = isStatic;
        if (Sequenced)
            iter_gameObject = objParent;

        if (h < blockheight/2)
        {
            if (height + h > Height)
                Debug.Log("Good solution! Total height: " + total_height);
            else
                Debug.Log("Bad solution! Total height: " + total_height);
            if (showInfoLevel)
                writer.WriteLine("Total height: " + total_height);
            yield break;
        }

        float new_base_size = base_size - 2 * path_wide - 2 * separation - 2 * sep;  // new base size

        if (new_base_size < h / 2)
        {
            if (height + h > Height)
                Debug.Log("Good solution! Total height: " + total_height);
            else
                Debug.Log("Bad solution! Total height: " + total_height);
            if (showInfoLevel)
                writer.WriteLine("Total height: " + total_height);
            yield break;
        }

        float bs2 = base_size / 2;
        Vector3 v0 = new Vector3(bs2, 0, bs2);
        Vector3 v1 = new Vector3(bs2 - sep, h, -(bs2 - sep));
        //float length = Mathf.Sqrt(new_base_size * new_base_size + h * h);
        float length = Vector3.Distance(v0, v1);

        // spiral / internal ramp calculations
        float length_spiral = 0;
        float base_size_spiral = base_size + (blockwide + 1) * spiralRampSeparation;
        if (rampMethod == RampMethodType.Internal)
            base_size_spiral = base_size - (blockwide + 1) * spiralRampSeparation;
        float bs2_spiral = base_size_spiral / 2;
        float sep_spiral = base_size_spiral * ramp_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        float h_spiral = base_size_spiral * ramp_inclination_tg * pyramid_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        int ch_spiral = Mathf.RoundToInt(h / blockheight);
        h_spiral = ch_spiral * blockheight; // adjust

        if (showManFinalRamp && level==0)
        { 
             if (MethodInsideRamp)
                Man.transform.position = objParent.transform.position + new Vector3(bs2 - sep - 2, h + 0.5f, -(bs2 - sep - 2));
            else
                 Man.transform.position = objParent.transform.position + v1;
             Man.SetActive(true);

            if (cam)
            {
                cam.transform.localPosition = new Vector3(bs2 - sep + 5, h + 1.8f, -(bs2 - sep + 5));
                cam.transform.LookAt(v1);
            }
        }

        if (showInfoLevel)
        {
            Debug.Log("Level : " + level + " : Height : " + h + " : Block rows : " + ch + ", Separation : " + sep + ", New base size : " + new_base_size + ", Length : " + length + ", Ramp inclination : " + radians_to_degrees(Mathf.Atan(h / length)) + ", Start height : " + height + ", % total height : " + height * 100 / Height + ", Ramp length : " + old_length);
            writer.WriteLine("Level : " + level + " : Height : " + h + " : Block rows : " + ch + ", Separation : " + sep + ", New base size : " + new_base_size + ", Length : " + length + ", Ramp inclination : " + radians_to_degrees(Mathf.Atan(h / length)) + ", Start height : " + height + ", % total height : " + height * 100 / Height + ", Ramp length : " + old_length);
            csviterwriter.WriteLine(level + ";" + h + ";" + ch + ";" + sep.ToString("F2") + ";" + new_base_size.ToString("F2") + ";" + length.ToString("F2") + ";" + radians_to_degrees(Mathf.Atan(h / length)).ToString("F2") + ";" + height.ToString("F2") + ";" + (height * 100 / Height).ToString("F2") + ";" + old_length.ToString("F2"));
        }

        // Draw pyramid
        //Debug.Log("CH : "+ch);
        float last_sepi = 0, last_length = 0, last_h = 0;
        Vector3 last_v0 = new Vector3(0, 0, 0);
        Vector3 last_v1 = new Vector3(0, 0, 0);
        GameObject obj, lastobj;
        Vector3 scaleChange;
        float distblocks = 0;
        float distblocksramp = 0;
        float forceblocks = 0;
        float forceblocksramp = 0;
        float nbs2 = new_base_size / 2;
        float bw2 = blockwide / 2;
        float bh2 = currentCourseHeight / 2;
        float b1 = (base_size - sep) / ch;
        float bht2 = bh2 / pyramid_inclination_tg;
        float bhtl = Mathf.Sqrt(currentCourseHeight * currentCourseHeight + bht2 * bht2)+0.3f;       
        int biter = 0;
        int blockant = 0;
        int num_block_real = 0;
        float distant = 0;
        float forceant = 0;
        float inclirampant = 0;
        float forceblocksrow_horiz_total = force_old_horiz;
        float forceblocksrow_vert_total = force_old_vert;
        float forceblocksrow_total_total = force_old_length;
        float rampInclinationRad = RampInclination * Mathf.Deg2Rad;
        int row_ori = row;
        float distramprow_last = 0;
        // ramp        
        float a1 = Mathf.Atan(sep / (base_size - sep));
        float a2 = Mathf.Atan(h / (base_size - sep));
        setbackKhufuCourseHeights = 0.0f;
        for (int i = 0; i < ch; i++)
        {
            if (progressBar && !Sequenced)
            {
                progressBar.IncProgress("Generating course "+i+ " at iteration " + (level+1));
                yield return null;
            }

            // uses thickness from Khufu courses
            if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
            {
                currentCourseHeight = GetBlockHeightForRow(row_ori+i);
                bh2 = currentCourseHeight / 2;
                bht2 = bh2 / pyramid_inclination_tg;
                bhtl = Mathf.Sqrt(currentCourseHeight * currentCourseHeight + bht2 * bht2) + 0.3f;
                setbackWide = currentCourseHeight / Mathf.Tan(degrees_to_radians(PyramidInclination));
            }

            GameObject row_gameObject = new GameObject();
            row_gameObject.name = "Course_" + level +"_"+i;
            row_gameObject.transform.parent = iter_gameObject.transform;
            row_gameObject.isStatic = isStatic;
            if (Sequenced)
                row_gameObject = objParent;

            float sepi = sep * i / ch;
            int bxi = 0;
            float blocksfraction = 0;
            float distblocksrow = 0;
            float distblocksramprow = 0;
            float forceblocksrow = 0;
            float forceblocksrow_horiz = 0;
            float forceblocksrow_vert = 0;
            float forceblocksrow_total = 0;
            float forceblocksramprow = force_old_length;
            float distramprow = 0;
            float incliramprow = 0;
            float forceramprow = 0;
            float totalHeightUpToCurrentCourse = 0;
            float totalHeightUpToCurrentCourseTotal = 0;
            if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
            {                
                for (int hIndex = 0; hIndex < i; hIndex++)
                {
                    totalHeightUpToCurrentCourse += GetBlockHeightForRow(row_ori + hIndex);
                }
                setbackKhufuCourseHeights += setbackWide;
                sepi = setbackKhufuCourseHeights;
            }
            else
                totalHeightUpToCurrentCourse = i * currentCourseHeight;
            if (rampMethod == RampMethodType.Straight || rampMethod == RampMethodType.Internal)
                if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
                {
                    for (int hIndex = 0; hIndex < row_ori + i; hIndex++)
                    {
                        totalHeightUpToCurrentCourseTotal += GetBlockHeightForRow(hIndex);
                    }
                }
                else
                    totalHeightUpToCurrentCourseTotal = (row_ori + i) * currentCourseHeight;

            v0 = new Vector3(bs2, 0, bs2);
            if (optionRamp == 0)
            {
                v1 = new Vector3(bs2 - sepi, totalHeightUpToCurrentCourse, -(bs2 - sepi));                    
                incliramprow = Mathf.Atan(totalHeightUpToCurrentCourse / (base_size - sepi));                    
                distramprow = Vector3.Distance(v0, v1);
                forceramprow = (old_length + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
            }
            else
            {
                v1 = new Vector3(bs2 - sep * (i + 1) / ch, totalHeightUpToCurrentCourse, bs2 - b1 * i);
                if (i > 0)
                {
                    distramprow = Vector3.Distance(v0, v1);
                    incliramprow = Mathf.Atan(totalHeightUpToCurrentCourse / distramprow);
                    forceramprow = (old_length + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
                }
                else
                {
                    distramprow = 0;
                    incliramprow = getTanFromDegrees(RampInclination);                 
                    forceramprow = old_length * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
                }
            }
            last_sepi = sepi;
            last_length = distramprow;
            if (rampMethod == RampMethodType.Straight)
            {
                v1 = GetStraightRampTerraceEntryPoint(totalHeightUpToCurrentCourseTotal);
                incliramprow = ramp_inclination_atg;
                distramprow = totalHeightUpToCurrentCourseTotal / Mathf.Sin(rampInclinationRad);
                forceramprow = distramprow * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
            }
            else
            if (rampMethod == RampMethodType.Spiral || rampMethod == RampMethodType.Internal)
            {                                   
                float sepi_spiral = sep * i / ch_spiral;
                float b1_spiral = (base_size_spiral - sep_spiral) / ch_spiral;
                Vector3 v0_spiral = new Vector3(bs2_spiral, 0, bs2_spiral);
                Vector3 v1_spiral;
                if (optionRamp == 0)
                {
                    //v1 = new Vector3(bs2 - sepi, totalHeightUpToCurrentCourse, -(bs2 - sepi));
                    v1_spiral = new Vector3(bs2_spiral - sepi_spiral, totalHeightUpToCurrentCourse, -(bs2_spiral - sepi_spiral));
                    incliramprow = Mathf.Atan(totalHeightUpToCurrentCourse / (base_size_spiral - sepi_spiral));
                    distramprow = Vector3.Distance(v0_spiral, v1_spiral);
                    forceramprow = (old_length_spiral + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));                    
                }
                else
                {
                    //v1 = new Vector3(bs2 - sep * (i + 1) / ch, totalHeightUpToCurrentCourse, bs2 - b1 * i);
                    v1_spiral = new Vector3(bs2_spiral - sep_spiral * (i + 1) / ch_spiral, totalHeightUpToCurrentCourse, bs2_spiral - b1_spiral * i);
                    if (i > 0)
                    {
                        distramprow = Vector3.Distance(v0_spiral, v1_spiral);
                        incliramprow = Mathf.Atan(totalHeightUpToCurrentCourse / distramprow);
                        forceramprow = (old_length_spiral + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
                    }
                    else
                    {
                        distramprow = 0;
                        incliramprow = getTanFromDegrees(RampInclination);
                        forceramprow = old_length_spiral * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
                    }
                }
                // the last one
                if (rampMethod == RampMethodType.Spiral && (i == ch-1))
                {
                    v1_spiral = new Vector3(bs2_spiral - sep_spiral * (i + 2) / ch_spiral, totalHeightUpToCurrentCourse, bs2_spiral - b1_spiral * (i+1));
                    distramprow = Vector3.Distance(v0_spiral, v1_spiral);
                    incliramprow = Mathf.Atan(totalHeightUpToCurrentCourse / distramprow);
                    forceramprow = (old_length_spiral + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
                }
                length_spiral = distramprow;
            }

            last_h = totalHeightUpToCurrentCourse;
            last_v0 = v0;
            last_v1 = v1;
            numberOfBlocksX = 0;
            lastNumberOfBlockDrawnX = -1;
            GameObject[] createdObjectsArray = new GameObject[(int) (base_size / blockwide)+1];
            x = -bs2 + sepi + bw2;
            v0 = new Vector3(bs2 - sepi, totalHeightUpToCurrentCourse, -(bs2 - sepi));
            while (x < bs2 - sepi - bw2)
            {
                lastCubeDrawn = null;
                numberOfBlocksX++;
                numberOfBlocksZ = 0;
                lastNumberOfBlockDrawnZ = -1;
                z = -bs2 + sepi + bw2;
                lastobj = null;
                while (z < bs2 - sepi - bw2)
                {
                    numberOfBlocksZ++;
                    num_block_real++;
                    obj = null;
                    if ((!DrawOnlyRow || row == DrawRow) &&
                        ((x < -bs2 + sepi + blockwide) || (x > bs2 - sepi - blockwide) || (z < -bs2 + sepi + blockwide) || (z > bs2 - sepi - blockwide) || (DrawUntilRow && row == DrawRow && !isRigidBody) ||
                        (DrawBlocks > 1 && (x < -bs2 + sepi + blockwide * DrawBlocks) || (x > bs2 - sepi - blockwide * DrawBlocks) || (z < -bs2 + sepi + blockwide * DrawBlocks) || (z > bs2 - sepi - blockwide * DrawBlocks))))
                    {
                        int rnd = UnityEngine.Random.Range(0, RockPrefab.Length);
                        if (!halfPyramid || x < 0)
                        {                            
                            obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                            obj.transform.localScale = new Vector3(blockwide - blockSeparation, currentCourseHeight, blockwide - blockSeparation);
                            obj.transform.name = "Block_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                            /*if (objParent)
                                obj.transform.parent = objParent.transform;*/
                            obj.transform.parent = row_gameObject.transform;
                            obj.isStatic = isStatic || row == 0;
                            if (isRigidBody)
                            {
                                Rigidbody rb = obj.GetComponent<Rigidbody>();
                                if (rb)
                                {
                                    rb.mass = massBlock;
                                    if (row > 0)
                                    {
                                        rb.isKinematic = false;
                                        rb.useGravity = true;
                                    }
                                }
                                // fixed Joints
                                if (useFixedJoints && lastobj && row > 0)
                                {
                                    FixedJoint fj = obj.AddComponent<FixedJoint>();
                                    fj.connectedBody = lastobj.GetComponent<Rigidbody>();
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;
                                }
                                GameObject objant = GameObject.Find("Block_" + row + "_" + (numberOfBlocksX - 1) + "_" + numberOfBlocksZ);
                                if (useFixedJoints && objant && row > 0)
                                {
                                    FixedJoint fj = obj.AddComponent<FixedJoint>();
                                    fj.connectedBody = objant.GetComponent<Rigidbody>();
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;
                                }
                                // Raycast to detect GameObjects in that direction
                                if (row > 0)
                                {
                                    RaycastHit hit;
                                    if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, currentCourseHeight, blockLayer))
                                    {
                                        Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                                        if (otherRb != null)
                                        {
                                            // Connect the new object with the hit object
                                            FixedJoint fj = obj.AddComponent<FixedJoint>();
                                            fj.connectedBody = otherRb;
                                            fj.breakForce = 1000000;
                                            fj.breakTorque = 1000000;
                                        }
                                    }
                                }
                            }
                            // internal ramp not to draw blocks in the last terrace
                            if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                                obj.SetActive(false);
                            lastobj = obj;
                        }
                        // draw all blocks
                        if (DrawAll && (!halfPyramid || x<0) && lastCubeDrawn && lastNumberOfBlockDrawnZ < numberOfBlocksZ - 1)
                        {
                            GameObject large_cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            large_cube.transform.name = "LargeCube_" + row + "_" + i + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                            //large_cube.transform.parent = objParent.transform;
                            large_cube.transform.parent = row_gameObject.transform;
                            large_cube.transform.position = (lastCubeDrawn.transform.position + obj.transform.position) / 2;
                            float distance = Vector3.Distance(lastCubeDrawn.transform.position, obj.transform.position);
                            large_cube.transform.localScale = new Vector3(blockwide-blockSeparation, currentCourseHeight, distance - blockwide - blockSeparation);
                            large_cube.GetComponent<MeshRenderer>().material = m_Material;
                            large_cube.tag = "Block";
                            large_cube.isStatic = isStatic || row == 0;

                            // internal ramp not to draw blocks in the last terrace
                            if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                                large_cube.SetActive(false);                            

                            if (isRigidBody)
                            {
                                Rigidbody rb = large_cube.AddComponent<Rigidbody>();
                                if (rb)
                                {
                                    rb.mass = massBlock * (numberOfBlocksZ - lastNumberOfBlockDrawnZ);
                                    rb.isKinematic = true;
                                    rb.useGravity = false;                                    
                                }
                                // fixed Joints
                                if (useFixedJoints && lastCubeDrawn && row > 0)
                                {
                                    FixedJoint fj = large_cube.AddComponent<FixedJoint>();
                                    fj.connectedBody = lastCubeDrawn.GetComponent<Rigidbody>();
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;

                                    fj = obj.GetComponent<FixedJoint>();
                                    if (fj)
                                        fj.connectedBody = large_cube.GetComponent<Rigidbody>();
                                }
                                // Raycast to detect GameObjects in that direction
                                if (row > 0)
                                {
                                    RaycastHit hit;
                                    if (Physics.Raycast(large_cube.transform.position, Vector3.down, out hit, currentCourseHeight, blockLayer))
                                    {
                                        Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                                        if (otherRb != null)
                                        {
                                            // Connect the new object with the hit object
                                            FixedJoint fj = large_cube.AddComponent<FixedJoint>();
                                            fj.connectedBody = otherRb;
                                            fj.breakForce = 1000000;
                                            fj.breakTorque = 1000000;
                                        }
                                    }                                    
                                }
                                GameObject objant = GameObject.Find("LargeCube_" + row + "_" + i + "_" + (numberOfBlocksX-1) + "_" + numberOfBlocksZ);
                                if (useFixedJoints && objant)
                                {
                                    FixedJoint fj = large_cube.AddComponent<FixedJoint>();
                                    fj.connectedBody = objant.GetComponent<Rigidbody>();
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;
                                }
                            }                           
                        }
                        lastNumberOfBlockDrawnZ = numberOfBlocksZ;
                        lastCubeDrawn = obj;
                        numberOfBlocksDrawn++;
                        if (MethodInsideRamp && (!halfPyramid || x < 0) && ((x < -bs2 + sepi + blockwide) || (x > bs2 - sepi - blockwide) || (z < -bs2 + sepi + blockwide) || (z > bs2 - sepi - blockwide)))
                        {
                            if (x < -bs2 + sepi + blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x - bw2 / 2, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                                obj.transform.localScale = new Vector3(0.5f * blockwide, currentCourseHeight, blockwide);
                            }
                            else
                            if (x > bs2 - sepi - blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x + bw2 / 2, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                                obj.transform.localScale = new Vector3(0.5f * blockwide, currentCourseHeight, blockwide);
                            }
                            else
                            if (z < -bs2 + sepi + blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z - bw2 / 2), Quaternion.identity);
                                obj.transform.localScale = new Vector3(blockwide, currentCourseHeight, 0.5f * blockwide);
                            }
                            else
                            if (z > bs2 - sepi - blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z + bw2 / 2), Quaternion.identity);
                                obj.transform.localScale = new Vector3(blockwide, currentCourseHeight, 0.5f * blockwide);
                            }
                            obj.isStatic = isStatic || row == 0;
                            if (isRigidBody && row > 0)
                            {
                                Rigidbody rb = obj.GetComponent<Rigidbody>();
                                if (rb)
                                {
                                    rb.mass = massBlock;
                                    rb.isKinematic = false;
                                    rb.useGravity = true;
                                }
                            }
                            /*if (objParent)
                                obj.transform.parent = objParent.transform;*/
                            obj.transform.parent = row_gameObject.transform;
                        }
                        if (DrawCasing && (!halfPyramid || x < 0) && ((x < -bs2 + sepi + blockwide) || (z < -bs2 + sepi + blockwide)))
                        {
                            if (lastCubeDrawn)
                                lastCubeDrawn.GetComponent<MeshRenderer>().material = m_Material_Blank;                            
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            if (x < -bs2 + sepi + blockwide)
                            {
                                cube.transform.position = objParent.transform.position + new Vector3(x - bw2 - bht2, height + bh2 + totalHeightUpToCurrentCourse, z);
                                cube.transform.localScale = new Vector3(0.1f, bhtl, blockwide);
                                cube.transform.rotation = Quaternion.Euler(0, 0, -(90 - PyramidInclination));
                            }
                            else
                            if (z < -bs2 + sepi + blockwide)
                            {
                                cube.transform.position = objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z - bw2 - bht2);
                                cube.transform.localScale = new Vector3(blockwide, bhtl, 0.1f);
                                cube.transform.rotation = Quaternion.Euler(90 - PyramidInclination, 0, 0);
                            }
                            cube.isStatic = true;
                            cube.GetComponent<MeshRenderer>().material = m_Material_Blank;
                            cube.AddComponent<Rigidbody>();
                            cube.GetComponent<Rigidbody>().isKinematic = true;
                            cube.GetComponent<Rigidbody>().useGravity = false;
                            cube.tag = "Block";
                            /*if (objParent)
                                cube.transform.parent = objParent.transform;*/
                            cube.transform.parent = row_gameObject.transform;
                            // internal ramp not to draw blocks in the last terrace
                            if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                                cube.SetActive(false);
                        }
                    }
                    z += blockwide;
                    numberOfBlocks++;
                    bxi++;
                    biter++;
                    v0 = new Vector3(x, totalHeightUpToCurrentCourse, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal                    
                    if (rampMethod == RampMethodType.Straight)
                    {
                        distblocksrow += distramprow + dist_horiz;   // distance ramp before + distance ramp + distance block
                        distblocksramprow += distramprow;                          // distance ramp before + distance ramp
                    }
                    else
                    if (rampMethod == RampMethodType.Spiral)
                    {
                        distblocksrow += old_length_spiral + distramprow + dist_horiz;    // distance ramp before + distance ramp + distance block
                        distblocksramprow += old_length_spiral +  distramprow;                          // distance ramp before + distance ramp
                    }
                    else
                    {
                        distblocksrow += old_length + distramprow + dist_horiz;   // distance ramp before + distance ramp + distance block
                        distblocksramprow += old_length + distramprow;                          // distance ramp before + distance ramp
                    }
                    forceblocksrow_horiz += dist_horiz * frictionCoef * massBlock * g;    // force horizontal row
                    forceblocksrow_vert += forceramprow;                                                // force vertical row
                    forceblocksrow_horiz_total += dist_horiz * frictionCoef * massBlock * g; // force row total
                    forceblocksrow_vert_total += forceramprow;
                    forceblocksrow_total += forceramprow + dist_horiz * frictionCoef * massBlock * g;
                    forceblocksrow += force_old_length + forceramprow + dist_horiz * frictionCoef * massBlock * g;
                    forceblocksramprow += forceramprow;
                    forceblocksrow_total_total += forceramprow + dist_horiz * frictionCoef * massBlock * g;

                    if (DrawUntilRow && row == DrawRow)
                        heightGranite = height + bh2 + totalHeightUpToCurrentCourse;
                    // save the object in the array for later use
                    createdObjectsArray[numberOfBlocksZ] = obj;

                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }
                // last block Z
                if ((!DrawOnlyRow || row == DrawRow) && (z != bs2 - sepi) && (!halfPyramid || x < 0))
                {
                    // adapt block size
                    scaleChange = new Vector3(blockwide- blockSeparation, currentCourseHeight, blockwide - blockSeparation);
                    scaleChange.z = bs2 - sepi - (z - bw2);
                    z = z - (blockwide - scaleChange.z) / 2;
                    /*if (i == 0)
                        obj = Instantiate(RockDivPrefab, new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                    else*/
                    obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                    obj.transform.name = "Block_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                    /*if (objParent)
                        obj.transform.parent = objParent.transform;*/
                    obj.transform.parent = row_gameObject.transform;
                    obj.transform.localScale = scaleChange;
                    // internal ramp not to draw blocks in the last terrace
                    if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                        obj.SetActive(false);

                    float totalMass = 0;
                    if (isRigidBody)
                    {
                        Rigidbody rb = obj.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.mass = massBlock * scaleChange.z / blockwide;
                            totalMass = rb.mass;
                        }
                    }

                    if (lastobj)
                    {
                        GameObject objnew = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)],
                                    new Vector3(lastobj.transform.position.x,
                                                lastobj.transform.position.y,
                                                lastobj.transform.position.z + obj.transform.localScale.z / 2),
                                    Quaternion.identity);
                        objnew.transform.name = "BlockComb_Z_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                        objnew.transform.localScale = new Vector3(lastobj.transform.localScale.x,lastobj.transform.localScale.y,lastobj.transform.localScale.z + obj.transform.localScale.z);
                        /*if (objParent)
                            objnew.transform.parent = objParent.transform;*/
                        objnew.transform.parent = row_gameObject.transform;
                        // internal ramp not to draw blocks in the last terrace
                        if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                            objnew.SetActive(false);
                        if (DrawCasing)
                            objnew.GetComponent<MeshRenderer>().material = m_Material_Blank;

                        if (isRigidBody)
                        {
                            Rigidbody rb1 = lastobj.GetComponent<Rigidbody>();
                            if (rb1)
                                totalMass += rb1.mass;                            
                        }

                        // delete previous objects
                        Destroy(lastobj);
                        Destroy(obj);
                        /*Rigidbody rb = obj.GetComponent<Rigidbody>();
                        obj.transform.name = obj.transform.name + "_merged1";
                        obj.isStatic = true;
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb = lastobj.GetComponent<Rigidbody>();
                        lastobj.transform.name = lastobj.transform.name + "_merged2";
                        lastobj.isStatic = true;
                        rb.isKinematic = true;
                        rb.useGravity = false;*/

                        lastobj = null;
                        obj = objnew;
                    }

                    obj.isStatic = isStatic || row==0;
                    if (isRigidBody)
                    {
                        Rigidbody rb = obj.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.mass = totalMass;
                            //rb.isKinematic = false;
                            //rb.useGravity = true;
                        }
                        // fixed Joints
                        GameObject objant = GameObject.Find("Block_" + row + "_" + numberOfBlocksX + "_" + (numberOfBlocksZ-1));
                        if (useFixedJoints && objant && row > 0)
                        {
                            FixedJoint fj = obj.AddComponent<FixedJoint>();
                            fj.connectedBody = objant.GetComponent<Rigidbody>();
                            fj.breakForce = 1000000;
                            fj.breakTorque = 1000000;
                        }
                        objant = GameObject.Find("BlockComb_Z_" + row + "_" + (numberOfBlocksX-1) + "_" + numberOfBlocksZ);
                        if (useFixedJoints && objant && row > 0)
                        {
                            FixedJoint fj = obj.AddComponent<FixedJoint>();
                            fj.connectedBody = objant.GetComponent<Rigidbody>();
                            fj.breakForce = 1000000;
                            fj.breakTorque = 1000000;
                        }
                        // Raycast to detect GameObjects in that direction
                        if (row > 0)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, currentCourseHeight, blockLayer))
                            {
                                Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                                if (otherRb != null)
                                {
                                    // Connect the new object with the hit object
                                    FixedJoint fj = obj.AddComponent<FixedJoint>();
                                    fj.connectedBody = otherRb;
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;
                                }
                            }
                        }
                    }
                    lastobj = obj;

                    numberOfBlocksDrawn++;
                    if (DrawCasing)
                    {
                        if (lastCubeDrawn)
                            lastCubeDrawn.GetComponent<MeshRenderer>().material = m_Material_Blank;
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z + scaleChange.z / 2 + bht2);
                        cube.transform.localScale = new Vector3(0.1f, bhtl, blockwide);
                        cube.transform.rotation = Quaternion.Euler(0, 90, 360 - (90 - PyramidInclination));
                        cube.isStatic = true;
                        cube.GetComponent<MeshRenderer>().material = m_Material_Blank;
                        cube.AddComponent<Rigidbody>();
                        cube.GetComponent<Rigidbody>().isKinematic = true;
                        cube.GetComponent<Rigidbody>().useGravity = false;
                        cube.tag = "Block";
                        /*if (objParent)
                            cube.transform.parent = objParent.transform;*/
                        cube.transform.parent = row_gameObject.transform;
                        // internal ramp not to draw blocks in the last terrace
                        if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                            cube.SetActive(false);
                    }
                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + scaleChange.z / blockwide;
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, totalHeightUpToCurrentCourse, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal
                    if (rampMethod == RampMethodType.Straight)
                    {
                        distblocksrow += distramprow + dist_horiz;
                        distblocksramprow += distramprow;
                    }
                    else
                    if (rampMethod == RampMethodType.Spiral)
                    {
                        distblocksrow += old_length_spiral + distramprow + dist_horiz;
                        distblocksramprow += old_length_spiral + distramprow;
                    }
                    else
                    {
                        distblocksrow += old_length + distramprow + dist_horiz;
                        distblocksramprow += old_length + distramprow;
                    }                    
                    forceblocksrow_horiz += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert += forceramprow;
                    forceblocksrow_horiz_total += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert_total += forceramprow;
                    forceblocksrow_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow += force_old_length + forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksramprow += forceramprow;
                    forceblocksrow_total_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }
                x += blockwide;
                if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
            }
            // last block X
            if ((!DrawOnlyRow || row == DrawRow) && (x != bs2 - sepi) && (!halfPyramid || x < 0))
            {
                // adapt block size
                scaleChange = new Vector3(blockwide - blockSeparation, currentCourseHeight, blockwide - blockSeparation);
                scaleChange.x = bs2 - sepi - (x - bw2);
                x = x - (blockwide - scaleChange.x) / 2;
                z = -bs2 + sepi + bw2;
                numberOfBlocksZ = 0;
                lastobj = null;
                while (z < bs2 - sepi - bw2)
                {
                    numberOfBlocksZ++;
                    if ((x < -bs2 + sepi + blockwide) || (x > bs2 - sepi - blockwide) || (z < -bs2 + sepi + blockwide) || (z > bs2 - sepi - blockwide) || (DrawUntilRow && row == DrawRow && !isRigidBody))
                    {
                        /*if (i == 0)
                            obj = Instantiate(RockDivPrefab, new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        else*/
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                        obj.transform.name = "Block_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                        /*if (objParent)
                            obj.transform.parent = objParent.transform;*/
                        obj.transform.parent = row_gameObject.transform;
                        obj.transform.localScale = scaleChange;
                        float totalMass = 0;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = massBlock * scaleChange.x / blockwide;
                                totalMass = rb.mass;
                            }
                        }
                        // internal ramp not to draw blocks in the last terrace
                        if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                            obj.SetActive(false);

                        GameObject objant = createdObjectsArray[numberOfBlocksZ];
                        if (objant)
                        {
                            GameObject objnew = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)],
                                    new Vector3(objant.transform.position.x+obj.transform.localScale.x / 2,
                                                objant.transform.position.y,
                                                objant.transform.position.z), 
                                    Quaternion.identity);
                            objnew.transform.name = "BlockComb_X_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                            objnew.transform.localScale = new Vector3(objant.transform.localScale.x+ obj.transform.localScale.x, objant.transform.localScale.y,objant.transform.localScale.z);
                            /*if (objParent)
                                objnew.transform.parent = objParent.transform;*/
                            objnew.transform.parent = row_gameObject.transform;
                            // internal ramp not to draw blocks in the last terrace
                            if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                                objnew.SetActive(false);
                            if (DrawCasing)
                                objnew.GetComponent<MeshRenderer>().material = m_Material_Blank;

                            if (isRigidBody)
                            {
                                Rigidbody rb1 = objant.GetComponent<Rigidbody>();
                                if (rb1)
                                    totalMass += rb1.mass;
                            }

                            // delete previous objects
                            Destroy(objant);
                            Destroy(obj);
                            /*Rigidbody rb = obj.GetComponent<Rigidbody>();
                            obj.transform.name = obj.transform.name+"_merged1";
                            obj.isStatic = true;
                            rb.isKinematic = true;
                            rb.useGravity = false;
                            rb = objant.GetComponent<Rigidbody>();
                            objant.transform.name = objant.transform.name + "_merged2";
                            objant.isStatic = true;
                            rb.isKinematic = true;
                            rb.useGravity = false;*/

                            lastobj = null;                            
                            obj = objnew;
                        }                        
                        obj.isStatic = isStatic || row == 0;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = totalMass;
                                //rb.isKinematic = false;
                                //rb.useGravity = true;
                            }
                            // fixed Joints
                            objant = GameObject.Find("BlockComb_X_" + row + "_" + numberOfBlocksX + "_" + (numberOfBlocksZ-1));
                            if (useFixedJoints && objant && row > 0)
                            {
                                FixedJoint fj = obj.AddComponent<FixedJoint>();
                                fj.connectedBody = objant.GetComponent<Rigidbody>();
                                fj.breakForce = 1000000;
                                fj.breakTorque = 1000000;
                            }
                            objant = GameObject.Find("Block_" + row + "_" + (numberOfBlocksX - 1) + "_" + numberOfBlocksZ);
                            if (useFixedJoints && objant && row > 0)
                            {
                                FixedJoint fj = obj.AddComponent<FixedJoint>();
                                fj.connectedBody = objant.GetComponent<Rigidbody>();
                                fj.breakForce = 1000000;
                                fj.breakTorque = 1000000;
                            }
                            // Raycast to detect GameObjects in that direction
                            if (row > 0)
                            {
                                RaycastHit hit;
                                if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, currentCourseHeight, blockLayer))
                                {
                                    Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                                    if (otherRb != null)
                                    {
                                        // Connect the new object with the hit object
                                        FixedJoint fj = obj.AddComponent<FixedJoint>();
                                        fj.connectedBody = otherRb;
                                        fj.breakForce = 1000000;
                                        fj.breakTorque = 1000000;
                                    }
                                }
                            }
                        }
                        lastobj = obj;

                        numberOfBlocksDrawn++;
                        if (DrawCasing)
                        {
                            if (lastCubeDrawn)
                                lastCubeDrawn.GetComponent<MeshRenderer>().material = m_Material_Blank;
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            cube.transform.position = objParent.transform.position + new Vector3(x + scaleChange.x / 2 + bht2, height + bh2 + totalHeightUpToCurrentCourse, z);
                            cube.transform.localScale = new Vector3(0.1f, bhtl, blockwide);
                            cube.transform.rotation = Quaternion.Euler(0, 0, (90 - PyramidInclination));
                            cube.isStatic = true;
                            cube.GetComponent<MeshRenderer>().material = m_Material_Blank;
                            cube.AddComponent<Rigidbody>();
                            cube.GetComponent<Rigidbody>().isKinematic = true;
                            cube.GetComponent<Rigidbody>().useGravity = false;
                            cube.tag = "Block";
                            /*if (objParent)
                                cube.transform.parent = objParent.transform;*/
                            cube.transform.parent = row_gameObject.transform;
                            // internal ramp not to draw blocks in the last terrace
                            if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                                cube.SetActive(false);
                        }
                    }
                    z += blockwide;
                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + scaleChange.x / blockwide;
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, totalHeightUpToCurrentCourse, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal
                    if (rampMethod == RampMethodType.Straight)
                    {
                        distblocksrow += distramprow + dist_horiz;
                        distblocksramprow += distramprow;
                    }
                    else
                    if (rampMethod == RampMethodType.Spiral)
                    {
                        distblocksrow += old_length_spiral + distramprow + dist_horiz;
                        distblocksramprow += old_length_spiral + distramprow;
                    }
                    else
                    {

                        distblocksrow += old_length + distramprow + dist_horiz;
                        distblocksramprow += old_length + distramprow;
                    }
                    forceblocksrow_horiz += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert += forceramprow;
                    forceblocksrow_horiz_total += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert_total += forceramprow;
                    forceblocksrow_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow += force_old_length + forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksramprow += forceramprow;
                    forceblocksrow_total_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }
                // last block Z
                if (z != bs2 - sepi)
                {
                    // adapt block size
                    scaleChange.z = bs2 - sepi - (z - bw2);
                    z = z - (blockwide - scaleChange.z) / 2;
                    obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                    /*if (objParent)
                        obj.transform.parent = objParent.transform;*/
                    obj.transform.parent = row_gameObject.transform;
                    obj.transform.localScale = scaleChange;
                    float totalMass = 0;
                    if (isRigidBody)
                    {
                        Rigidbody rb = obj.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.mass = massBlock * scaleChange.z / blockwide;
                            totalMass = rb.mass;
                        }
                    }
                    // internal ramp not to draw blocks in the last terrace
                    if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                        obj.SetActive(false);

                    if (lastobj)
                    {
                        GameObject objnew = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)],
                                    new Vector3(lastobj.transform.position.x,
                                                lastobj.transform.position.y,
                                                lastobj.transform.position.z + obj.transform.localScale.z / 2),
                                    Quaternion.identity);
                        objnew.transform.name = "BlockComb_XZ_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                        objnew.transform.localScale = new Vector3(lastobj.transform.localScale.x, lastobj.transform.localScale.y, lastobj.transform.localScale.z + obj.transform.localScale.z);
                        /*if (objParent)
                            objnew.transform.parent = objParent.transform;*/
                        objnew.transform.parent = row_gameObject.transform;
                        // internal ramp not to draw blocks in the last terrace
                        if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                            objnew.SetActive(false);
                        if (DrawCasing)
                            objnew.GetComponent<MeshRenderer>().material = m_Material_Blank;

                        if (isRigidBody)
                        {
                            Rigidbody rb1 = lastobj.GetComponent<Rigidbody>();
                            if (rb1)
                                totalMass += rb1.mass;
                        }

                        // delete previous objects
                        FixedJoint fj = lastobj.GetComponent<FixedJoint>();
                        Destroy(lastobj);
                        Destroy(obj);
                        /*Rigidbody rb = obj.GetComponent<Rigidbody>();
                        obj.transform.name = obj.transform.name + "_merged1";
                        obj.isStatic = true;
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb = lastobj.GetComponent<Rigidbody>();
                        lastobj.transform.name = lastobj.transform.name + "_merged2";
                        lastobj.isStatic = true;
                        rb.isKinematic = true;
                        rb.useGravity = false;*/

                        //delete previous
                        GameObject Comb_Z = GameObject.Find("BlockComb_Z_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ);
                        if (Comb_Z)
                            Destroy(Comb_Z);

                        lastobj = null;
                        if (fj)
                        {
                            lastobj = fj.connectedBody.gameObject;
                        }
                        obj = objnew;
                    }
                    obj.isStatic = isStatic || row == 0;
                    if (isRigidBody)
                    {
                        Rigidbody rb = obj.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.mass = totalMass;
                            //rb.isKinematic = false;
                            //rb.useGravity = true;
                        }
                        // fixed Joints
                        if (useFixedJoints && lastobj && row > 0)
                        {
                            FixedJoint fj = obj.AddComponent<FixedJoint>();
                            fj.connectedBody = lastobj.GetComponent<Rigidbody>();
                            fj.breakForce = 1000000;
                            fj.breakTorque = 1000000;
                        }
                        GameObject objant = GameObject.Find("BlockComb_Z_" + row + "_" + (numberOfBlocksX - 1) + "_" + numberOfBlocksZ);
                        if (useFixedJoints && objant && row > 0)
                        {
                            FixedJoint fj = obj.AddComponent<FixedJoint>();
                            fj.connectedBody = objant.GetComponent<Rigidbody>();
                            fj.breakForce = 1000000;
                            fj.breakTorque = 1000000;
                        }
                        // Raycast to detect GameObjects in that direction
                        if (row > 0)
                        {
                            RaycastHit hit;
                            if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, currentCourseHeight, blockLayer))
                            {
                                Rigidbody otherRb = hit.collider.GetComponent<Rigidbody>();
                                if (otherRb != null)
                                {
                                    // Connect the new object with the hit object
                                    FixedJoint fj = obj.AddComponent<FixedJoint>();
                                    fj.connectedBody = otherRb;
                                    fj.breakForce = 1000000;
                                    fj.breakTorque = 1000000;
                                }
                            }
                        }
                    }
                    numberOfBlocksDrawn++;
                    if (DrawCasing)
                    {
                        if (lastCubeDrawn)
                            lastCubeDrawn.GetComponent<MeshRenderer>().material = m_Material_Blank;
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = objParent.transform.position + new Vector3(x + scaleChange.x / 2 + bht2, height + bh2 + totalHeightUpToCurrentCourse, z);
                        cube.transform.localScale = new Vector3(0.1f, bhtl, blockwide);
                        cube.transform.rotation = Quaternion.Euler(0, 0, (90 - PyramidInclination));
                        cube.isStatic = true;
                        //cube.GetComponent<MeshRenderer>().material = m_Material_Blank;
                        cube.AddComponent<Rigidbody>();
                        cube.GetComponent<Rigidbody>().isKinematic = true;
                        cube.GetComponent<Rigidbody>().useGravity = false;
                        cube.tag = "Block";
                        /*if (objParent)
                            cube.transform.parent = objParent.transform;*/
                        cube.transform.parent = row_gameObject.transform;
                        // internal ramp not to draw blocks in the last terrace
                        if (rampMethod == RampMethodType.Internal && DrawUntilRow && row == DrawRow)
                            cube.SetActive(false);
                    }

                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, totalHeightUpToCurrentCourse, z);
                    var dist_horiz = Vector3.Distance(v0, v1);
                    if (rampMethod == RampMethodType.Straight)
                    {
                        distblocksrow += distramprow + dist_horiz;
                        distblocksramprow += distramprow;
                    }
                    else
                    if (rampMethod == RampMethodType.Spiral)
                    {
                        distblocksrow += old_length_spiral + distramprow + dist_horiz;
                        distblocksramprow += old_length_spiral + distramprow;
                    }
                    else
                    {
                        distblocksrow += old_length + distramprow + dist_horiz;
                        distblocksramprow += old_length + distramprow;
                    }
                    forceblocksrow_horiz += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert += forceramprow;
                    forceblocksrow_horiz_total += dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow_vert_total += forceramprow;
                    forceblocksrow_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksrow += force_old_length + forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    forceblocksramprow += forceramprow;
                    forceblocksrow_total_total += forceramprow + dist_horiz * frictionCoef * massBlock * g * blockscale;
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }
                x += blockwide;
                if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
            }
            bxi += Mathf.CeilToInt(blocksfraction);
            biter += Mathf.CeilToInt(blocksfraction);
            numberOfBlocks += Mathf.CeilToInt(blocksfraction);
            createdObjectsArray = null;

            if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
            // row values      
            if (showInfoRow || showInfoLevel)
            {
                if (showInfoRow)
                {
                    int num_ramps_headways = 1;
                    if (PyramidHeadwayType == PyramidHeadwayType.Double_Ramp)
                        num_ramps_headways = 2;
                    if (PyramidHeadwayType == PyramidHeadwayType.Four_Ramp)
                        num_ramps_headways = 3;
                    if (PyramidHeadwayType == PyramidHeadwayType.Adaptative)
                    {
                        if (level==0)
                        {
                            if (row < 9 && Method16Ramp)
                                num_ramps_headways = 12;
                            else
                            if (row < 20 && Method8Ramp)
                                num_ramps_headways = 6;
                            else
                                num_ramps_headways = 3;
                        }
                        else
                            num_ramps_headways = 3;
                    }
                    if (!Method4Ramp && !Method2Ramp)
                        num_ramps_headways = 1;

                    float current_headway = MinHeadway + (old_length + distramprow) / ramp_total_length * (MaxHeadway - MinHeadway);
                    if (rampMethod == RampMethodType.Straight)
                    {
                        current_headway = MinHeadway + (distramprow) / ramp_total_length * (MaxHeadway - MinHeadway);                       
                    }
                    else
                    if (rampMethod == RampMethodType.Spiral || rampMethod == RampMethodType.Internal)
                    {
                        current_headway = MinHeadway + (old_length_spiral + distramprow) / ramp_total_length * (MaxHeadway - MinHeadway);
                    }
                    float bxi_ramp = Mathf.Round(bxi / num_ramps_headways);
                    // Row;blocks;up ramps;blocks per ramp;fixed headway(min);adaptative headway(min);total time(min);adaptative total time(min);;total time(working years);adaptativive total time(working years)                        
                    csvheadwaywriter.WriteLine(i + ";" + bxi + ";" + num_ramps_headways+ ";"+ bxi_ramp + ";" + AverageHeadway.ToString("F2") + ";" + current_headway.ToString("F2") + ";" + (bxi_ramp * AverageHeadway).ToString("F2") + ";" + (bxi_ramp * current_headway).ToString("F2") + ";" + (bxi_ramp * AverageHeadway / WorkingYearMinutes).ToString("F5") + ";" + (bxi_ramp * current_headway / WorkingYearMinutes).ToString("F5"));
                }

                float old_lenght_ant = old_length;
                if (rampMethod == RampMethodType.Straight)
                    old_length = 0;
                if (rampMethod == RampMethodType.Spiral || rampMethod == RampMethodType.Internal)
                    old_length = old_length_spiral;

                if (blockant > 0)
                {
                    Debug.Log("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km : " + (distblocksrow/1000).ToString("F3") + ", force blocks (MJ): " + (forceblocksrow/1000000).ToString("F3") + ", Decrement - blocks : " + (bxi * 100 / blockant).ToString("F2") + " %, Distance : " + (distblocksrow * 100 / distant).ToString("F2") + " %, Force : " + (forceblocksrow * 100 / forceant).ToString("F2") + " %");
                    if (showInfoLevel)
                        writer.WriteLine("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km: " + (distblocksrow/1000).ToString("F3") + ", force blocks (MJ): " + (forceblocksrow/10000000).ToString("F3") + ", Decrement - blocks : " + (bxi * 100 / blockant).ToString("F2") + " %, Distance : " + (distblocksrow * 100 / distant).ToString("F2") + " %, Force : " + (forceblocksrow * 100 / forceant).ToString("F2") + " %");
                    if (showInfoRow)
                    {
                        // Row;blocks;ramp inclination;Ramp length (m);Ramp length total (m);distance blocks (Km);distance blocks Ramp (Km);distance blocks Horiz (Km);Sum force blocks (MJ);Sum Vert. force blocks (MJ);Sum Horiz. force blocks (MJ);Vert. force blocks row (MJ);Horiz. force blocks row (MJ);Total force blocks row (MJ);% Decrement blocks;% increase Distance;% increase Force
                        csvrowwriter.WriteLine(i + ";" + bxi 
                                + ";" + radians_to_degrees(incliramprow).ToString("F2") + ";" + distramprow.ToString("F2") + ";" + (old_length + distramprow).ToString("F2") 
                                + ";" + radians_to_degrees(a1).ToString("F2") + ";" + radians_to_degrees(a2).ToString("F2") + ";" + (old_length + distramprow).ToString("F2")
                                + ";" + (distblocksrow / 1000).ToString("F3") + ";" + (distblocksramprow / 1000).ToString("F3") + ";" + ((distblocksrow - distblocksramprow) / 1000).ToString("F3") 
                                + ";" + (forceblocksrow_total_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert_total / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz_total / 1000000).ToString("F3") 
                                + ";" + (forceblocksrow_vert / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz / 1000000).ToString("F3") + ";" + (forceblocksrow_total / 1000000).ToString("F3") + ";" + bxi * 100 / blockant 
                                + ";" + (distblocksrow * 100 / distant).ToString("F2") + ";" + (forceblocksrow * 100 / forceant).ToString("F2"));                        
                    }
                }
                else
                {
                    Debug.Log("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km: " + (distblocksrow/1000).ToString("F3") + ", force blocks (MJ): " + (forceblocksrow/1000000).ToString("F2"));
                    if (showInfoLevel) 
                        writer.WriteLine("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow) + ", Length ramp : " + distramprow + ", distance blocks Km: " + (distblocksrow / 1000).ToString("F3") + ", force blocks : (MJ): " + (forceblocksrow / 1000000).ToString("F2"));
                    if (showInfoRow)
                        csvrowwriter.WriteLine(i + ";" + bxi 
                                + ";" + RampInclination.ToString("F2") + ";" + distramprow.ToString("F2") + ";" + (old_length + distramprow).ToString("F2")
                                + ";" + radians_to_degrees(a1).ToString("F2") + ";" + radians_to_degrees(a2).ToString("F2") + ";" + (old_length + distramprow).ToString("F2")
                                + ";" + (distblocksrow / 1000).ToString("F3") + ";" + (distblocksramprow / 1000).ToString("F3") + ";" + ((distblocksrow - distblocksramprow) / 1000).ToString("F3") 
                                + ";" + (forceblocksrow_total_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert_total / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz_total / 1000000).ToString("F3") 
                                + ";" + (forceblocksrow_vert / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz / 1000000).ToString("F3") + ";" + (forceblocksrow_total / 1000000).ToString("F3"));
                }                 

                old_length = old_lenght_ant;
            }

            // corners
            if (DrawCasing)
            {
                if (lastCubeDrawn)
                    lastCubeDrawn.GetComponent<MeshRenderer>().material = m_Material_Blank;
                // corner 1                        
                GameObject corner1 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner1.transform.position = objParent.transform.position + new Vector3(-bs2 + sepi - bht2, height + bh2 + totalHeightUpToCurrentCourse, -bs2 + sepi - bht2);
                corner1.transform.rotation = Quaternion.Euler(0, 90, 0);
                /*if (objParent)
                    corner1.transform.parent = objParent.transform;*/
                corner1.transform.parent = row_gameObject.transform;
                // corner 2                        
                GameObject corner2 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner2.transform.position = objParent.transform.position + new Vector3(bs2 - sepi + bht2, height + bh2 + totalHeightUpToCurrentCourse, -bs2 + sepi - bht2);
                corner2.transform.rotation = Quaternion.Euler(0, 0, 0);
                /*if (objParent)
                    corner2.transform.parent = objParent.transform;*/
                corner2.transform.parent = row_gameObject.transform;
                // corner 3                       
                GameObject corner3 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner3.transform.position = objParent.transform.position + new Vector3(bs2 - sepi + bht2, height + bh2 + totalHeightUpToCurrentCourse, bs2 - sepi + bht2);
                corner3.transform.rotation = Quaternion.Euler(0, 270, 0);
                /*if (objParent)
                    corner3.transform.parent = objParent.transform;*/
                corner3.transform.parent = row_gameObject.transform;
                // corner 4                        
                GameObject corner4 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner4.transform.position = objParent.transform.position + new Vector3(-bs2 + sepi - bht2, height + bh2 + totalHeightUpToCurrentCourse, bs2 - sepi + bht2);
                corner4.transform.rotation = Quaternion.Euler(0, 180, 0);
                /*if (objParent)
                    corner4.transform.parent = objParent.transform;*/
                corner4.transform.parent = row_gameObject.transform;
            }

            // Granite project 
            ProcessGraniteCalculations(row_ori + i, currentCourseHeight);

            // old value
            blockant = bxi;
            distant = distblocksrow;
            forceant = forceblocksrow;
            inclirampant = incliramprow;
            // sumatory
            distblocks += distblocksrow;
            distblocksramp += distblocksramprow;
            forceblocks += forceblocksrow;
            forceblocksramp += forceblocksramprow;
            distramprow_last = distramprow;
            row++;
            if (DrawUntilRow && row > DrawRow)
                break;
        }        

        if (showInfoLevelTotal)
        {
            Debug.Log("Blocks per level : " + biter + ", distance blocks per level : " + distblocks + ", force blocks per level : " + forceblocks + ", force blocks ramp per level : " + forceblocksramp + ", % force ramp per level : " + forceblocksramp * 100 / forceblocks + ", Total height : " + (height + h) + ", rows : " + row);
            writer.WriteLine("Blocks per level : " + biter + ", distance blocks per level : " + distblocks + ", force blocks per level : " + forceblocks + ", force blocks ramp per level : " + forceblocksramp + ", % force ramp per level : " + forceblocksramp * 100 / forceblocks + ", Total height : " + (height + h) + ", rows : " + row);
        }
        if (beforeBlocks > 0 && showInfoLevelDec)
        {
            Debug.Log("Decrement: Blocks per level : " + (beforeBlocks * 100 / biter - 100) + " % , distance blocks per level : " + (beforeDistance * 100 / distblocks - 100) + " %, force blocks per level : " + (beforeForce * 100 / forceblocks - 100) + " %");
            writer.WriteLine("Decrement: Blocks per level : " + (beforeBlocks * 100 / biter - 100) + " % , distance blocks per level : " + (beforeDistance * 100 / distblocks - 100) + " %, force blocks per level : " + (beforeForce * 100 / forceblocks - 100) + " %");
        }
        
        totalForce += forceblocks;
        totalForceRamp += forceblocksramp;
        totalLength += distblocks;
        totalLengthRamp += distblocksramp;

        float minBaseSize = blockwide * holeWide * 2;
        if (Method4Ramp && minBaseSize<minBaseSize4Ramps)
            minBaseSize = minBaseSize * 2;

        // do not draw ramps if the base size is too small
        if (showRamps && rampMethod != RampMethodType.Straight && base_size > minBaseSize)
        {
            if (progressBar && !Sequenced)
            {
                progressBar.Show("Drawing ramps at iteration " + (level + 1));
                yield return null;
            }

            int rampFace = 0;
            if (SingleRampFaceStart == RampPositionFace.NorthFace)
                rampFace = 2;
            else
            if (SingleRampFaceStart == RampPositionFace.EastFace)
                rampFace = 3;
            else
            if (SingleRampFaceStart == RampPositionFace.SouthFace)
                rampFace = 0;
            else
            if (SingleRampFaceStart == RampPositionFace.WestFace)
                rampFace = 1;
            // draw ramps
            if (rampMethod == RampMethodType.Spiral)
            {
                if ((Method4Ramp || Method2Ramp) && minBaseSize2Ramps < base_size)
                    Draw4Ramps(level, base_size + (blockwide + 1) * spiralRampSeparation, height, h, sep, length + blockwide * spiralRampSeparation,
                                row, last_sepi, last_length, last_h, last_v0, last_v1);
                else
                    DrawRamps(level, base_size + (blockwide + 1) * spiralRampSeparation, height, h_spiral, sep_spiral, length + blockwide * spiralRampSeparation,
                                row, last_sepi, last_length, last_h, last_v0, last_v1);
            }
            else
            if (rampMethod == RampMethodType.Internal)
                DrawRamps(level, base_size - (blockwide + 1) * spiralRampSeparation, height, h_spiral, sep_spiral, length - blockwide * spiralRampSeparation,
                        row, last_sepi, last_length, last_h, last_v0, last_v1);
            else
            if ((Method4Ramp || Method2Ramp) && minBaseSize2Ramps < base_size)
            {
                // 2-ramps for minimum size - ramps on opposite sides
                if (Method4Ramp && minBaseSize4Ramps > base_size)
                {
                    DrawRamps(level + rampFace, base_size - 2 * blockwide, height, h, sep, length - blockwide,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
                    DrawRamps(level + (rampFace + 2) % 4, base_size - 2 * blockwide, height, h, sep, length - blockwide,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
                }
                // 2-4-ramps
                else
                    if (MethodInsideRamp)
                        Draw4Ramps(level, base_size - 2 * blockwide, height, h, sep, length - blockwide,
                                    row, last_sepi, last_length, last_h, last_v0, last_v1);
                    else
                        Draw4Ramps(level, base_size, height, h, sep, length,
                                row, last_sepi, last_length, last_h, last_v0, last_v1);
            }
            else
            {
                if (MethodInsideRamp)
                    DrawRamps(level + rampFace, base_size - 2 * blockwide, height, h, sep, length - blockwide,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
                else
                    DrawRamps(level + rampFace, base_size, height, h, sep, length,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
            }
        }        

        // show granite block King's Chamber
        if (rampMethod == RampMethodType.Integrated && DrawGranite && DrawUntilRow && (row + 1 > DrawRow) && !exportPyramidObj && !isRigidBody)
        {
            GameObject granite_gameObject = new GameObject();
            granite_gameObject.name = "Terrace_" + level+"_"+ row;
            granite_gameObject.transform.parent = objParent.transform;
            granite_gameObject.isStatic = isStatic;
            if (Sequenced)
                granite_gameObject = objParent;

            if ((heightGranite < maxHeightGraniteRock) && (heightGranite > 0))
            {
                int numOfGraniteRock1Def = numOfGraniteRock1;
                int numOfGraniteRock2Def = numOfGraniteRock2;
                if (heightGranite > minHeightGraniteRock)
                {
                    numOfGraniteRock1Def = (int)UnityEngine.Random.Range((maxHeightGraniteRock - heightGranite) * numOfGraniteRock1 / maxHeightGraniteRock, numOfGraniteRock1);
                    numOfGraniteRock2Def = (int)UnityEngine.Random.Range((maxHeightGraniteRock - heightGranite) * numOfGraniteRock2 / maxHeightGraniteRock, numOfGraniteRock2);
                }

                if (numOfGraniteRock1Def > 0 && graniteRockPrefab1)
                {
                    for (int i = 0; i < numOfGraniteRock1Def; i++)
                    {
                        GameObject objGranite = null;
                        // Try to find a non-overlapping position
                        Vector3 spawnPos = Vector3.zero;
                        bool positionFound = false;
                        int attempts = 0;
                        int maxAttempts = 30; // maximum attempts to find a position

                        // Dimensions for OverlapBox
                        //Vector3 halfExtents = graniteRockPrefab1.transform.localScale / 2f * 0.9f;
                        Vector3 halfExtents = new Vector3(1.3f,1.3f,4.0f) / 2f * 0.9f;  // size 10 t block

                        while (!positionFound && attempts < maxAttempts)
                        {
                            spawnPos = GetRandomPositionOnTerrace(row, new_base_size, heightGranite, graniteRockPrefab1.transform.localScale.y);

                            Collider[] colliders = Physics.OverlapBox(spawnPos, halfExtents, Quaternion.identity, blockLayer);

                            if (colliders.Length == 0)
                            {
                                positionFound = true;
                            }
                            attempts++;
                        }

                        // if no position found after max attempts, use the last calculated position (it will overlap, but better than not placing it)
                        objGranite = Instantiate(graniteRockPrefab1, spawnPos, Quaternion.identity);
                        objGranite.transform.parent = granite_gameObject.transform;
                    }
                }
                if (numOfGraniteRock2Def > 0 && graniteRockPrefab2)
                {
                    for (int i = 0; i < numOfGraniteRock2Def; i++)
                    {
                        GameObject objGranite = null;
                        // Try to find a non-overlapping position
                        Vector3 spawnPos = Vector3.zero;
                        bool positionFound = false;
                        int attempts = 0;
                        int maxAttempts = 30; // maximum attempts to find a position

                        // Dimensions for OverlapBox
                        //Vector3 halfExtents = graniteRockPrefab2.transform.localScale / 2f * 0.9f;
                        Vector3 halfExtents = new Vector3(1.3f, 1.8f, 8.0f) / 2f * 0.9f;  // size 70 t block

                        while (!positionFound && attempts < maxAttempts)
                        {
                            spawnPos = GetRandomPositionOnTerrace(row, new_base_size, heightGranite, graniteRockPrefab2.transform.localScale.y);

                            Collider[] colliders = Physics.OverlapBox(spawnPos, halfExtents, Quaternion.identity, blockLayer);

                            if (colliders.Length == 0)
                            {
                                positionFound = true;
                            }
                            attempts++;
                        }

                        // if no position found after max attempts, use the last calculated position (it will overlap, but better than not placing it)
                        objGranite = Instantiate(graniteRockPrefab2, spawnPos, Quaternion.identity);
                        objGranite.transform.parent = granite_gameObject.transform;
                    }
                }
            }
            // limestone meghalitic blocks
            if ((heightGranite < maxHeightGraniteRock2) && (heightGranite > 0))
            {
                int numOfGraniteRock3Def = numOfGraniteRock3;
                if (heightGranite > minHeightGraniteRock)
                {
                    numOfGraniteRock3Def = (int)UnityEngine.Random.Range((maxHeightGraniteRock2 - heightGranite) * numOfGraniteRock3 / maxHeightGraniteRock2, numOfGraniteRock3);
                }

                if (numOfGraniteRock3Def > 0 && graniteRockPrefab3)
                {
                    for (int i = 0; i < numOfGraniteRock3Def; i++)
                    {
                        GameObject objGranite = null;
                        // Try to find a non-overlapping position
                        Vector3 spawnPos = Vector3.zero;
                        bool positionFound = false;
                        int attempts = 0;
                        int maxAttempts = 30; // maximum attempts to find a position

                        // Dimensions for OverlapBox
                        //Vector3 halfExtents = graniteRockPrefab3.transform.localScale / 2f * 0.9f;
                        Vector3 halfExtents = new Vector3(1.3f, 1.3f, 7.0f) / 2f * 0.9f;  // size 50 t block

                        while (!positionFound && attempts < maxAttempts)
                        {
                            spawnPos = GetRandomPositionOnTerrace(row, new_base_size, heightGranite, graniteRockPrefab3.transform.localScale.y);

                            Collider[] colliders = Physics.OverlapBox(spawnPos, halfExtents, Quaternion.identity, blockLayer);

                            if (colliders.Length == 0)
                            {
                                positionFound = true;
                            }
                            attempts++;
                        }

                        // if no position found after max attempts, use the last calculated position (it will overlap, but better than not placing it)
                        objGranite = Instantiate(graniteRockPrefab3, spawnPos, Quaternion.identity);
                        objGranite.transform.parent = granite_gameObject.transform;
                    }
                }
            }

            if (piramidon)
            {               
                if (row % 2 == 0)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4), heightGranite + 1, UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }
                else
                if (row % 2 == 1)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4), heightGranite + 1, -UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }
            }   

            // setup ramps course
            if (courseRampPrefab)
            {
                currentCourseHeight = blockheight;
                // uses thickness from Khufu courses
                if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
                    currentCourseHeight = GetBlockHeightForRow(DrawRow + 1);                                    

                Vector3 DesiredSize = new Vector3(horizontalTransferDistanceMeters, currentCourseHeight, 3.0f);

                Debug.Log(" capstanOperators10t: " + capstanOperators10t +
                          " capstanOperators40t: " + capstanOperators40t +
                          " capstanOperators50t: " + capstanOperators50t +
                          " capstanOperators60t: " + capstanOperators60t +
                          " capstanOperators70t: " + capstanOperators70t +
                          " capstanOperators80t: " + capstanOperators80t);

                int capstanOperators = (capstanOperators10t + capstanOperators40t + capstanOperators50t + capstanOperators60t + capstanOperators70t + capstanOperators80t) / 6;
                if (DrawGranitePullers == DrawGranitePullers.pullers10t) capstanOperators = capstanOperators10t;
                if (DrawGranitePullers == DrawGranitePullers.pullers40t) capstanOperators = capstanOperators40t;
                if (DrawGranitePullers == DrawGranitePullers.pullers50t) capstanOperators = capstanOperators50t;
                if (DrawGranitePullers == DrawGranitePullers.pullers60t) capstanOperators = capstanOperators60t;
                if (DrawGranitePullers == DrawGranitePullers.pullers70t) capstanOperators = capstanOperators70t;
                if (DrawGranitePullers == DrawGranitePullers.pullers80t) capstanOperators = capstanOperators80t;

                Debug.Log(" totalPullers10t: " + totalPullers10t +
                          " totalPullers40t: " + totalPullers40t +
                          " totalPullers50t: " + totalPullers50t +
                          " totalPullers60t: " + totalPullers60t +
                          " totalPullers70t: " + totalPullers70t +
                          " totalPullers80t: " + totalPullers80t);

                int totalPullers = (totalPullers10t + totalPullers40t + totalPullers50t + totalPullers60t + totalPullers70t + totalPullers80t) / 6;
                if (DrawGranitePullers == DrawGranitePullers.pullers10t) totalPullers = totalPullers10t;
                if (DrawGranitePullers == DrawGranitePullers.pullers40t) totalPullers = totalPullers40t;
                if (DrawGranitePullers == DrawGranitePullers.pullers50t) totalPullers = totalPullers50t;
                if (DrawGranitePullers == DrawGranitePullers.pullers60t) totalPullers = totalPullers60t;
                if (DrawGranitePullers == DrawGranitePullers.pullers70t) totalPullers = totalPullers70t;
                if (DrawGranitePullers == DrawGranitePullers.pullers80t) totalPullers = totalPullers80t;

                // draw half setup ramps
                for (int i = 0; i < setupTimePerCourseGroups / 2; i++)
                {

                    if (row % 2 == 0)
                    {
                        GameObject objSetupRamp = Instantiate(courseRampPrefab, objParent.transform.position + new Vector3(horizontalTransferDistanceMeters / 2, heightGranite + 0.3f, -UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4)), Quaternion.identity);
                        objSetupRamp.transform.parent = granite_gameObject.transform;
                        objSetupRamp.transform.localScale = DesiredSize;
                        objSetupRamp.transform.Rotate(0, 0, -mezzanineRampAngleDegrees, Space.World);
                        // bollards
                        GameObject woodencyl1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        woodencyl1.name = "Course_Ramp_wooden_cylinder_" + level + "_" + row + "_1";
                        woodencyl1.transform.position = objSetupRamp.transform.position + new Vector3(horizontalTransferDistanceMeters/2 + 0.5f, 1.0f, 1.75f);
                        //woodencyl1.transform.localRotation = Quaternion.Euler(130.0f, -14.0f, 23.0f);
                        woodencyl1.transform.localScale = new Vector3(0.3f, 2.0f, 0.3f);
                        woodencyl1.transform.parent = granite_gameObject.transform;
                        woodencyl1.GetComponent<MeshRenderer>().material = m_Material_wood;
                        woodencyl1.isStatic = true;
                        GameObject woodencyl2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        woodencyl2.name = "Course_Ramp_wooden_cylinder_" + level + "_" + row + "_2";
                        woodencyl2.transform.position = objSetupRamp.transform.position + new Vector3(horizontalTransferDistanceMeters / 2 + 0.5f, 1.0f, -1.75f);
                        //woodencyl2.transform.localRotation = Quaternion.Euler(130.0f, -14.0f, 23.0f);
                        woodencyl2.transform.localScale = new Vector3(0.3f, 2.0f, 0.3f);
                        woodencyl2.transform.parent = granite_gameObject.transform;
                        woodencyl2.GetComponent<MeshRenderer>().material = m_Material_wood;
                        woodencyl2.isStatic = true;

                        // draw egyptian only in the first ramp
                        if (DrawEgyptians && Egyptian_body && i == 0)
                        {
                            GameObject workers_gameObject = new GameObject();
                            workers_gameObject.name = "Team_Granite_" + level;
                            workers_gameObject.transform.parent = granite_gameObject.transform;
                            workers_gameObject.isStatic = true;
                            if (Sequenced)
                                workers_gameObject = objParent;

                            // draw capstan Operators
                            for (int j = 0; j < capstanOperators / 4; j++)
                            {
                                // left hand
                                GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 1.0f, 0), Quaternion.identity);
                                Egyptian.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + "_" + j +"_1";
                                Egyptian.transform.position = woodencyl1.transform.position + new Vector3(0.5f, 0, 1.75f + j * 1.0f);
                                //Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);
                                Egyptian.transform.parent = workers_gameObject.transform;
                                Egyptian.isStatic = true;
                                // right hand
                                GameObject Egyptian2 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 1.0f, 0), Quaternion.identity);
                                Egyptian2.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + j + "_2";
                                Egyptian2.transform.position = woodencyl1.transform.position + new Vector3(1.5f, 0, 1.75f + j * 1.0f);
                                //Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);
                                Egyptian2.transform.parent = workers_gameObject.transform;
                                Egyptian2.isStatic = true;

                                // left hand
                                GameObject Egyptian3 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian3.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + "_" + j + "_3";
                                Egyptian3.transform.position = woodencyl2.transform.position + new Vector3(0.5f, 1.0f, -(1.75f + j * 1.0f));
                                Egyptian3.transform.localRotation = Quaternion.Euler(0.0f, 180f, 0.0f);
                                Egyptian3.transform.parent = workers_gameObject.transform;
                                Egyptian3.isStatic = true;
                                // right hand
                                GameObject Egyptian4 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian4.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + "_" + j + "_4";
                                Egyptian4.transform.position = woodencyl2.transform.position + new Vector3(1.5f, 1.0f, -(1.75f + j * 1.0f));
                                Egyptian4.transform.localRotation = Quaternion.Euler(0.0f, 180f, 0.0f);
                                Egyptian4.transform.parent = workers_gameObject.transform;
                                Egyptian4.isStatic = true;
                            }

                            // draw Pullers
                            int numPullersXRope = totalPullers / NumberOfRopesGroups;
                            for (int j = 0; j < NumberOfRopesGroups; j++)
                            {
                                for (int k = 0; k < numPullersXRope; k++)
                                {
                                    GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                    Egyptian.name = "Egyptian_granite_puller_" + level + "_" + row + "_" + i + "_" + j + "_" + k;
                                    Egyptian.transform.position = (woodencyl1.transform.position + woodencyl2.transform.position) / 2 + new Vector3(5.0f + k * 1.0f, 1.0f, (NumberOfRopesGroups / 2 - j) * 1.5f);
                                    Egyptian.transform.localRotation = Quaternion.Euler(0, 270f, 0.0f);
                                    Egyptian.transform.parent = workers_gameObject.transform;
                                    Egyptian.isStatic = true;
                                }
                            }
                        }
                    }
                    else
                    if (row % 2 == 1)
                    {
                        GameObject objSetupRamp = Instantiate(courseRampPrefab, objParent.transform.position + new Vector3(horizontalTransferDistanceMeters / 2, heightGranite + 0.3f, -UnityEngine.Random.Range(horizontalTransferDistanceMeters, new_base_size / 4)), Quaternion.identity);
                        objSetupRamp.transform.parent = granite_gameObject.transform;
                        objSetupRamp.transform.localScale = DesiredSize;
                        objSetupRamp.transform.Rotate(0, 0, -mezzanineRampAngleDegrees, Space.World);
                        // bollards
                        GameObject woodencyl1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        woodencyl1.name = "Course_Ramp_wooden_cylinder_" + level + "_" + row + "_1";
                        woodencyl1.transform.position = objSetupRamp.transform.position + new Vector3(-horizontalTransferDistanceMeters / 2 - 0.5f, 1.0f, 1.75f);
                        //woodencyl1.transform.localRotation = Quaternion.Euler(130.0f, -14.0f, 23.0f);
                        woodencyl1.transform.localScale = new Vector3(0.3f, 2.0f, 0.3f);
                        woodencyl1.transform.parent = granite_gameObject.transform;
                        woodencyl1.GetComponent<MeshRenderer>().material = m_Material_wood;
                        woodencyl1.isStatic = true;
                        GameObject woodencyl2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        woodencyl2.name = "Course_Ramp_wooden_cylinder_" + level + "_" + row + "_2";
                        woodencyl2.transform.position = objSetupRamp.transform.position + new Vector3(-horizontalTransferDistanceMeters / 2 - 0.5f, 1.0f, -1.75f);
                        //woodencyl2.transform.localRotation = Quaternion.Euler(130.0f, -14.0f, 23.0f);
                        woodencyl2.transform.localScale = new Vector3(0.3f, 2.0f, 0.3f);
                        woodencyl2.transform.parent = granite_gameObject.transform;
                        woodencyl2.GetComponent<MeshRenderer>().material = m_Material_wood;
                        woodencyl2.isStatic = true;

                        // draw egyptian only in the first ramp
                        if (DrawEgyptians && Egyptian_body && i == 0)
                        {
                            GameObject workers_gameObject = new GameObject();
                            workers_gameObject.name = "Team_Granite_" + level;
                            workers_gameObject.transform.parent = granite_gameObject.transform;
                            workers_gameObject.isStatic = true;
                            if (Sequenced)
                                workers_gameObject = objParent;

                            // draw capstan Operators
                            for (int j = 0; j < capstanOperators / 6; j++)
                            {
                                // left hand
                                GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + "_" + j + "_1";
                                Egyptian.transform.position = woodencyl1.transform.position + new Vector3(-0.5f, 0, 1.75f + j * 1.0f);
                                Egyptian.transform.parent = workers_gameObject.transform;
                                Egyptian.isStatic = true;
                                // right hand
                                GameObject Egyptian2 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian2.name = "Egyptian_granite_right_" + level + "_" + row + "_" + i + "_" + j + "_2";
                                Egyptian2.transform.position = woodencyl1.transform.position + new Vector3(-1.5f, 0, 1.75f + j * 1.0f);
                                Egyptian2.transform.parent = workers_gameObject.transform;
                                Egyptian2.isStatic = true;
                                // right hand 2
                                GameObject Egyptian6 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian6.name = "Egyptian_granite_right_" + level + "_" + row + "_" + i + "_" + j + "_3";
                                Egyptian6.transform.position = woodencyl1.transform.position + new Vector3(-2.5f, 0, 1.75f + j * 1.0f);
                                Egyptian6.transform.parent = workers_gameObject.transform;
                                Egyptian6.isStatic = true;

                                // left hand
                                GameObject Egyptian3 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian3.name = "Egyptian_granite_left_" + level + "_" + row + "_" + i + "_" + j + "_3";
                                Egyptian3.transform.position = woodencyl2.transform.position + new Vector3(-0.5f, 0, -(1.75f + j * 1.0f));
                                Egyptian3.transform.localRotation = Quaternion.Euler(0.0f, 180f, 0.0f);
                                Egyptian3.transform.parent = workers_gameObject.transform;
                                Egyptian3.isStatic = true;
                                // right hand
                                GameObject Egyptian4 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian4.name = "Egyptian_granite_right_" + level + "_" + row + "_" + i + "_" + j + "_4";
                                Egyptian4.transform.position = woodencyl2.transform.position + new Vector3(-1.5f, 0, -(1.75f + j * 1.0f));
                                Egyptian4.transform.localRotation = Quaternion.Euler(0.0f, 180f, 0.0f);
                                Egyptian4.transform.parent = workers_gameObject.transform;
                                Egyptian4.isStatic = true;
                                // right hand 2
                                GameObject Egyptian5 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                Egyptian5.name = "Egyptian_granite_right_" + level + "_" + row + "_" + i + "_" + j + "_5";
                                Egyptian5.transform.position = woodencyl2.transform.position + new Vector3(-2.5f, 0, -(1.75f + j * 1.0f));
                                Egyptian5.transform.localRotation = Quaternion.Euler(0.0f, 180f, 0.0f);
                                Egyptian5.transform.parent = workers_gameObject.transform;
                                Egyptian5.isStatic = true;
                            }

                            // draw Pullers
                            int numPullersXRope = totalPullers / NumberOfRopesGroups;
                            for (int j = 0; j < NumberOfRopesGroups; j++)
                            {
                                for (int k = 0; k < numPullersXRope; k++)
                                {
                                    GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                                    Egyptian.name = "Egyptian_granite_puller_" + level + "_" + row + "_" + i + "_" + j + "_" + k;
                                    Egyptian.transform.position = (woodencyl1.transform.position + woodencyl2.transform.position) / 2 + new Vector3(- 5.0f - k * 1.0f, 0, (-NumberOfRopesGroups / 2 + j)*1.5f);
                                    Egyptian.transform.localRotation = Quaternion.Euler(0, 270f, 0.0f);
                                    Egyptian.transform.parent = workers_gameObject.transform;
                                    Egyptian.isStatic = true;
                                }
                            }
                        }
                    }
                }
            }

            // Draw Half Course
            if (DrawHalfCourseForGraniteBlocks && DrawRow > 0)
            {
                currentCourseHeight = blockheight;
                // uses thickness from Khufu courses
                if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
                {
                    currentCourseHeight = GetBlockHeightForRow(DrawRow + 1);
                    bh2 = currentCourseHeight / 2;
                    bht2 = bh2 / pyramid_inclination_tg;
                    bhtl = Mathf.Sqrt(currentCourseHeight * currentCourseHeight + bht2 * bht2) + 0.3f;
                    setbackWide = currentCourseHeight / Mathf.Tan(degrees_to_radians(PyramidInclination));
                }

                GameObject row_gameObject = new GameObject();
                row_gameObject.name = "Course_Granite_" + level + "_" + (DrawRow + 1);
                row_gameObject.transform.parent = iter_gameObject.transform;
                row_gameObject.isStatic = isStatic;

                float sepi = sep * (DrawRow - row_ori + 1) / ch;
                int bxi = 0;
                float totalHeightUpToCurrentCourse = 0;
                float totalHeightUpToCurrentCourseTotal = 0;
                if (selectedPyramid == PyramidType.Khufu && useKhufuCourseHeights)
                {
                    for (int hIndex = 0; hIndex < DrawRow - row_ori + 1; hIndex++)
                    {
                        totalHeightUpToCurrentCourse += GetBlockHeightForRow(row_ori + hIndex);
                    }
                    setbackKhufuCourseHeights += setbackWide;
                    sepi = setbackKhufuCourseHeights;
                }
                else
                    totalHeightUpToCurrentCourse = (DrawRow - row_ori + 1) * currentCourseHeight;

                last_h = totalHeightUpToCurrentCourse;
                last_v0 = v0;
                last_v1 = v1;
                numberOfBlocksX = 0;
                lastNumberOfBlockDrawnX = -1;
                x = -bs2 + sepi + bw2;
                v0 = new Vector3(bs2 - sepi, totalHeightUpToCurrentCourse, -(bs2 - sepi));
                while (x < bs2 - sepi - bw2)
                {
                    lastCubeDrawn = null;
                    numberOfBlocksX++;
                    numberOfBlocksZ = 0;
                    lastNumberOfBlockDrawnZ = -1;
                    z = -bs2 + sepi + bw2;
                    lastobj = null;
                    while (z < bs2 - sepi - bw2)
                    {
                        numberOfBlocksZ++;
                        num_block_real++;
                        obj = null;
                        if (x < 0)
                        {
                            int rnd = UnityEngine.Random.Range(0, RockPrefab.Length);
                            obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                            obj.transform.localScale = new Vector3(blockwide - blockSeparation, currentCourseHeight, blockwide - blockSeparation);
                            obj.transform.name = "Block_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                            obj.transform.parent = row_gameObject.transform;
                            obj.isStatic = isStatic || row == 0;
                            lastobj = obj;
                            lastNumberOfBlockDrawnZ = numberOfBlocksZ;
                            lastCubeDrawn = obj;
                            numberOfBlocksDrawn++;
                        }
                        z += blockwide;
                        numberOfBlocks++;
                        bxi++;
                        biter++;

                        if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                    }
                    // last block Z
                    if ((z != bs2 - sepi) && (x < 0))
                    {
                        // adapt block size
                        scaleChange = new Vector3(blockwide - blockSeparation, currentCourseHeight, blockwide - blockSeparation);
                        scaleChange.z = bs2 - sepi - (z - bw2);
                        z = z - (blockwide - scaleChange.z) / 2;
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + totalHeightUpToCurrentCourse, z), Quaternion.identity);
                        obj.transform.name = "Block_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                        obj.transform.parent = row_gameObject.transform;
                        obj.transform.localScale = scaleChange;

                        if (lastobj)
                        {
                            GameObject objnew = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)],
                                        new Vector3(lastobj.transform.position.x,
                                                    lastobj.transform.position.y,
                                                    lastobj.transform.position.z + obj.transform.localScale.z / 2),
                                        Quaternion.identity);
                            objnew.transform.name = "BlockComb_Z_" + row + "_" + numberOfBlocksX + "_" + numberOfBlocksZ;
                            objnew.transform.localScale = new Vector3(lastobj.transform.localScale.x, lastobj.transform.localScale.y, lastobj.transform.localScale.z + obj.transform.localScale.z);
                            objnew.transform.parent = row_gameObject.transform;
                            if (DrawCasing)
                                objnew.GetComponent<MeshRenderer>().material = m_Material_Blank;
                            // delete previous objects
                            Destroy(lastobj);
                            Destroy(obj);

                            lastobj = null;
                            obj = objnew;
                        }

                        obj.isStatic = isStatic || row == 0;
                        lastobj = obj;

                        numberOfBlocksDrawn++;
                        num_block_real++;
                    }
                    x += blockwide;
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }                
          
            }

        }

        force_old_length += forceblocksrow_total_total;
        force_old_horiz += forceblocksrow_horiz_total;
        force_old_vert += forceblocksrow_vert_total;

        //return length;
        if (rampMethod == RampMethodType.Spiral || rampMethod == RampMethodType.Internal)
            path_length += path_length + length_spiral;
        else
            path_length += path_length + length;

        if (maxBlocks > 0 && numberOfBlocks > maxBlocks)
            yield break;
        else
            yield return StartCoroutine(compute_size_level(level + 1, new_base_size, path_wide, separation, height + h,
                                            old_length + length, biter, distblocks, forceblocks, force_old_length, force_old_vert, force_old_horiz, row, 
                                            old_length_spiral + length_spiral));
    }

    private void draw_one_size_level(int level, float base_size, float path_wide, float separation, float height, int index)
    {
        if (height > Height)
        {
            numberOfBlocksFinish = numberOfBlocks;
            return;
        }

        //float h = base_size * ramp_inclination_tg;  // height
        float h = base_size * ramp_inclination_tg * pyramid_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        // divide by height of block
        int ch = Mathf.CeilToInt(h / blockheight);
        h = ch * blockheight; // adjust
        //float sep = h / pyramid_inclination_tg; // separation       
        float sep = base_size * ramp_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);

        if (h < 0.524f)
        {
            numberOfBlocksFinish = numberOfBlocks;
            return;
        }

        float new_base_size = base_size - 2 * path_wide - 2 * separation - 2 * sep;  // new base size

        if (new_base_size < h / 2)
        {
            numberOfBlocksFinish = numberOfBlocks;
            return;
        }
       
        float bs2 = base_size / 2;

        // at start new row delete gameobjects in the midle
        if (numberOfBlocks == index)
        {
            for (int i = 0; i < blocksMidle2.Count; i++)
                GameObject.Destroy(blocksMidle2[i]);
            blocksMidle2.Clear();
            blocksMidle2 = new List<GameObject>(blocksMidle);
            blocksMidle.Clear();            
        }

        // Draw pyramid
        //Debug.Log("CH : "+ch);
        GameObject obj;
        Vector3 scaleChange;
        float nbs2 = new_base_size / 2;
        float bw2 = blockwide / 2;
        float bh2 = blockheight / 2;
        for (int i = 0; i < ch; i++)
        {
            float sepi = sep * i / ch;
            x = -bs2 + sepi + bw2;
            while (x < bs2 - sepi - bw2)
            {
                z = -bs2 + sepi + bw2;
                while (z < bs2 - sepi - bw2)
                {
                    if (numberOfBlocks==index)
                    {
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        if (objParent)
                            obj.transform.parent = objParent.transform;
                        if (!((x < -bs2 + sepi + 2 * blockwide) || (x > bs2 - sepi - 2 * blockwide) || (z < -bs2 + sepi + 2 * blockwide) || (z > bs2 - sepi - 2 * blockwide)))
                            blocksMidle.Add(obj);
                        obj.isStatic = isStatic;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = massBlock;
                                rb.isKinematic = false;
                                rb.useGravity = true;
                            }
                        }
                        numberOfBlocksDrawn++;
                        return;
                    }
                    z += blockwide;
                    numberOfBlocks++;                    
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
                }
                // last block Z
                if (z != bs2 - sepi)
                {
                    if (numberOfBlocks == index)
                    {
                        // adapt block size
                        scaleChange = new Vector3(blockwide, blockheight, blockwide);
                        scaleChange.z = bs2 - sepi - (z - bw2);
                        z = z - (blockwide - scaleChange.z) / 2;
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        if (objParent)
                            obj.transform.parent = objParent.transform;
                        obj.transform.localScale = scaleChange;
                        obj.isStatic = isStatic;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = massBlock * scaleChange.z / blockwide;
                                rb.isKinematic = false;
                                rb.useGravity = true;
                            }
                        }
                        if (!((x < -bs2 + sepi + 2 * blockwide) || (x > bs2 - sepi - 2 * blockwide) || (z < -bs2 + sepi + 2 * blockwide) || (z > bs2 - sepi - 2 * blockwide)))
                            blocksMidle.Add(obj);
                        numberOfBlocksDrawn++;
                        return;
                    }
                    numberOfBlocks++;                    
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
                }
                x += blockwide;
                if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
            }
            // last block X
            if (x != bs2 - sepi)
            {
                // adapt block size
                scaleChange = new Vector3(blockwide, blockheight, blockwide);
                scaleChange.x = bs2 - sepi - (x - bw2);
                x = x - (blockwide - scaleChange.x) / 2;
                z = -bs2 + sepi + bw2;
                while (z < bs2 - sepi - bw2)
                {
                    if (numberOfBlocks == index)
                    {
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        if (objParent)
                            obj.transform.parent = objParent.transform;
                        obj.transform.localScale = scaleChange;
                        obj.isStatic = isStatic;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = massBlock * scaleChange.x / blockwide;
                                rb.isKinematic = false;
                                rb.useGravity = true;
                            }
                        }
                        if (!((x < -bs2 + sepi + 2 * blockwide) || (x > bs2 - sepi - 2 * blockwide) || (z < -bs2 + sepi + 2 * blockwide) || (z > bs2 - sepi - 2 * blockwide)))
                            blocksMidle.Add(obj);
                        numberOfBlocksDrawn++;
                        return;
                    }
                    z += blockwide;
                    numberOfBlocks++;                    
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
                }
                // last block Z
                if (z != bs2 - sepi)
                {
                    // adapt block size
                    if (numberOfBlocks == index)
                    {
                        scaleChange.z = bs2 - sepi - (z - bw2);
                        z = z - (blockwide - scaleChange.z) / 2;
                        /*if (i == 0)
                            obj = Instantiate(RockDivPrefab, objParent.transform.position +new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        else*/
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                        if (objParent)
                            obj.transform.parent = objParent.transform;
                        obj.transform.localScale = scaleChange;
                        obj.isStatic = isStatic;
                        if (isRigidBody)
                        {
                            Rigidbody rb = obj.GetComponent<Rigidbody>();
                            if (rb)
                            {
                                rb.mass = massBlock * scaleChange.z / blockwide;
                                rb.isKinematic = false;
                                rb.useGravity = true;
                            }
                        }
                        if (!((x < -bs2 + sepi + 2 * blockwide) || (x > bs2 - sepi - 2 * blockwide) || (z < -bs2 + sepi + 2 * blockwide) || (z > bs2 - sepi - 2 * blockwide)))
                            blocksMidle.Add(obj);
                        numberOfBlocksDrawn++;
                        return;
                    }
                    numberOfBlocks++;                    
                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
                }
                x += blockwide;
                if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;
            }
            if (maxBlocks > 0 && numberOfBlocks > maxBlocks) return;            
        }
        lastLevelBlocks = numberOfBlocks;
        lastLevel = level;
        draw_one_size_level(level + 1, new_base_size, path_wide, separation, height + h, index);
        return;
    }

    private void DrawRamps(int level, float base_size, float height, float h, float sep, float length,
                            int row, float last_sepi, float last_length, float last_h, Vector3 last_v0, Vector3 last_v1)
    {
        float bh2 = blockheight / 2;
        if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
        {
            if (level % 4 == 1)
            {
                last_v0 = Quaternion.Euler(0, 90f, 0) * last_v0;
                last_v1 = Quaternion.Euler(0, 90f, 0) * last_v1;
            }
            else
            if (level % 4 == 2)
            {
                last_v0 = Quaternion.Euler(0, 180f, 0) * last_v0;
                last_v1 = Quaternion.Euler(0, 180f, 0) * last_v1;
            }
            else
            if (level % 4 == 3)
            {
                last_v0 = Quaternion.Euler(0, 270f, 0) * last_v0;
                last_v1 = Quaternion.Euler(0, 270f, 0) * last_v1;
            }
        }

        GameObject iter_gameObject = GameObject.Find("Iter_" + level);
        if (iter_gameObject == null)
            iter_gameObject = objParent;
        GameObject ramp_gameObject = new GameObject();
        ramp_gameObject.name = "GroupRamp_" + level;
        ramp_gameObject.transform.parent = iter_gameObject.transform;
        ramp_gameObject.isStatic = true;
        if (Sequenced)
            ramp_gameObject = objParent;

        // ramp        
        float a1 = Mathf.Atan(sep / (base_size - sep));
        float a2 = Mathf.Atan(h / (base_size - sep));

        if (rampMethod == RampMethodType.Integrated)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Ramp_" + level + "_" + row;
            if (level % 4 == 0)
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide - 2) * blockwide / 2, height + blockheight * holeHeight / 2, 0);
                else
                    cube.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide - 2) * blockwide / 2, h / 2 + height + blockheight * holeHeight / 2, sep / 2);
                cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            }
            else
            if (level % 4 == 1)
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, (holeWide - 2) * blockwide / 2);
                else
                    cube.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight / 2, -(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2);
                cube.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 2)
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3((holeWide - 2) * blockwide / 2, height + blockheight * holeHeight / 2, 0);
                else
                    cube.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
                cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 3)
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, -(holeWide - 2) * blockwide / 2);
                else
                    cube.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight / 2, (base_size - sep) / 2 - (holeWide - 2) * blockwide / 2);
                cube.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            }
            if (MethodInsideRamp)
            {
                if (level % 4 == 0)
                    cube.transform.position += Vector3.left * blockwide;
                else
                if (level % 4 == 1)
                    cube.transform.position += new Vector3(0, 0, blockwide);
                else
                if (level % 4 == 2)
                    cube.transform.position -= Vector3.left * blockwide;
                else
                if (level % 4 == 3)
                    cube.transform.position -= new Vector3(0, 0, blockwide);

                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2 - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
                else
                    cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2, blockheight * holeHeight, blockwide * holeWide);
                else
                    cube.transform.localScale = new Vector3(length, blockheight * holeHeight, blockwide * holeWide);
            }

            // ommitted blocks for integrated ramp
            cube.transform.parent = ramp_gameObject.transform;
            cube.isStatic = true;        
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().generatePyramid = this;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
            cube.GetComponent<DeleteObject>().mainRamp = true;
            cube.GetComponent<MeshRenderer>().enabled = false;            
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
        }

        // ramp corner wide
        float base_size_ant = base_size;
        // not remove blocks in the first level
        if (level > 0 && (rampMethod == RampMethodType.Integrated || rampMethod == RampMethodType.Internal))
        {
            if (rampMethod == RampMethodType.Internal)
                 base_size += (blockwide + 1) * spiralRampSeparation;            
            GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            float holeDiag = MathF.Sqrt(((holeWide + 1) * blockwide) * ((holeWide + 1) * blockwide));            
            cube_c.name = "Ramp-corner_" + level + "_" + row;
            if (level % 4 == 0)
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - holeDiag / 2);
            else
            if (level % 4 == 1)
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + holeDiag / 2);
            else
            if (level % 4 == 2)
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + holeDiag / 2);
            else
            if (level % 4 == 3)
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - holeDiag / 2);
            cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
            //cube_c.transform.parent = objParent.transform;
            cube_c.transform.parent = ramp_gameObject.transform;
            cube_c.isStatic = true;
            cube_c.AddComponent<DeleteObject>();
            cube_c.GetComponent<DeleteObject>().generatePyramid = this;
            cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube_c.GetComponent<DeleteObject>().deleteObject = !Decomisioning;            
            cube_c.GetComponent<MeshRenderer>().enabled = false;
            cube_c.GetComponent<BoxCollider>().isTrigger = true;
        }
        base_size = base_size_ant;

        // ramp floor
        if (DrawFloor)
        {
            GameObject cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "Ramp_floor_" + level + "_" + row;
            cubefloor.tag = "Ramp";
            if (level % 4 == 0)
            {
                if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide-2) * blockwide / 2, height - bh2, 0);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide - 2) * blockwide/2, h / 2 + height - bh2, sep / 2);
                cubefloor.transform.position -= new Vector3(0.7f, 0, 0);
                if (setback)
                    cubefloor.transform.position += new Vector3(setbackWide / 2, 0, 0);
                if (MethodInsideRamp)
                    cubefloor.transform.position -= new Vector3(blockwide, 0, 0);
                cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            }
            else
            if (level % 4 == 1)
            {
                if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height - bh2, (holeWide - 2) * blockwide / 2);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2);
                cubefloor.transform.position -= new Vector3(0, 0, -0.7f);
                if (setback)
                    cubefloor.transform.position += new Vector3(0, 0, -setbackWide / 2);
                if (MethodInsideRamp)
                    cubefloor.transform.position -= new Vector3(0, 0, -blockwide);
                cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 2)
            {
                if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3((holeWide - 2) * blockwide / 2, height - bh2, 0);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2, h / 2 + height - bh2, -sep / 2);
                cubefloor.transform.position -= new Vector3(-0.7f, 0, 0);
                if (setback)
                    cubefloor.transform.position += new Vector3(-setbackWide / 2, 0, 0);
                if (MethodInsideRamp)
                    cubefloor.transform.position -= new Vector3(-blockwide, 0, 0);
                cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 3)
            {
                if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height - bh2, -(holeWide - 2) * blockwide / 2);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - (holeWide - 2) * blockwide / 2);
                cubefloor.transform.position -= new Vector3(0, 0, 0.7f);
                if (setback)
                    cubefloor.transform.position += new Vector3(0, 0, setbackWide / 2);
                if (MethodInsideRamp)
                    cubefloor.transform.position -= new Vector3(0, 0, blockwide);
                cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            }
            if (DrawUntilRow && row > DrawRow && rampMethod == RampMethodType.Integrated)
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
            }
            else
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Spiral)
                    cubefloor.transform.localScale = new Vector3(length + (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Internal)
                    cubefloor.transform.localScale = new Vector3(length - (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
            }
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;

            if (rampMethod == RampMethodType.Spiral)
                DrawSteppedEmbankment(cubefloor,false);            
        }        

        if (DrawWall && rampMethod == RampMethodType.Integrated)
        {
            // ramp wall
            GameObject cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "Ramp_wall_" + level + "_" + row;
            cubewall.tag = "Ramp";

            if (level % 4 == 0)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide - 1) * blockwide, height + blockheight * holeHeight/2, 0);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide-1) * blockwide, h / 2 + height + blockheight * holeHeight/2, sep / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            }
            else
            if (level % 4 == 1)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, (holeWide - 1) * blockwide);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight / 2, -(base_size - sep) / 2 + (holeWide - 1) * blockwide);
                cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 2)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3((holeWide - 1) * blockwide, height + blockheight * holeHeight / 2, 0);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 1) * blockwide, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 3)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, -(holeWide - 1) * blockwide);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight / 2, (base_size - sep) / 2 - (holeWide - 1) * blockwide);
                cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            }
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = objParent.transform.position + new Vector3(last_length - (holeWide+2) * blockwide * 2, blockheight * (holeHeight+2), 0.1f);
                else
                    cubewall.transform.localScale = objParent.transform.position + new Vector3(length - (holeWide+2) * blockwide * 2, blockheight * (holeHeight+2), 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight , 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
            }
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
        }

        // corner floor
        base_size_ant = base_size;
        if (DrawFloor && (rampMethod == RampMethodType.Integrated || rampMethod == RampMethodType.Internal))
        {
            if (rampMethod == RampMethodType.Internal)
                base_size += (blockwide + 1) * spiralRampSeparation;
            GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner.name = "Ramp_corner_" + level + "_" + row;
            cubecorner.tag = "Ramp";
            float holeDiag = MathF.Sqrt((holeWide * blockwide) * (holeWide * blockwide));
            if (level % 4 == 0)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeDiag), height, (base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(setbackWide, 0, setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, -27.0f, -32.0f);
            }
            else
            if (level % 4 == 1)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeDiag), height, -(base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(setbackWide, 0, -setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, 53.0f, 48.0f);
            }
            else
            if (level % 4 == 2)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeDiag), height, -(base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(-setbackWide, 0, -setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, -42.0f, -48.0f);
            }
            else
            if (level % 4 == 3)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeDiag), height, (base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(-setbackWide, 0, setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, 32.0f, 27.0f);
            }
            if (setback)
                cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2 + setbackWide, holeWide * blockwide * 2 + setbackWide, 0.1f);
            else
                cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
            //cubecorner.transform.parent = objParent.transform;
            cubecorner.transform.parent = ramp_gameObject.transform;
            cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner.isStatic = true;
        }
        base_size = base_size_ant;

        // corner wall
        if (DrawWall && rampMethod == RampMethodType.Integrated)
        {
            GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row;
            cubecorner_wall.tag = "Ramp";
            float holeDiag = MathF.Sqrt(((holeWide+1) * blockwide) * ((holeWide + 1) * blockwide));
            if (level % 4 == 0)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag, height + blockheight * holeHeight/2, (base_size / 2) - holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, 60.0f, 0.0f);
            }
            else
            if (level % 4 == 1)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag, height + blockheight * holeHeight/2, -(base_size / 2) + holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, -30.0f, 0.0f);
            }
            else
            if (level % 4 == 2)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag, height + blockheight * holeHeight/2, -(base_size / 2) + holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, 60.0f, 0.0f);
            }
            else
            if (level % 4 == 3)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag, height + blockheight * holeHeight/2, (base_size / 2) - holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, -30.0f, 0.0f);
            }
            cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide / 2, blockheight * holeHeight, 0.1f);
            //cubecorner_wall.transform.parent = objParent.transform;
            cubecorner_wall.transform.parent = ramp_gameObject.transform;
            cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner_wall.isStatic = true;
        }

        // wooden cylinder
        if (DrawWoodenCyl && !exportPyramidObj && rampMethod == RampMethodType.Integrated)
        {
            float angleInRadians = PyramidInclination * (MathF.PI / 180.0f);
            float baseWidth = (holeWide + 1.0f) * blockwide;
            float height_wood = baseWidth * MathF.Tan(angleInRadians);
            float hypotenuse_wood = baseWidth / MathF.Cos(angleInRadians) * 2 / 3;

            GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row;
            if (level % 4 == 0)
            {
                woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(-7.0f, -35.0f, 45.0f);

            }
            else
            if (level % 4 == 1)
            {
                woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(45.0f, -35.0f, 7.0f);

            }
            else
            if (level % 4 == 2)
            {
                woodencyl.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(45.0f, 14.0f, -23.0f);

            }
            else
            if (level % 4 == 3)
            {
                woodencyl.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(130.0f, -14.0f, 23.0f);

            }
            woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
            //woodencyl.transform.parent = objParent.transform;
            woodencyl.transform.parent = ramp_gameObject.transform;
            woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
            woodencyl.isStatic = true;
        }

        GameObject workers_gameObject = new GameObject();
        workers_gameObject.name = "Team_" + level;
        workers_gameObject.transform.parent = iter_gameObject.transform;
        workers_gameObject.isStatic = true;
        if (Sequenced)
            workers_gameObject = objParent;

        // stone sled
        if (DrawEgyptians && stone_sled && height<Height*0.9f && !exportPyramidObj && rampMethod == RampMethodType.Integrated)
        {
            GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
            stone_sled1.name = "stone_sled_" + level + "_" + row;
            if (level % 4 == 0)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide / 2), height + 1.5f, (base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 90.0f+ RampInclination, RampInclination);
            }
            else
            if (level % 4 == 1)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 2.5f * blockwide), height + 1.5f, -(base_size / 2 - holeWide * blockwide / 2));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, RampInclination, -RampInclination);
            }
            else
            if (level % 4 == 2)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide / 2), height + 0.75f, -(base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 90.0f+ RampInclination, -RampInclination);
            }
            else
            if (level % 4 == 3)
            {
                stone_sled1.transform.position = new Vector3(-(base_size / 2 - 2.5f * blockwide), height + 1.5f, (base_size / 2 - holeWide * blockwide / 2));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, RampInclination, RampInclination);
            }
            //stone_sled1.transform.parent = objParent.transform;
           stone_sled1.transform.parent = workers_gameObject.transform;
           stone_sled1.isStatic = true;
           stone_sled1.transform.position = stone_sled1.transform.position + new Vector3(0, -0.8f, 0);
        }

        // egyptians
        if (DrawEgyptians && Egyptian_body && height < Height * 0.9f && !exportPyramidObj && rampMethod == RampMethodType.Integrated)
        {
            for (int i = 0; i < 12; i++)
            {
                // left hand
                GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian.name = "Egyptian_left_" + level + "_" + row+"_"+i;
                if (level % 4 == 0)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (1.25f + 0.1f*i) - holeWide * blockwide / 2), height + 3.5f + 0.16f*i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);                    
                }
                else
                if (level % 4 == 1)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, -(base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, 90.0f+ RampInclination + 180f, 0.0f);
                }
                else
                if (level % 4 == 2)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, 180.0f+ RampInclination + 180f, 0.0f);
                }
                else
                if (level % 4 == 3)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, (base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, -90f + RampInclination + 180f, 0.0f);
                }
                //Egyptian.transform.parent = objParent.transform;
                Egyptian.transform.parent = workers_gameObject.transform;
                Egyptian.isStatic = true;
                Egyptian.transform.position = Egyptian.transform.position + new Vector3(0, -2.8f, 0);
                // right hand
                GameObject Egyptian2 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i;
                if (level % 4 == 0)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0.25f, 0, 0);
                }
                else
                if (level % 4 == 1)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, -(base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, 90.0f+ RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0, 0, -0.25f);
                }
                else
                if (level % 4 == 2)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, 180.0f+ RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0, 0, 0.25f);
                }
                else
                if (level % 4 == 3)
                {
                    Egyptian2.transform.position = new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, (base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, -90f + RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(-0.25f, 0, 0);
                }
                //Egyptian2.transform.parent = objParent.transform;
                Egyptian2.transform.parent = workers_gameObject.transform;
                Egyptian2.isStatic = true;
                Egyptian2.transform.position = Egyptian2.transform.position + new Vector3(0, -2.8f, 0);
            }
        }
    }

    private void Draw4Ramps(int level, float base_size, float height, float h, float sep, float length, 
                            int row, float last_sepi, float last_length, float last_h, Vector3 last_v0, Vector3 last_v1)
    {
        GameObject iter_gameObject = GameObject.Find("Iter_" + level);
        if (iter_gameObject == null)
            iter_gameObject = objParent;
        GameObject ramp_gameObject = new GameObject();
        ramp_gameObject.name = "GroupRamp_" + level;
        ramp_gameObject.transform.parent = iter_gameObject.transform;
        ramp_gameObject.isStatic = true;
        if (Sequenced)
            ramp_gameObject = objParent;                                

        float bh2 = blockheight / 2;        
        // angle      
        float a1 = Mathf.Atan(sep / (base_size - sep));
        float a2 = Mathf.Atan(h / (base_size - sep));
        // Ramp 1
        if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_1";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide - 2) * blockwide / 2, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide - 2) * blockwide / 2, h / 2 + height + blockheight * holeHeight / 2, sep / 2);
            //    cube.transform.position = new Vector3((base_size - sep) / 2, h / 2 + height + blockheight, sep / 2);
            cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                cube.transform.position += Vector3.left * blockwide;
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2 - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
                else
                    cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2, blockheight * holeHeight, blockwide * holeWide);
                else
                    cube.transform.localScale = new Vector3(length, blockheight * holeHeight, blockwide * holeWide);
            }
            //cube.transform.localScale = new Vector3(length, blockheight * 2, 3 * blockwide);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().generatePyramid = this;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
            cube.GetComponent<DeleteObject>().mainRamp = true;
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
            cube1 = cube;
        }

        if (!Method2Ramp || level % 2 == 0)
        {
            // Ramp 2
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_2";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight/2, (holeWide - 2) * blockwide / 2);
            else
                cube.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight/2, -(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2);
            //cube.transform.position = new Vector3(sep / 2, h / 2 + height + blockheight, -(base_size - sep) / 2);
            cube.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                cube.transform.position += new Vector3(0, 0, blockwide);
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length*2 - blockwide, blockheight * (holeHeight+2), holeWide * (holeWide - 1));
                else
                    cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight+2), holeWide * (holeWide - 1));
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length*2, blockheight * holeHeight, blockwide * holeWide);
                else
                    cube.transform.localScale = new Vector3(length, blockheight * holeHeight, blockwide * holeWide);
            }
            //cube.transform.localScale = new Vector3(length, blockheight * 2, 3 * blockwide);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().generatePyramid = this;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
            cube2 = cube;
        }        

        // Ramp 3
        if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_3";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3((holeWide - 2) * blockwide / 2, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
            cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            //cube.transform.position = new Vector3(-(base_size - sep) / 2, h / 2 + height + blockheight, -sep / 2);
            cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                cube.transform.position -= Vector3.left * blockwide;
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2 - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
                else
                    cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight + 2), blockwide * (holeWide - 1));
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length * 2, blockheight * holeHeight, blockwide * holeWide);
                else
                    cube.transform.localScale = new Vector3(length, blockheight * holeHeight, blockwide * holeWide);
            }
            //cube.transform.localScale = new Vector3(length, blockheight * 2, 3 * blockwide);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().generatePyramid = this;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
            cube3 = cube;
        }

        if (!Method2Ramp || level % 2 == 0)
        {
            // Ramp 4
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_4";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, -(holeWide - 2) * blockwide / 2);
            else
                cube.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight / 2, (base_size - sep) / 2 - (holeWide - 2) * blockwide / 2);
            //cube.transform.position = new Vector3(-sep / 2, h / 2 + height + blockheight, (base_size - sep) / 2);
            cube.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                cube.transform.position -= new Vector3(0, 0, blockwide);
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length*2 - blockwide, blockheight * (holeHeight+2), blockwide * (holeWide - 1));
                else
                    cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight+2), blockwide * (holeWide - 1));
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cube.transform.localScale = new Vector3(last_length*2, blockheight * holeHeight, blockwide * holeWide);
                else
                    cube.transform.localScale = new Vector3(length, blockheight * holeHeight, blockwide * holeWide);
            }
            //cube.transform.localScale = new Vector3(length, blockheight * 2, 3 * blockwide);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().generatePyramid = this;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
            cube4 = cube;
        }        

        // ramp corner wide
        // not remove blocks in the first level
        if (level > 0)
        {
            float holeDiag = MathF.Sqrt(((holeWide + 1) * blockwide) * ((holeWide + 1) * blockwide));
            // Ramp 1
            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_1";
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - holeDiag / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<DeleteObject>().deleteObject = !Decomisioning;                
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }
            // Ramp 2
            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_2";
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + holeDiag / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }

            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_3";
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + holeDiag / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_4";
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - holeDiag / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<DeleteObject>().deleteObject = !Decomisioning;
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }
        }

        // ramp floor
        GameObject cubefloor = null;        
        if (minBaseSize4Ramps < base_size && DrawFloor && (!Method2Ramp || level % 2 == 1))
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_1";
            cubefloor.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide - 2) * blockwide / 2, height - bh2, 0);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide - 2) * blockwide / 2, h / 2 + height - bh2, sep / 2);
            cubefloor.transform.position -= new Vector3(0.7f, 0, 0);
            if (setback)
                cubefloor.transform.position += new Vector3(setbackWide / 2, 0, 0);
            if (MethodInsideRamp)
                cubefloor.transform.position -= new Vector3(blockwide, 0, 0);
            //cubefloor.transform.position = new Vector3((base_size - sep) / 2 - 1, h / 2 + height - bh2, sep / 2);
            if (DrawUntilRow && row > DrawRow)
                if (setback)
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
            else
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Spiral)
                    cubefloor.transform.localScale = new Vector3(length + (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
            }
            cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
            cubefloor1 = cubefloor;

            if (rampMethod == RampMethodType.Spiral)
                DrawSteppedEmbankment(cubefloor, false);
        }

        if (DrawFloor && (!Method2Ramp || level % 2 == 0))
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_2";
            cubefloor.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height - bh2, (holeWide - 2) * blockwide / 2);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2);
            cubefloor.transform.position -= new Vector3(0, 0, -0.7f);
            if (setback)
                cubefloor.transform.position += new Vector3(0, 0, -setbackWide / 2);
            if (MethodInsideRamp)
                cubefloor.transform.position -= new Vector3(0, 0, -blockwide);
            //cubefloor.transform.position = new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + 1);
            cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
            }
            else
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Spiral)
                    cubefloor.transform.localScale = new Vector3(length + (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
            }
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
            cubefloor2 = cubefloor;

            if (rampMethod == RampMethodType.Spiral)
                DrawSteppedEmbankment(cubefloor, false);
        }

        if (minBaseSize4Ramps < base_size && DrawFloor && (!Method2Ramp || level % 2 == 1))
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_3";
            cubefloor.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3((holeWide - 2) * blockwide / 2, height - bh2, 0);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 2) * blockwide / 2, h / 2 + height - bh2, -sep / 2);
            cubefloor.transform.position -= new Vector3(-0.7f, 0, 0);
            if (setback)
                cubefloor.transform.position += new Vector3(-setbackWide / 2, 0, 0);
            if (MethodInsideRamp)
                cubefloor.transform.position -= new Vector3(-blockwide, 0, 0);
            //cubefloor.transform.position = new Vector3(-(base_size - sep) / 2 + 1, h / 2 + height - bh2, -sep / 2);
            cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.5f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.5f, holeWide * blockwide);
            }
            else
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.5f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Spiral)
                    cubefloor.transform.localScale = new Vector3(length + (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
            }
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
            cubefloor3 = cubefloor;

            if (rampMethod == RampMethodType.Spiral)
                DrawSteppedEmbankment(cubefloor, false);
        }
        
        if (DrawFloor && (!Method2Ramp || level % 2 == 0))
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_4";
            cubefloor.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height - bh2, -(holeWide - 2) * blockwide / 2);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - (holeWide - 2) * blockwide / 2);
            cubefloor.transform.position -= new Vector3(0, 0, 0.7f);
            if (setback)
                cubefloor.transform.position += new Vector3(0, 0, setbackWide / 2);
            if (MethodInsideRamp)
                cubefloor.transform.position -= new Vector3(0, 0, blockwide);
            //cubefloor.transform.position = new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - 1);
            cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
            }
            else
            {
                if (setback)
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide + setbackWide);
                else
                    cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 2, bh2 + 0.4f, holeWide * blockwide);
                if (rampMethod == RampMethodType.Spiral)
                    cubefloor.transform.localScale = new Vector3(length + (blockwide * spiralRampSeparation), bh2 + 0.4f, holeWide * blockwide);
            }
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
            cubefloor4 = cubefloor;

            if (rampMethod == RampMethodType.Spiral)
                DrawSteppedEmbankment(cubefloor, false);
        }
        
        // ramp wall
        GameObject cubewall = null;        
        if (minBaseSize4Ramps < base_size && DrawWall && (!Method2Ramp || level % 2 == 1))
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_1";
            cubewall.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-(holeWide - 1) * blockwide, height + blockheight * holeHeight / 2, 0);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - (holeWide - 1) * blockwide, h / 2 + height + blockheight * holeHeight / 2, sep / 2);
            //cubewall.transform.position = new Vector3((base_size - sep) / 2 - 1.5f * blockwide, h / 2 + height + blockheight, sep / 2);
            cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
            cubewall1 = cubewall;
        }
        
        if (DrawWall && (!Method2Ramp || level % 2 == 0))
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_2";
            cubewall.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, (holeWide - 1) * blockwide);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight / 2, -(base_size - sep) / 2 + (holeWide - 1) * blockwide);
            //cubewall.transform.position = new Vector3(sep / 2, h / 2 + height + blockheight, -(base_size - sep) / 2 + 1.5f * blockwide);
            cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight+2), 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
            cubewall2 = cubewall;
        }
        
        if (minBaseSize4Ramps < base_size && DrawWall && (!Method2Ramp || level % 2 == 1))
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_3";
            cubewall.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3((holeWide - 1) * blockwide, height + blockheight * holeHeight / 2, 0);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + (holeWide - 1) * blockwide, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
            //cubewall.transform.position = new Vector3(-(base_size - sep) / 2 + 1.5f * blockwide, h / 2 + height + blockheight, -sep / 2);
            cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
            cubewall3 = cubewall;
        }
        
        if (DrawWall && (!Method2Ramp || level % 2 == 0))
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_4";
            cubewall.tag = "Ramp";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight/2, -(holeWide - 1) * blockwide);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight/2, (base_size - sep) / 2 - (holeWide - 1) * blockwide);
            //cubewall.transform.position = new Vector3(-sep / 2, h / 2 + height + blockheight, (base_size - sep) / 2 - 1.5f * blockwide);
            cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 2) * blockwide * 2, blockheight * (holeHeight + 2), 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - (holeWide + 1) * blockwide * 2, blockheight * holeHeight, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
            cubewall4 = cubewall;
        }
        
        // corner floor
        if (DrawFloor)
        {
            float holeDiag = MathF.Sqrt((holeWide * blockwide) * (holeWide * blockwide));
            if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
            {
                GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "4Ramp_corner_" + level + "_" + row + "_1";
                cubecorner.tag = "Ramp";
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeDiag), height, (base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(setbackWide, 0, setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, -27.0f, -32.0f);
                if (setback)
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2 + setbackWide, holeWide * blockwide * 2 + setbackWide, 0.1f);
                else
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
                //cubecorner.transform.parent = objParent.transform;
                cubecorner.transform.parent = ramp_gameObject.transform;
                cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "4Ramp_corner_" + level + "_" + row + "_2";
                cubecorner.tag = "Ramp";
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeDiag), height, -(base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(setbackWide, 0, -setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, 53.0f, 48.0f);
                if (setback)
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2 + setbackWide, holeWide * blockwide * 2 + setbackWide, 0.1f);
                else 
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
                //cubecorner.transform.parent = objParent.transform;
                cubecorner.transform.parent = ramp_gameObject.transform;
                cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner.isStatic = true;
            }

            if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
            {
                GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "4Ramp_corner_" + level + "_" + row + "_3";
                cubecorner.tag = "Ramp";
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeDiag), height, -(base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(-setbackWide, 0, -setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, -42.0f, -48.0f);
                if (setback)
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2 + setbackWide, holeWide * blockwide * 2 + setbackWide, 0.1f);
                else
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
                //cubecorner.transform.parent = objParent.transform;
                cubecorner.transform.parent = ramp_gameObject.transform;
                cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "4Ramp_corner_" + level + "_" + row + "_4";
                cubecorner.tag = "Ramp";
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeDiag), height, (base_size / 2 - holeDiag));
                if (setback)
                    cubecorner.transform.position += new Vector3(-setbackWide, 0, setbackWide);
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, 32.0f, 27.0f);
                if (setback)
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2 + setbackWide, holeWide * blockwide * 2 + setbackWide, 0.1f);
                else
                    cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
                //cubecorner.transform.parent = objParent.transform;
                cubecorner.transform.parent = ramp_gameObject.transform;
                cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner.isStatic = true;
            }
        }

        // corner wall
        if (DrawWall)
        {
            float holeDiag = MathF.Sqrt(((holeWide + 1) * blockwide) * ((holeWide + 1) * blockwide));
            if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
            {
                GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "4Ramp_cornerwall_" + level + "_" + row + "_1";
                cubecorner_wall.tag = "Ramp";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag, height + blockheight * holeHeight / 2, (base_size / 2) - holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, 60.0f, 0.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight, 0.1f);
                //cubecorner_wall.transform.parent = objParent.transform;
                cubecorner_wall.transform.parent = ramp_gameObject.transform;
                cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner_wall.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "4Ramp_cornerwall_" + level + "_" + row + "_2";
                cubecorner_wall.tag = "Ramp";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeDiag, height + blockheight * holeHeight / 2, -(base_size / 2) + holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, -30.0f, 0.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight, 0.1f);
                //cubecorner_wall.transform.parent = objParent.transform;
                cubecorner_wall.transform.parent = ramp_gameObject.transform;
                cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner_wall.isStatic = true;
            }

            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "4Ramp_cornerwall_" + level + "_" + row + "_3";
                cubecorner_wall.tag = "Ramp";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag, height + blockheight * holeHeight / 2, -(base_size / 2) + holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, 60.0f, 90.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight, 0.1f);
                //cubecorner_wall.transform.parent = objParent.transform;
                cubecorner_wall.transform.parent = ramp_gameObject.transform;
                cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner_wall.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "4Ramp_cornerwall_" + level + "_" + row + "_4";
                cubecorner_wall.tag = "Ramp";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeDiag, height + blockheight * holeHeight / 2, (base_size / 2) - holeDiag);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, -30.0f, 0.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight, 0.1f);
                //cubecorner_wall.transform.parent = objParent.transform;
                cubecorner_wall.transform.parent = ramp_gameObject.transform;
                cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner_wall.isStatic = true;
            }
        }

        if (DrawWoodenCyl && !exportPyramidObj)
        {
            float angleInRadians = PyramidInclination * (MathF.PI / 180.0f);
            float baseWidth = (holeWide + 1.0f) * blockwide;
            float height_wood = baseWidth * MathF.Tan(angleInRadians);
            float hypotenuse_wood = baseWidth / MathF.Cos(angleInRadians) * 2 / 3;

            // wooden cylinder
            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_1";
                woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(-8.3f, -36.3f, 48.0f);
                woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
                //woodencyl.transform.parent = objParent.transform;
                woodencyl.transform.parent = ramp_gameObject.transform;
                woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
                woodencyl.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_2";
                woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2 ) + (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(45.0f, -35.0f, 7.0f);
                woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
                //woodencyl.transform.parent = objParent.transform;
                woodencyl.transform.parent = ramp_gameObject.transform;
                woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
                woodencyl.isStatic = true;
            }

            if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
            {
                GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_3";
                woodencyl.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(45.0f, 14.0f, -23.0f);
                woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
                //woodencyl.transform.parent = objParent.transform;
                woodencyl.transform.parent = ramp_gameObject.transform;
                woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
                woodencyl.isStatic = true;
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_4";
                woodencyl.transform.position = objParent.transform.position + new Vector3(-(base_size / 2)+ (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(135.0f, -18.0f, 20.0f);
                woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
                //woodencyl.transform.parent = objParent.transform;
                woodencyl.transform.parent = ramp_gameObject.transform;
                woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
                woodencyl.isStatic = true;
            }
        }

        GameObject workers_gameObject = new GameObject();
        workers_gameObject.name = "Team_" + level;
        workers_gameObject.transform.parent = iter_gameObject.transform;
        workers_gameObject.isStatic = true;
        if (Sequenced)
            workers_gameObject = objParent;

        // stone sled
        if (DrawEgyptians && stone_sled && height < Height * 0.9f && !exportPyramidObj)
        {
            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_1";
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide / 2), height + 1.5f, (base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 90.0f + RampInclination, RampInclination);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
                stone_sled1.transform.position = stone_sled1.transform.position + new Vector3(0, -0.8f, 0);
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_2";
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 2.5f * blockwide), height + 1.5f, -(base_size / 2 - holeWide * blockwide / 2));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, RampInclination, -RampInclination);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
                stone_sled1.transform.position = stone_sled1.transform.position + new Vector3(0, -0.8f, 0);
            }

            if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
            {
                GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_3";
                stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide / 2), height + 1.5f, -(base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 90.0f+ RampInclination, -RampInclination);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
                stone_sled1.transform.position = stone_sled1.transform.position + new Vector3(0, -0.8f, 0);
            }

            if (!Method2Ramp || level % 2 == 0)
            {
                GameObject stone_sled1 = Instantiate(stone_sled, new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_4";
                stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - 2.5f * blockwide), height + 1.5f, (base_size / 2 - holeWide * blockwide / 2));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, RampInclination, RampInclination);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
                stone_sled1.transform.position = stone_sled1.transform.position + new Vector3(0, -0.8f, 0);
            }
        }

        // egyptians
        if (DrawEgyptians && Egyptian_body && height < Height * 0.9f && !exportPyramidObj)
        {
            for (int i = 0; i < 12; i++)
            {
                // left hand
                if (minBaseSize4Ramps < base_size &&  (!Method2Ramp || level % 2 == 1))
                {
                    GameObject Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_1";
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);                    
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                    Egyptian.transform.position = Egyptian.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (!Method2Ramp || level % 2 == 0)
                {
                    GameObject Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_2";
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, -(base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, 90.0f+ RampInclination + 180f, 0.0f);
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                    Egyptian.transform.position = Egyptian.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
                {
                    GameObject Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_3";
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, 180.0f+ RampInclination + 180f, 0.0f);
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                    Egyptian.transform.position = Egyptian.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (!Method2Ramp || level % 2 == 0)
                {
                    GameObject Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_4";
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, (base_size / 2 - (1.25f + 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian.transform.localRotation = Quaternion.Euler(RampInclination, -90.0f+ RampInclination + 180f, 0.0f);
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                    Egyptian.transform.position = Egyptian.transform.position + new Vector3(0, -2.8f, 0);
                }

                // right hand
                if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
                {
                    GameObject Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_1";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0.25f, 0, 0);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                    Egyptian2.transform.position = Egyptian2.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (!Method2Ramp || level % 2 == 0)
                {
                    GameObject Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_2";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, -(base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, 90.0f+ RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0, 0, -0.25f);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                    Egyptian2.transform.position = Egyptian2.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (minBaseSize4Ramps < base_size && (!Method2Ramp || level % 2 == 1))
                {
                    GameObject Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_3";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2), height + 3.5f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, 180.0f+ RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(-0.25f, 0, 0);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                    Egyptian2.transform.position = Egyptian2.transform.position + new Vector3(0, -2.8f, 0);
                }

                if (!Method2Ramp || level % 2 == 0)
                {
                    GameObject Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_4";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 3.5f + 0.16f * i, (base_size / 2 + (0.75f - 0.1f * i) - holeWide * blockwide / 2));
                    Egyptian2.transform.localRotation = Quaternion.Euler(RampInclination, -90.0f+ RampInclination + 180f, 0.0f);
                    Egyptian2.transform.position -= new Vector3(0, 0, 0.25f);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                    Egyptian2.transform.position = Egyptian2.transform.position + new Vector3(0, -2.8f, 0);
                }
            }
        }

        // only at level 0 if 8 ramps
        if (level == 0 && Method8Ramp && DrawUntilRow && row < 21 && minBaseSize8Ramps < base_size)
        {
            // Middle Ramp 1
            GameObject cube = Instantiate(cube1);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.name = "Middle_4Ramp_" + level + "_" + row + "_1";
            cube.transform.position += new Vector3(0, 0, -base_size / 2);

            cube = Instantiate(cube2);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.name = "Middle_4Ramp_" + level + "_" + row + "_2";
            cube.transform.position += new Vector3(-base_size / 2, 0, 0);

            cube = Instantiate(cube3);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.name = "Middle_4Ramp_" + level + "_" + row + "_3";
            cube.transform.position += new Vector3(0, 0, base_size / 2);

            cube = Instantiate(cube4);
            //cube.transform.parent = objParent.transform;
            cube.transform.parent = ramp_gameObject.transform;
            cube.name = "Middle_4Ramp_" + level + "_" + row + "_4";
            cube.transform.position += new Vector3(base_size / 2, 0, 0);

            // ramp floor
            if (DrawFloor)
            {
                cubefloor = Instantiate(cubefloor1);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_1";
                cubefloor.tag = "Ramp";
                cubefloor.transform.position += new Vector3(0, 0, -base_size / 2);

                cubefloor = Instantiate(cubefloor2);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_2";
                cubefloor.tag = "Ramp";
                cubefloor.transform.position += new Vector3(-base_size / 2, 0, 0);

                cubefloor = Instantiate(cubefloor3);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_3";
                cubefloor.tag = "Ramp";
                cubefloor.transform.position += new Vector3(0, 0, base_size / 2);

                cubefloor = Instantiate(cubefloor4);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_4";
                cubefloor.tag = "Ramp";
                cubefloor.transform.position += new Vector3(base_size / 2, 0, 0);
            }

            // ramp wall
            if (DrawWall)
            {
                cubewall = Instantiate(cubewall1);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_1";
                cubewall.tag = "Ramp";
                cubewall.transform.position += new Vector3(0, 0, -base_size / 2);

                cubewall = Instantiate(cubewall2);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_2";
                cubewall.tag = "Ramp";
                cubewall.transform.position += new Vector3(-base_size / 2, 0, 0);

                cubewall = Instantiate(cubewall3);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_3";
                cubewall.tag = "Ramp";
                cubewall.transform.position += new Vector3(0, 0, base_size / 2);

                cubewall = Instantiate(cubewall4);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_4";
                cubewall.tag = "Ramp";
                cubewall.transform.position += new Vector3(base_size / 2, 0, 0);
            }

            if (level == 0 && Method16Ramp && DrawUntilRow && row < 11 && minBaseSize16Ramps < base_size)
            {
                // Middle Ramp 16
                cube = Instantiate(cube1);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_1";
                cube.transform.position += new Vector3(0, 0, -base_size * 3 / 4);

                cube = Instantiate(cube1);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_2";
                cube.transform.position += new Vector3(0, 0, -base_size / 4);

                cube = Instantiate(cube2);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_3";
                cube.transform.position += new Vector3(-base_size * 3 / 4, 0, 0);

                cube = Instantiate(cube2);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_4";
                cube.transform.position += new Vector3(-base_size / 4, 0, 0);

                cube = Instantiate(cube3);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_5";
                cube.transform.position += new Vector3(0, 0, base_size * 3 / 4);

                cube = Instantiate(cube3);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_6";
                cube.transform.position += new Vector3(0, 0, base_size / 4);

                cube = Instantiate(cube4);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_7";
                cube.transform.position += new Vector3(base_size * 3/ 4, 0, 0);

                cube = Instantiate(cube4);
                //cube.transform.parent = objParent.transform;
                cube.transform.parent = ramp_gameObject.transform;
                cube.name = "Middle_8Ramp_" + level + "_" + row + "_8";
                cube.transform.position += new Vector3(base_size / 4, 0, 0);

                if (DrawFloor)
                {
                    cubefloor = Instantiate(cubefloor1);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_1";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(0, 0, -base_size * 3 / 4);

                    cubefloor = Instantiate(cubefloor1);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_2";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(0, 0, -base_size / 4);

                    cubefloor = Instantiate(cubefloor2);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_3";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(-base_size * 3 / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor2);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_4";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(-base_size / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor3);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_5";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(0, 0, base_size * 3 / 4);

                    cubefloor = Instantiate(cubefloor3);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_6";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(0, 0, base_size / 4);

                    cubefloor = Instantiate(cubefloor4);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_7";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(base_size * 3 / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor4);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_8";
                    cubefloor.tag = "Ramp";
                    cubefloor.transform.position += new Vector3(base_size / 4, 0, 0);
                }

                // ramp wall
                if (DrawWall)
                {
                    cubewall = Instantiate(cubewall1);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_1";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(0, 0, -base_size * 3 / 4);

                    cubewall = Instantiate(cubewall1);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_2";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(0, 0, -base_size / 4);

                    cubewall = Instantiate(cubewall2);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_3";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(-base_size * 3 / 4, 0, 0);

                    cubewall = Instantiate(cubewall2);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_4";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(-base_size / 4, 0, 0);

                    cubewall = Instantiate(cubewall3);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_5";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(0, 0, base_size * 3 / 4);

                    cubewall = Instantiate(cubewall3);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_6";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(0, 0, base_size / 4);

                    cubewall = Instantiate(cubewall4);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_7";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(base_size * 3 / 4, 0, 0);

                    cubewall = Instantiate(cubewall4);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_8";
                    cubewall.tag = "Ramp";
                    cubewall.transform.position += new Vector3(base_size / 4, 0, 0);
                }
            }
        }
    }

    private IEnumerator ExportObj()
    {
        yield return new WaitForSeconds(1.0f);

        string exportPath = Path.Combine(Application.persistentDataPath, exportSubFolder);
        string fileName = outputFileName;

        ObjExporter.ExportGameObjectToObj(objParent, exportPath, fileName, exportCombineMeshes);
        Debug.Log($"Exportado a: {Path.Combine(exportPath, fileName + ".obj")}");
        Debug.Log("Para ver la carpeta: En Unity Editor, haz clic derecho en este script y selecciona 'Open Export Folder'.");
    }

    /// <summary>
    /// Delete the existing pyramid from the scene if it exists.
    /// </summary>
    public void ClearPyramid(bool all)
    {
        // delete previous row
        if (!all && DrawOnlyRow)
        {
            // Loop optimized for the 'true' case
            for (int i = objParent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = objParent.transform.GetChild(i).gameObject;
                if (child.TryGetComponent(out BoxCollider bc) && bc.isTrigger)
                {
                    GameObject.Destroy(child);
                }
            }
        }
        else
        {
            // Loop optimized for the 'false' case
            for (int i = objParent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = objParent.transform.GetChild(i).gameObject;
                GameObject.Destroy(child);
            }
        }
        // Also clear the list of detected blocks when creating a new pyramid
        DetectedDeletedBlocks.Clear();
    }

    /// <summary>
    /// Restaura los valores de los parámetros a sus valores por defecto.
    /// </summary>
    public void ResetValues()
    {
        rampMethod = RampMethodType.Integrated;
        selectedPyramid = PyramidType.Default;
        BaseSize = 230;
        Height = 147; // 147 is the height of the pyramid of Khufu
        PyramidInclination = 51.84f;
        RampInclination = 7;
        blockheight = 0.71f;
        blockwide = 1.27f;
        PathWide = 0;
        PathSeparation = 0;
        holeHeight = 3;
        holeWide = 3;
        blockSeparation = 0.01f; // separation between blocks
        massBlock = 2267.96f;
        EmbankmentVolume = 0f;
        PyramidVolume = 0f;
        // Reset new variables to their defaults
        holeHeight = 3;
        holeWide = 3;
        Method2Ramp = false;
        Method4Ramp = false;
        MethodInsideRamp = false;
        Method8Ramp = false;
        Method16Ramp = false;
        DrawUntilRow = false;
        DrawOnlyRow = false;
        DrawCasing = false;
        DrawRow = 0;
        DrawBlocks = 1;
        DeletedBlocks = 0;
        DrawWall = true;
        DrawFloor = true;
        DrawWoodenCyl = true;
        DrawEgyptians = true;
        DrawGranite = true;
        DrawAll = false;
        showRamps = true;
        halfPyramid = false;
        setback = false;
        StraightRampFace = RampPositionFace.NorthFace;
        DrawPyramidInterior = false;
        DrawPyramidInteriorTransparent = true;

        PyramidVolume = 0f;
        EmbankmentVolume = 0f;
        Decomisioning = false;
        AnimateDecommissioning = false;
        DecommissioningTimeLapse = 0.1f;
        DecommissioningStep = 0.05f;               
        SideSlopeAngle = 30.0f;
        spiralRampSeparation = 2;
        internalRampStraightRampHigh = 40.0f;
        pyramidTransparency = 0.25f;

        showInfoGranite = false;        
        numberOfGranite10tons = 6;
        numberOfLimestone40tons = 24;
        numberOfGranite50tons = 0;
        numberOfGranite60tons = 0;
        numberOfGranite70tons = 45;
        numberOfGranite80tons = 0;
        startCourseKingsChamber = 60;
        endCourseKingsChamber = 85;
        endCourseGableteKingsChamber = 96;
        forcePerPullerNewtons = 250.0f;
        mezzanineRampAngleDegrees = 3.0f;
        mezzanineFrictionCoef = 0.2f;
        horizontalTransferDistanceMeters = 10.0f;
        setupTimePerCourseHours = 2.0f;
        setupTimePerCourseGroups = 6;
        pullingSpeedRampMetersPerSecond = 0.15f;
        pullingSpeedTerraceMetersPerSecond = 0.20f;
        useCapstan = true;
        frictionCoefCapstan = 0.3f;
        capstanWrapAngleRadians = Mathf.PI;
        totalGraniteMoveTimeWorkingYears = 0;

        Debug.Log("Properties reset to default values.");
    }

    public void onChangePyramidType()
    {
        // If the selected option is not 'Default', update the dimensions.
        if (selectedPyramid != PyramidType.Default)
        {
            switch (selectedPyramid)
            {
                case PyramidType.Khufu:
                    BaseSize = 230.36f;
                    Height = 146.50f;
                    PyramidInclination = 51.85f;
                    blockheight = 0.71f;
                    break;

                case PyramidType.Khafre:
                    BaseSize = 215.25f;
                    Height = 143.50f;
                    PyramidInclination = 53.17f;
                    blockheight = 0.70f;
                    break;

                case PyramidType.Menkaure:
                    BaseSize = 108.5f;
                    Height = 65.5f;
                    PyramidInclination = 51.34f;
                    blockheight = 0.65f;
                    break;

                case PyramidType.Bent_bottom:
                    BaseSize = 188.0f;
                    Height = 47f; // Total height of the pyramid.
                    PyramidInclination = 54.5f;
                    blockheight = 0.66f; // An average value.
                    break;

                case PyramidType.Bent_top:
                    BaseSize = 124.5f;
                    Height = 58f; // Total height of the pyramid.
                    PyramidInclination = 43.3f;
                    blockheight = 0.66f; // An average value.
                    break;

                case PyramidType.Red:
                    BaseSize = 220.0f;
                    Height = 104.4f;
                    PyramidInclination = 43.3f;
                    blockheight = 0.6f;
                    break;
            }
        }
    }

    /// <summary>
    /// Sets the visibility of all MeshRenderers in the children of the PyramidInterior GameObject.
    /// </summary>
    /// <param name="isVisible">True to make them visible, false to hide them.</param>
    public void SetInteriorVisibility(bool isVisible)
    {
        if (PyramidInterior == null)
            return;
        
        // Get all MeshRenderer components in the children, including the parent itself.
        MeshRenderer[] renderers = PyramidInterior.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = isVisible;
        }        
    }

    /// <summary>
    /// Asynchronously destroys the detected blocks in steps.
    /// </summary>
    private IEnumerator DecommissionCoroutine()
    {
        Debug.Log($"Starting decommissioning animation for {DetectedDeletedBlocks.Count} blocks...");
        //if (progressBar) progressBar.Show("Decommissioning...");

        int totalBlocksToDecommission = DetectedDeletedBlocks.Count;
        int batchSize = Mathf.Max(1, Mathf.CeilToInt(totalBlocksToDecommission * DecommissioningStep)); // 5% per step
        int blocksProcessed = 0;

        for (int i = 0; i < totalBlocksToDecommission; i++)
        {
            GameObject block = DetectedDeletedBlocks[i];
            if (block != null) // Check if the block hasn't been destroyed already
            {
                block.tag = "Untagged"; // Remove tag to avoid re-detection
                block.SetActive(true);
            }

            blocksProcessed++;

            // After processing a batch, update progress and wait.
            if (blocksProcessed % batchSize == 0 || i == totalBlocksToDecommission - 1)
            {
                float progress = (float)(i + 1) / totalBlocksToDecommission;
                //if (progressBar) progressBar.SetProgress(progress, $"Decommissioning... ({i + 1}/{totalBlocksToDecommission})");

                yield return new WaitForSeconds(DecommissioningTimeLapse);
            }
        }

        //if (progressBar) progressBar.Hide();
        Debug.Log("Decommissioning animation finished.");
        DetectedDeletedBlocks.Clear(); // Clean up the list after animation
    }

    /// <summary>
    /// Adds a GameObject to the list of blocks detected for deletion.
    /// This is called by the DeleteObject script before the object is destroyed.
    /// </summary>
    /// <param name="blockToAdd">The GameObject that was detected.</param>
    public void AddDetectedBlock(GameObject blockToAdd)
    {
        if (blockToAdd == null) return;

        // Use BinarySearch with our custom comparer to find the correct insertion index.
        int index = DetectedDeletedBlocks.BinarySearch(blockToAdd, yComparer);

        // If BinarySearch returns a negative number, it's the bitwise complement
        // of the index of the next element that is larger than the object.
        if (index < 0)
        {
            index = ~index;
        }

        DetectedDeletedBlocks.Insert(index, blockToAdd);
    }

    /// <summary>
    /// Calcula y dibuja una rampa recta desde la última hilada dibujada hasta el suelo, orientada correctamente.
    /// </summary>
    private void DrawStraightRamp()
    {
        if (RampInclination <= 0)
        {
            Debug.LogError("La Inclinación de la Rampa debe ser mayor que 0.");
            return;
        }

        CalculateMetricsAtRow(DrawRow, out float startHeight, out float totalSetback);

        float rampInclinationRad = RampInclination * Mathf.Deg2Rad;
        float rampLength = startHeight / Mathf.Sin(rampInclinationRad);
        float horizontalProjection = startHeight / Mathf.Tan(rampInclinationRad);

        Vector3 startPosition = Vector3.zero;
        Vector3 endPosition = Vector3.zero;
        Quaternion rampRotation = Quaternion.identity;
        float faceOffset = (BaseSize / 2) - totalSetback;

        switch (StraightRampFace)
        {
            case RampPositionFace.NorthFace:
                startPosition = new Vector3(0, startHeight, faceOffset);
                endPosition = new Vector3(0, 0, startPosition.z + horizontalProjection);
                rampRotation = Quaternion.Euler(RampInclination, 0, 0);
                break;
            case RampPositionFace.SouthFace:
                startPosition = new Vector3(0, startHeight, -faceOffset);
                endPosition = new Vector3(0, 0, startPosition.z - horizontalProjection);
                rampRotation = Quaternion.Euler(-RampInclination, 0, 0);
                break;
            case RampPositionFace.EastFace:
                startPosition = new Vector3(faceOffset, startHeight, 0);
                endPosition = new Vector3(startPosition.x + horizontalProjection, 0, 0);
                rampRotation = Quaternion.Euler(0, 90, 0) * Quaternion.Euler(RampInclination, 0, 0);
                break;
            case RampPositionFace.WestFace:
                startPosition = new Vector3(-faceOffset, startHeight, 0);
                endPosition = new Vector3(startPosition.x - horizontalProjection, 0, 0);
                rampRotation = Quaternion.Euler(0, -90, 0) * Quaternion.Euler(RampInclination, 0, 0);
                break;                
        }

        GameObject rampObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rampObj.name = "StraightRamp";
        if (objParent != null)
        {
            rampObj.transform.SetParent(objParent.transform);
        }
        rampObj.isStatic = true;

        rampObj.transform.position = (startPosition + endPosition) / 2;
        rampObj.transform.localScale = new Vector3(holeWide * blockwide, 0.25f, rampLength);
        rampObj.transform.rotation = rampRotation;
        if (m_Material_floor)
            rampObj.GetComponent<MeshRenderer>().material = m_Material_floor;

        DrawSteppedEmbankment(rampObj,true);
    }


    /// <summary>
    /// Creates a stepped embankment under the ramp surface, layer by layer.
    /// </summary>
    private void DrawSteppedEmbankment(GameObject rampSurface, bool rampX)
    {
        if (SideSlopeAngle <= 0 || SideSlopeAngle >= 90) return;

        // Get initial metrics from the top ramp surface
        CalculateMetricsAtRow(DrawRow, out float startHeight, out _);        
        float currentYPosition = startHeight;
        float currentWidth = rampSurface.transform.localScale.x;
        if (!rampX)
            currentWidth = rampSurface.transform.localScale.z;

        if (rampMethod == RampMethodType.Straight)
        {
            CalculateEmbankmentVolume();
            Debug.Log($"Volumen del Terraplén Calculado: {EmbankmentVolume:N2} m³");
            Debug.Log($"% Volumen del Terraplén Calculado sobre el total de la pirámide : {EmbankmentVolume * 100 / PyramidVolume:N2} %");
        }
        
        // Create a parent for the embankment layers to keep the hierarchy clean.
        // It shares the same orientation as the ramp surface.
        GameObject embankmentParent = new GameObject("StraightRamp_Embankment");
        if (objParent != null) embankmentParent.transform.SetParent(objParent.transform);
        embankmentParent.transform.position = rampSurface.transform.position;
        embankmentParent.transform.rotation = rampSurface.transform.rotation;

        // Loop from the course just below the ramp's start down to the ground (row 0)
        for (int row = DrawRow - 1; row >= 0; row--)
        {
            float courseHeight = GetBlockHeightForRow(row);
            if (courseHeight <= 0) continue;

            // Calculate how much wider this new layer should be on each side
            float stepExpansion = courseHeight / Mathf.Tan(SideSlopeAngle * Mathf.Deg2Rad);
            float newWidth = currentWidth + (2 * stepExpansion);

            // Create the new layer as a simple cube
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            layer.name = $"Embankment_Layer_{row}";
            layer.isStatic = true;
            layer.transform.SetParent(embankmentParent.transform, false); // Set parent without changing world position yet
            if (m_Material_adobe)
                layer.GetComponent<MeshRenderer>().material = m_Material_adobe;
            else
                if (m_Material)
                    layer.GetComponent<MeshRenderer>().material = m_Material;

            // Set its scale in its local space
            if (rampX)
                layer.transform.localScale = new Vector3(newWidth, courseHeight, rampSurface.transform.localScale.z);
            else
                layer.transform.localScale = new Vector3(rampSurface.transform.localScale.x, courseHeight, newWidth);

            // Position it relative to the embankment parent.
            // The center of this new layer must be aligned under the main ramp's center.
            float middleOfCurrentLayerY = currentYPosition - (courseHeight / 2);
            float downwardOffset = startHeight - middleOfCurrentLayerY;
            layer.transform.localPosition = new Vector3(0, -downwardOffset, 0);

            // Update variables for the next iteration
            currentWidth = newWidth;
            currentYPosition -= courseHeight;            
        }
    }

    /// <summary>
    /// Calculates the total height and horizontal setback at a specific row number.
    /// </summary>
    private void CalculateMetricsAtRow(int row, out float totalHeight, out float totalSetback)
    {
        totalHeight = 0f;
        totalSetback = 0f;
        float pyramidInclinationRad = PyramidInclination * Mathf.Deg2Rad;
        if (Mathf.Tan(pyramidInclinationRad) == 0) return;

        for (int i = 0; i <= row; i++)
        {
            float courseHeight = GetBlockHeightForRow(i);
            totalHeight += courseHeight;
            totalSetback += courseHeight / Mathf.Tan(pyramidInclinationRad);
        }
    }

    /// <summary>
    /// Calculate the entry point on the pyramid terrace for the straight ramp at a given height.
    /// </summary>
    /// <param name="currentHeight">La altura actual de la hilada que se está construyendo.</param>
    /// <returns>La coordenada 3D del punto de entrada en la terraza.</returns>
    public Vector3 GetStraightRampTerraceEntryPoint(float currentHeight)
    {
        // 1. Calculate the horizontal setback at the current height.
        float pyramidInclinationRad = PyramidInclination * Mathf.Deg2Rad;
        if (Mathf.Tan(pyramidInclinationRad) == 0) return new Vector3(0, currentHeight, 0);
        float setback = currentHeight / Mathf.Tan(pyramidInclinationRad);

        // 2. Calculate the position of the edge of the terrace.
        float faceOffset = (BaseSize / 2) - setback;
        Vector3 entryPoint = Vector3.zero;

        // 3. Determine the coordinate of the entry point based on the selected face.
        switch (StraightRampFace)
        {
            case RampPositionFace.NorthFace:
                entryPoint = new Vector3(0, currentHeight, faceOffset);
                break;
            case RampPositionFace.SouthFace:
                entryPoint = new Vector3(0, currentHeight, -faceOffset);
                break;
            case RampPositionFace.EastFace:
                entryPoint = new Vector3(faceOffset, currentHeight, 0);
                break;
            case RampPositionFace.WestFace:
                entryPoint = new Vector3(-faceOffset, currentHeight, 0);
                break;
        }

        return entryPoint;
    }

    /// <summary>
    /// Calculate the total volume of the entire pyramid.
    /// </summary>
    private void CalculateTotalVolume()
    {
        if (BaseSize <= 0 || Height <= 0)
        {
            PyramidVolume = 0;
            return;
        }

        // Formula for the volume of a square-based pyramid: (1/3) * side² * height
        PyramidVolume = (BaseSize * BaseSize * Height) / 3f;
        Debug.Log($"Total Pyramid Volume of the Calculated Pyramid: {PyramidVolume:N2} m³");
    }

    /// <summary>
    /// Calculate the volume of the pyramid up to a specific course (a truncated pyramid).
    /// </summary>
    private void CalculateVolumeUntilRow(int targetRow)
    {
        if (targetRow <= 0)
        {
            PyramidVolume = 0;
            return;
        }

        // 1. Calculate the height of the truncated pyramid (the frustum).
        CalculateMetricsAtRow(targetRow, out float frustumHeight, out _);

        // Asegurarse de que la altura no exceda la altura total.
        frustumHeight = Mathf.Min(frustumHeight, Height);

        if (Height <= 0)
        {
            PyramidVolume = 0;
            return;
        }

        // 2. Calculate the size of the upper base of the frustum using similarity of triangles.
        float heightOfCutOffPyramid = Height - frustumHeight;
        float topBaseSize = (heightOfCutOffPyramid * BaseSize) / Height;

        // 3. Calculate the volume of the complete pyramid..
        float volumeFullPyramid = (BaseSize * BaseSize * Height) / 3f;

        // 4. Calculate the volume of the small pyramid that is "cut" from the top.
        float volumeCutOffPyramid = (topBaseSize * topBaseSize * heightOfCutOffPyramid) / 3f;

        // 5. The volume of the frustum is the difference.
        PyramidVolume = volumeFullPyramid - volumeCutOffPyramid;

        Debug.Log($"Pyramid volume calculated up to the row {targetRow} (Height: {frustumHeight:F2}m): {PyramidVolume:N2} m³");
    }

    /// <summary>
    /// Calculate the volume of the straight ramp embankment using the integral formula.
    /// </summary>
    /// <summary>
    /// Calculates the gross and clipped volume of the straight ramp embankment using the integral formula.
    /// </summary>
    private void CalculateEmbankmentVolume()
    {
        float ClippedEmbankmentVolume = 0;
        float IntrudingVolumePercentage = 0;
        if (RampInclination <= 0 || SideSlopeAngle <= 0 || SideSlopeAngle >= 90)
        {
            EmbankmentVolume = 0;            
            return;
        }

        // 1. Get necessary parameters
        CalculateMetricsAtRow(DrawRow, out float h, out _); // h = Ramp start height
        float w = holeWide * blockwide;                     // w = Ramp useful width
        float theta = RampInclination * Mathf.Deg2Rad;      // θ = Ramp angle in radians
        float alpha = SideSlopeAngle * Mathf.Deg2Rad;       // α = Side slope angle in radians

        if (h <= 0 || Mathf.Sin(theta) == 0)
        {
            EmbankmentVolume = 0;
            ClippedEmbankmentVolume = 0;
            IntrudingVolumePercentage = 0;
            return;
        }

        // 2. Calculate intermediate values from the integral formula
        float L = h / Mathf.Sin(theta);             // L = Ramp length
        float sin_theta = Mathf.Sin(theta);
        float cot_alpha = 1f / Mathf.Tan(alpha);

        // 3. Apply the integral formula to get the gross volume (V_free)
        // V_free = (w*sin(θ)/2)*L² + (cot(α)*sin²(θ)/3)*L³
        float term1 = (w * sin_theta / 2f) * (L * L);
        float term2 = (cot_alpha * (sin_theta * sin_theta) / 3f) * (L * L * L);
        EmbankmentVolume = term1 + term2;

        // 4. Calculate the percentage of the volume that intrudes into the pyramid (p_inside)
        float denominator = 2f + (3f * w) / (h * cot_alpha);
        if (denominator == 0)
        {
            IntrudingVolumePercentage = 0;
            ClippedEmbankmentVolume = EmbankmentVolume;
            return;
        }
        IntrudingVolumePercentage = 1f / denominator;

        // 5. Calculate the final clipped volume (V_clip)
        ClippedEmbankmentVolume = (1f - IntrudingVolumePercentage) * EmbankmentVolume;        

        Debug.Log($"Gross Embankment Volume (V_free): {EmbankmentVolume:N2} m³, Intrusion: {IntrudingVolumePercentage:P2}, Net Clipped Volume (V_clip): {ClippedEmbankmentVolume:N2} m³");

        EmbankmentVolume = EmbankmentVolume - ClippedEmbankmentVolume;

        //Debug.Log($"Embankment Volume (V_free-V_clip): {EmbankmentVolume:N2} m³");
    }


    /// <summary>
    /// Helper function to get the top 4 corners of the ramp surface in world coordinates.
    /// </summary>
    private Vector3[] GetWorldCorners(GameObject obj)
    {
        Vector3[] corners = new Vector3[4];
        Transform t = obj.transform;
        float width = t.localScale.x / 2f;
        float depth = t.localScale.z / 2f;
        float height = t.localScale.y / 2f;

        corners[0] = t.TransformPoint(new Vector3(-width, height, depth)); // Top-Far-Left
        corners[1] = t.TransformPoint(new Vector3(width, height, depth));  // Top-Far-Right
        corners[2] = t.TransformPoint(new Vector3(width, height, -depth)); // Top-Near-Right
        corners[3] = t.TransformPoint(new Vector3(-width, height, -depth));// Top-Near-Left

        return corners;
    }


    /// <summary>
    /// Gets the block height for a specific row.
    /// Uses the Khufu list if enabled and the row is within bounds, otherwise returns the default height.
    /// </summary>
    /// <param name="rowNumber">The absolute row number, starting from 0 at the base.</param>
    /// <returns>The height of the block for that row.</returns>
    private float GetBlockHeightForRow(int rowNumber)
    {
        if (useKhufuCourseHeights && rowNumber >= 0 && rowNumber < khufuCourseHeights.Count)
        {
            return khufuCourseHeights[rowNumber];
        }
        // Fallback to the default block height if the option is disabled or the row is out of bounds.
        return this.blockheight;
    }

    /// <summary>
    /// Sets the transparency for all GameObjects with the "Block" tag under the main parent.
    /// </summary>
    /// <param name="alpha">The transparency level, from 0 (invisible) to 1 (opaque).</param>
    public void SetPyramidTransparency(float alpha)
    {
        float pyramidTransparency = Mathf.Clamp01(alpha); // Ensure value is between 0 and 1

        if (objParent == null) return;
        if (m_Material == null)
        {
            Debug.LogError("Transparent Block Material is not assigned in the Inspector.");
            return;
        }

        // Get all renderers in the children of the parent object
        Renderer[] renderers = objParent.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Only affect objects tagged as "Block"
            if (renderer.gameObject.CompareTag("Block"))
            {
                renderer.material = m_Material;
                Color color = renderer.material.color;
                color.a = pyramidTransparency;
                renderer.material.color = color;                
            }
        }
    }

    /// <summary>
    /// Calculates the force required to pull a single block up the mezzanine ramp.
    /// </summary>
    private float CalculatePullForce(float mass, float rampAngleRad, float rampFrictionCoeff, out float totalPullForce)
    {
        float sinTheta = Mathf.Sin(rampAngleRad);
        float cosTheta = Mathf.Cos(rampAngleRad);

        float forceParallel = mass * g * sinTheta;
        float forceFriction = mass * g * cosTheta * rampFrictionCoeff;
        totalPullForce = forceParallel + forceFriction; // This is the raw force needed

        if (useCapstan)
        {
            float forceMultiplier = Mathf.Exp(frictionCoefCapstan * capstanWrapAngleRadians);
            return totalPullForce / forceMultiplier; // This is the final force to apply
        }
        else
        {
            return totalPullForce; // No capstan, final force is the raw force
        }
    }

    /// <summary>
    /// Calculates the number of pullers required for a given force.
    /// </summary>
    private int CalculatePullers(float totalForceToApply)
    {
        if (forcePerPullerNewtons <= 0) return 0;
        return Mathf.CeilToInt(totalForceToApply / forcePerPullerNewtons);
    }

    /// <summary>
    /// Calculates the total work (energy) in MegaJoules to move one block.
    /// </summary>
    private float CalculateWork(float mass, float distanceOnRamp, float rampAngleRad, float frictionCoeff)
    {
        // Work on ramp = (Force) * distance
        float sinTheta = Mathf.Sin(rampAngleRad);
        float cosTheta = Mathf.Cos(rampAngleRad);
        float totalPullForce = (mass * g * sinTheta) + (mass * g * cosTheta * frictionCoeff);
        float workOnRamp_J = totalPullForce * distanceOnRamp;

        // Work on horizontal transfer
        float workHorizontal_J = (mass * g * frictionCoeff) * horizontalTransferDistanceMeters;

        return (workOnRamp_J + workHorizontal_J) / 1000000f; // Convert to MegaJoules
    }

    /// <summary>
    /// Processes and logs the calculations for moving granite megaliths for a specific course.
    /// </summary>
    private void ProcessGraniteCalculations(int row, float currentCourseHeight)
    {
        //if (!showInfoGranite || csvgranitewriter == null) return;
        if (endCourseGableteKingsChamber==0) endCourseGableteKingsChamber = endCourseKingsChamber;
        if (row > endCourseGableteKingsChamber) return;

        // 1. Calculate blocks for *this course*
        float totalCourses = (endCourseKingsChamber - startCourseKingsChamber) + 1;
        if (totalCourses <= 0 && row>startCourseKingsChamber) return;

        float remainingPercentage = 0f;
        if (row < startCourseKingsChamber)
        {
            remainingPercentage = 1.0f;
        }
        else if (row > endCourseKingsChamber)
        {
            remainingPercentage = 0.0f;
        }
        else
        {
            float totalCoursesInRange = (endCourseKingsChamber - startCourseKingsChamber) + 1;
            if (totalCoursesInRange <= 0) totalCoursesInRange = 1;
            float coursesCompleted = row - startCourseKingsChamber;
            remainingPercentage = Mathf.Clamp01(1.0f - (coursesCompleted / totalCoursesInRange));
        }

        float remainingPercentageLimestone = 0f;
        if (row < endCourseKingsChamber)
        {
            remainingPercentageLimestone = 1.0f;
        }
        else if (row > endCourseGableteKingsChamber)
        {
            remainingPercentageLimestone = 0.0f;
        }
        else 
        {
            float totalCoursesInRangeLimestone = (endCourseGableteKingsChamber - endCourseKingsChamber);
            if (totalCoursesInRangeLimestone <= 0) totalCoursesInRangeLimestone = 1;
            float coursesCompletedLimestone = endCourseGableteKingsChamber - row;
            remainingPercentageLimestone = 1.0f - Mathf.Clamp01(1.0f - (coursesCompletedLimestone / totalCoursesInRangeLimestone));
        }

        int blocks10t = (int)Mathf.Ceil(numberOfGranite10tons * remainingPercentage);
        int blocks40t = (int)Mathf.Ceil(numberOfLimestone40tons * remainingPercentageLimestone);
        int blocks50t = (int)Mathf.Ceil(numberOfGranite50tons * remainingPercentage);
        int blocks60t = (int)Mathf.Ceil(numberOfGranite60tons * remainingPercentage);
        int blocks70t = (int)Mathf.Ceil(numberOfGranite70tons * remainingPercentage);
        int blocks80t = (int)Mathf.Ceil(numberOfGranite80tons * remainingPercentage);
        int totalBlocksThisCourse = blocks10t + blocks40t + blocks50t + blocks60t + blocks70t + blocks80t;

        if (totalBlocksThisCourse == 0) return;

        // 2. Calculate distances and ramp angle
        float rampAngleRad = mezzanineRampAngleDegrees * Mathf.Deg2Rad;
        float verticalDistance = currentCourseHeight;
        if (Mathf.Sin(rampAngleRad) == 0) return;
        float distanceOnRamp = verticalDistance / Mathf.Sin(rampAngleRad);

        // 3. Calculate forces and pullers per block type
        float forceToApply10t = CalculatePullForce(10000, rampAngleRad, mezzanineFrictionCoef, out float rawForce10t);
        float forceToApply40t = CalculatePullForce(40000, rampAngleRad, mezzanineFrictionCoef, out float rawForce40t);
        float forceToApply50t = CalculatePullForce(50000, rampAngleRad, mezzanineFrictionCoef, out float rawForce50t);
        float forceToApply60t = CalculatePullForce(60000, rampAngleRad, mezzanineFrictionCoef, out float rawForce60t);
        float forceToApply70t = CalculatePullForce(70000, rampAngleRad, mezzanineFrictionCoef, out float rawForce70t);
        float forceToApply80t = CalculatePullForce(80000, rampAngleRad, mezzanineFrictionCoef, out float rawForce80t);

        // TotalPullers is the team size needed *without* a capstan
        totalPullers10t = CalculatePullers(rawForce10t);
        totalPullers40t = CalculatePullers(rawForce40t);
        totalPullers50t = CalculatePullers(rawForce50t);
        totalPullers60t = CalculatePullers(rawForce60t);
        totalPullers70t = CalculatePullers(rawForce70t);
        totalPullers80t = CalculatePullers(rawForce80t);

        // CapstanOperators is the smaller team applying the *reduced* force
        capstanOperators10t = CalculatePullers(forceToApply10t);
        capstanOperators40t = CalculatePullers(forceToApply40t);
        capstanOperators50t = CalculatePullers(forceToApply50t);
        capstanOperators60t = CalculatePullers(forceToApply60t);
        capstanOperators70t = CalculatePullers(forceToApply70t);
        capstanOperators80t = CalculatePullers(forceToApply80t);

        // 4. Calculate Work (Energy) for this course
        float work10t_MJ = CalculateWork(10000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks10t;
        float work40t_MJ = CalculateWork(40000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks40t;
        float work50t_MJ = CalculateWork(50000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks50t;
        float work60t_MJ = CalculateWork(60000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks60t;
        float work70t_MJ = CalculateWork(70000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks70t;
        float work80t_MJ = CalculateWork(80000, distanceOnRamp, rampAngleRad, mezzanineFrictionCoef) * blocks80t;
        float totalWorkThisCourse_MJ = work10t_MJ + work40t_MJ + work50t_MJ + work60t_MJ + work70t_MJ + work80t_MJ;

        // 5. Calculate Time for this course
        float setupTime_hours = 0;
        if (setupTimePerCourseGroups > 0)
        {
            setupTime_hours = setupTimePerCourseHours * setupTimePerCourseGroups * remainingPercentage;
            if (row > endCourseKingsChamber && row < endCourseKingsChamber)
                setupTime_hours = setupTimePerCourseHours * setupTimePerCourseGroups * remainingPercentageLimestone;
            if (setupTime_hours< setupTimePerCourseHours) {
                setupTime_hours = setupTimePerCourseHours;
            }
        }

        float timeOnRamp_sec = 0;
        if (pullingSpeedRampMetersPerSecond > 0)
        {
            timeOnRamp_sec = distanceOnRamp / pullingSpeedRampMetersPerSecond;
        }

        float timeOnTerrace_sec = 0;
        if (pullingSpeedTerraceMetersPerSecond > 0)
        {
            timeOnTerrace_sec = horizontalTransferDistanceMeters / pullingSpeedTerraceMetersPerSecond;
        }

        float timePerTrip_sec = timeOnRamp_sec + timeOnTerrace_sec;

        float totalDisplacementTime_hours = (timePerTrip_sec * totalBlocksThisCourse) / 3600f;
        float totalTimeThisCourse_hours = setupTime_hours + totalDisplacementTime_hours;

        // Calculate Working Years
        float totalTimeThisCourse_minutes = totalTimeThisCourse_hours * 60f;
        float totalTimeThisCourse_years = 0f;
        if (WorkingYearMinutes > 0)
        {
            totalTimeThisCourse_years = totalTimeThisCourse_minutes / WorkingYearMinutes;
        }

        // 6. Accumulate total time
        totalGraniteMoveTimeWorkingYears += totalTimeThisCourse_years;

        // 7. Log to CSV

        // Create formatted string for pullers: TotalPullers(CapstanOperators)xNumBlocks
        var pullerLogParts = new List<string>();

        // Add log part only if there are blocks of that type
        if (blocks10t > 0)
        {
            pullerLogParts.Add($"{totalPullers10t}({capstanOperators10t})x{blocks10t} 10t");
        }
        if (blocks40t > 0)
        {
            pullerLogParts.Add($"{totalPullers40t}({capstanOperators40t})x{blocks40t} 40t");
        }
        if (blocks50t > 0)
        {
            pullerLogParts.Add($"{totalPullers50t}({capstanOperators50t})x{blocks50t} 50t");
        }
        if (blocks60t > 0)
        {
            pullerLogParts.Add($"{totalPullers60t}({capstanOperators60t})x{blocks60t} 60t");
        }
        if (blocks70t > 0)
        {
            pullerLogParts.Add($"{totalPullers70t}({capstanOperators70t})x{blocks70t} 70t");
        }
        if (blocks80t > 0)
        {
            pullerLogParts.Add($"{totalPullers80t}({capstanOperators80t})x{blocks80t} 80t");
        }

        // Join the parts with " | "
        string pullersLogString = string.Join(" | ", pullerLogParts);

        if (showInfoGranite)
            csvgranitewriter.WriteLine(
            $"{row};" +
            $"{remainingPercentage:F1};" +
            $"{currentCourseHeight:F2};" +
            $"{mezzanineRampAngleDegrees:F1};" +
            $"{distanceOnRamp:F1};" +
            $"{horizontalTransferDistanceMeters:F1};" +
            $"{totalBlocksThisCourse};" +
            $"{totalDisplacementTime_hours:F2};" +
            $"{setupTime_hours:F2};" +
            $"{totalTimeThisCourse_hours:F2};" +
            $"{totalTimeThisCourse_years:F5};" + // UPDATED COLUMN
            $"{pullersLogString};" +
            $"{totalWorkThisCourse_MJ:F3}"
        );
    }

    /// <summary>
    /// Dynamically calculates the turning points (iterations) of the
    /// helical ramp based on the coupled geometry from S4 (Fig S4.1).
    /// h = (B * tan(a)) / (1 + (tan(a) / tan(b)))
    /// </summary>
    public List<TurningPoint> CalculateIERTurningPoints()
    {
        var turningPoints = new List<TurningPoint>();
        float currentHeight = 0f;
        int iteration = 1;

        // Convert angles from degrees to radians for trigonometric functions
        float rampAngleRad = this.RampInclination * Mathf.Deg2Rad; // Alpha (a)
        float pyramidAngleRad = this.PyramidInclination * Mathf.Deg2Rad; // Beta (b)

        // Pre-calculate tangents
        float tanRamp = Mathf.Tan(rampAngleRad);
        float tanPyramid = Mathf.Tan(pyramidAngleRad);

        // Error checking for invalid parameters
        if (tanRamp <= 0)
        {
            Debug.LogError("RampInclination must be positive to calculate turns.");
            return turningPoints; // Returns an empty list
        }
        if (tanPyramid <= 0)
        {
            Debug.LogError("PyramidInclination must be positive to calculate turns.");
            return turningPoints; // Returns an empty list
        }

        // Calculate the ratio term from the derived formula
        float tanRatio = tanRamp / tanPyramid;
        
        while (currentHeight < this.Height && iteration < MAX_TURNING_ITERATIONS)
        {
            // 1. Calculate B (currentBaseSize) at the currentHeight
            // B(h) = BaseSize * (1 - h / Height)
            float currentBaseSize = this.BaseSize * (1f - (currentHeight / this.Height));

            if (currentBaseSize < this.blockwide)
            {
                break; // Pyramid is too narrow to continue
            }

            // 2. Calculate verticalGain (h) using the correct formula from S4 
            // h = (B * tan(a)) / (1 + (tan(a) / tan(b)))
            float verticalGain = (currentBaseSize * tanRamp) / (1f + tanRatio);


            if (verticalGain < 0.001f)
            {
                break; // Insignificant gain, avoid infinite loop near apex
            }

            // 3. Calculate the new turning point height
            float newHeight = currentHeight + verticalGain;

            if (newHeight >= this.Height)
            {
                break; // The next turn would be above the apex
            }      
            
            // 4. Save this turning point
            int course = GetCourseAtHeight(newHeight);

            int blocksOnCourse = GetBlockCountForCourse(course);
            if (Math.Sqrt(blocksOnCourse) * blockwide < minBaseSize2Ramps / 2)
            {
                break; // maximum base size reached half for 2 ramps  
            }

            turningPoints.Add(new TurningPoint(iteration, newHeight, course, blocksOnCourse));

            // 5. Prepare the next iteration
            currentHeight = newHeight;
            iteration++;
        }

        return turningPoints;
    }

    /// <summary>
    /// Calculates the height (h) of a terrace based on the number of blocks
    /// it contains, using your formula N = (Blocks per Side)^2.
    /// </summary>
    public float FindHeightForTerraceBlockCount(int targetBlockCount)
    {
        // Based on your clarification: "the total blocks would be its square"
        // We invert this logic to find the blocks per side:
        float blocksPerSide = Mathf.Sqrt(targetBlockCount);

        // Calculate the length of that terrace side
        float targetSideLength = blocksPerSide * this.blockwide;

        // Invert the pyramid slope formula to find the height (h)
        // Side(h) = BaseSize * (1 - h / Height)
        //... solving for h:
        // h = Height * (1 - (Side(h) / BaseSize))
        float calculatedHeight = Height * (1f - (targetSideLength / BaseSize));

        return calculatedHeight;
    }

    /// <summary>
    /// (NEW) Gets the cumulative height from the base to the TOP of a given course number.
    /// Uses the discrete khufuCourseHeights array if enabled.
    /// </summary>
    public float GetHeightAtCourse(int course)
    {
        // Use 1-based indexing for safety
        if (course <= 0) return 0;

        if (!useKhufuCourseHeights || khufuCourseHeights == null || khufuCourseHeights.Count == 0)
        {
            // Fallback to average height
            return course * this.blockheight;
        }

        float cumulativeHeight = 0;
        // Course is 1-indexed, array is 0-indexed
        int targetIndex = course - 1;

        // Sum heights up to and including the target course
        for (int i = 0; i < khufuCourseHeights.Count && i <= targetIndex; i++)
        {
            cumulativeHeight += khufuCourseHeights[i];
        }

        // If course is out of bounds, return max height
        if (targetIndex >= khufuCourseHeights.Count)
        {
            return cumulativeHeight;
        }

        return cumulativeHeight;
    }

    /// <summary>
    /// (MODIFIED) Converts a height in meters to a course (row) number.
    /// Now supports discrete course heights via the khufuCourseHeights array.
    /// </summary>
    public int GetCourseAtHeight(float height)
    {
        if (!useKhufuCourseHeights || khufuCourseHeights == null || khufuCourseHeights.Count == 0)
        {
            // Original logic: Fallback to average height
            // Courses are 1-indexed
            return Mathf.FloorToInt(height / blockheight) + 1;
        }

        // (NEW) Discrete height logic
        float cumulativeHeight = 0;
        for (int i = 0; i < khufuCourseHeights.Count; i++)
        {
            cumulativeHeight += khufuCourseHeights[i];
            // If the cumulative height *at the end* of this course
            // is greater than or equal to the target height,
            // then this is the correct course.
            if (cumulativeHeight >= height)
            {
                return i + 1; // Course is 1-indexed
            }
        }

        // If height is greater than all courses, return the top course
        return khufuCourseHeights.Count;
    }

    /// <summary>
    /// Finds the nearest IER turning point (Iteration and Height)
    /// to a given targetHeight.
    /// </summary>
    public TurningPoint FindNearestIERTurn(float targetHeight)
    {
        // Use the _ierTurningNodes list that was dynamically calculated in Start()
        if (_ierTurningNodes == null || _ierTurningNodes.Count == 0)
        {
            // This should only happen if RampInclination is <= 0
            return default(TurningPoint); // Returns an empty (default) turn
        }

        // Find the nearest turn using Linq, comparing the
        // absolute height difference.
        TurningPoint nearestTurn = _ierTurningNodes
         .OrderBy(turn => Mathf.Abs(turn.Height - targetHeight))
         .First();

        return nearestTurn;
    }

    /// <summary>
    /// Calculates the total number of blocks on a specific course (terrace).
    /// This is the inverse function of FindHeightForTerraceBlockCount.
    /// </summary>
    public int GetBlockCountForCourse(int course)
    {
        // 1. Find the actual cumulative height at the top of this course
        // This respects the useKhufuCourseHeights setting
        float h = GetHeightAtCourse(course);

        // 2. Calculate the side length of the pyramid terrace at that height
        // Side(h) = BaseSize * (1 - h / Height)
        float terraceSideLength = this.BaseSize * (1f - (h / this.Height));

        // 3. Calculate how many blocks fit on one side
        float blocksPerSide = terraceSideLength / this.blockwide;

        // 4. Calculate total blocks (N = side * side), as per your formula
        float totalBlockCount = blocksPerSide * blocksPerSide;

        // Return the integer number of blocks
        return Mathf.RoundToInt(totalBlockCount);
    }

    public RampTargetMetrics CalculateRampTargetMetrics(int targetRow)
    {
        RampTargetMetrics result = new RampTargetMetrics();
        result.IsValid = false;

        if (targetRow < 0) return result;

        float currentBaseSize = BaseSize;
        float currentHeight = 0f;
        int currentRowIndex = 0; 
        int level = 0;           

        float rampTg = Mathf.Tan(RampInclination * Mathf.Deg2Rad);
        float pyrTg = Mathf.Tan(PyramidInclination * Mathf.Deg2Rad);

        int startFaceOffset = 0;
        switch (SingleRampFaceStart)
        {
            case RampPositionFace.NorthFace: startFaceOffset = 3; break;
            case RampPositionFace.WestFace: startFaceOffset = 0; break;
            case RampPositionFace.SouthFace: startFaceOffset = 1; break;
            case RampPositionFace.EastFace: startFaceOffset = 2; break;
        }

        while (currentHeight < Height)
        {
            float h_level_theoretical = currentBaseSize * rampTg * pyrTg / (rampTg + pyrTg);

            float h_level_accumulated = 0f;
            int coursesInThisLevel = 0;
            int tempRowCounter = currentRowIndex;

            while (true)
            {
                float nextCourseHeight = GetBlockHeightForRow(tempRowCounter);

                if ((h_level_accumulated + nextCourseHeight) > h_level_theoretical && h_level_accumulated > 0) break;
                if ((currentHeight + h_level_accumulated + nextCourseHeight) > Height) break;

                if (tempRowCounter == targetRow)
                {
                    result.Level = level;
                    result.Height = currentHeight + h_level_accumulated;

                    float sep_total_level = currentBaseSize * rampTg / (rampTg + pyrTg);
                    float sepi = sep_total_level * (h_level_accumulated / h_level_theoretical);

                    float bs2 = currentBaseSize / 2;

                    float h_local = h_level_accumulated;
                    Vector3 localPos = new Vector3(bs2 - sepi, h_local, -(bs2 - sepi));

                    int effectiveFaceIndex = (level + startFaceOffset) % 4;

                    result.FaceIndex = effectiveFaceIndex;
                    Quaternion rotation = Quaternion.identity;

                    switch (effectiveFaceIndex)
                    {
                        case 0:
                            result.FaceName = "North";
                            rotation = Quaternion.Euler(0, 0, 0);
                            break;
                        case 1:
                            result.FaceName = "East";
                            rotation = Quaternion.Euler(0, 90, 0);
                            break;
                        case 2:
                            result.FaceName = "South";
                            rotation = Quaternion.Euler(0, 180, 0);
                            break;
                        case 3:
                            result.FaceName = "West";
                            rotation = Quaternion.Euler(0, 270, 0);
                            break;
                    }

                    Vector3 flatLocalPos = new Vector3(localPos.x, 0, localPos.z);
                    Vector3 rotatedPos = rotation * flatLocalPos;
                    rotatedPos.y = result.Height; // height

                    result.Position = rotatedPos;

                    if (objParent != null)
                    {
                        result.Position += objParent.transform.position;
                    }

                    result.IsValid = true;
                    return result;
                }

                h_level_accumulated += nextCourseHeight;
                coursesInThisLevel++;
                tempRowCounter++;
            }

            // Next level
            float sep_level_final = currentBaseSize * rampTg / (rampTg + pyrTg);
            currentBaseSize -= (2 * PathWide + 2 * PathSeparation + 2 * sep_level_final);
            currentHeight += h_level_accumulated;
            currentRowIndex += coursesInThisLevel;
            level++;

            if (currentBaseSize <= 0 || coursesInThisLevel == 0) break;
        }

        return result;
    }

    /// <summary>
    /// Calculates a position at a specific distance from the center (origin) that lies on the line
    /// connecting the center and a target point.
    /// Calcula una posición a una distancia específica del centro que pasa por la línea
    /// que une el centro y el punto objetivo.
    /// </summary>
    /// <param name="targetPoint">The point that defines the direction vector from the center.</param>
    /// <param name="distanceFromCenter">The desired distance from the center (0,0,0).</param>
    /// <returns>The calculated position Vector3.</returns>
    public Vector3 GetTargetPositionFromCenter(Vector3 targetPoint, float distanceFromCenter)
    {
        // Asumimos que el "centro" de la pirámide es la posición del objeto padre (objParent)
        // o el origen (0,0,0) si queremos ser puramente locales.
        // Dado que CalculateRampTargetMetrics devuelve coordenadas MUNDIALES (si objParent != null),
        // usaremos la posición del objParent como el "centro" del universo de la pirámide.

        Vector3 center = (objParent != null) ? objParent.transform.position : Vector3.zero;

        // 1. Calcular la dirección desde el centro hacia el punto objetivo
        Vector3 direction = (targetPoint - center).normalized;

        // 2. Calcular el nuevo punto a la distancia deseada
        Vector3 resultPosition = center + (direction * distanceFromCenter);

        return resultPosition;
    }

    /// <summary>
    /// Moves the camera in an orbit around the pyramid.
    /// </summary>
    private IEnumerator OrbitCameraAroundPyramid()
    {
        Debug.Log("Starting camera orbit...");

        Vector3 centerPoint = objParent.transform.position + new Vector3(0, Height / 3f, 0); 

        float maxDimension = Mathf.Max(BaseSize, Height);
        float distance = maxDimension * camOrbitDistanceFactor;
        float heightOffset = Height * camOrbitHeightOffsetFactor; 

        Vector3 startOffset = new Vector3(0, heightOffset, -distance);
        cam.transform.position = centerPoint + startOffset;
        cam.transform.LookAt(centerPoint);

        while (OrbitCameraOnFinish)
        {
            cam.transform.RotateAround(centerPoint, Vector3.up, OrbitSpeed * Time.deltaTime);

            cam.transform.LookAt(centerPoint);

            yield return null; 
        }
    }

    // Get a random position on the terrace for block placement
    private Vector3 GetRandomPositionOnTerrace(int row, float currentBaseSize, float currentHeight, float blockHeightY)
    {
        float offsetX = 0;
        float offsetZ = 0;
        float halfBase = currentBaseSize / 4; // Range within quarter of the base size

        if (row % 2 == 0)
        {
            offsetX = UnityEngine.Random.Range(horizontalTransferDistanceMeters, halfBase);
            offsetZ = UnityEngine.Random.Range(horizontalTransferDistanceMeters, halfBase);
        }
        else 
        {
            offsetX = UnityEngine.Random.Range(horizontalTransferDistanceMeters, halfBase);
            offsetZ = -UnityEngine.Random.Range(horizontalTransferDistanceMeters, halfBase);
        }

        Vector3 localPos = new Vector3(offsetX, currentHeight + graniteRockPrefab1.transform.localScale.y / 2, offsetZ); 

        localPos.y = currentHeight + 0.75f + blockHeightY / 2;

        return objParent.transform.position + localPos;
    }

}
