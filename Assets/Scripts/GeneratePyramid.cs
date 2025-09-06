using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using UMA;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;
using static UnityEngine.UI.GridLayoutGroup;

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
    Four_Ramp,
    Adaptative
}

public class GeneratePyramid : MonoBehaviour
{
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
    public int holeHeight = 3;
    /// <summary>
    /// The width of the ramp's passage in block units.
    /// </summary>
    public int holeWide = 3;
    /// <summary>
    /// The separation between individual blocks for visual clarity.
    /// </summary>
    public float blockSeparation = 0.01f; // separation between blocks

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
    /// <summary>
    /// Prefab for the pyramidion (capstone).
    /// </summary>
    public GameObject piramidon;
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
    /// Toggles drawing the outer casing stones.
    /// </summary>
    public bool DrawCover = false;
    /// <summary>
    /// The specific row to draw for DrawUntilRow or DrawOnlyRow.
    /// </summary>
    public int DrawRow = 0;
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
    /// Number of granite blocks of type 1.
    /// </summary>
    public int numOfGraniteRock1 = 0;
    /// <summary>
    /// Number of granite blocks of type 2.
    /// </summary>
    public int numOfGraniteRock2 = 0;
    /// <summary>
    /// Minimum height (in meters) to start placing granite blocks.
    /// </summary>
    public int minHeightGraniteRock = 43;
    /// <summary>
    /// Maximum height to place granite blocks.
    /// </summary>
    public int maxHeightGraniteRock = 62;
    /// <summary>
    /// Minimum base size to use a 2-ramp system.
    /// </summary>
    public int minBaseSize2Ramps = 32;
    /// <summary>
    /// Minimum base size to use a 4-ramp system.
    /// </summary>
    public int minBaseSize4Ramps = 64;
    /// <summary>
    /// Minimum base size to use an 8-ramp system.
    /// </summary>
    public int minBaseSize8Ramps = 128;
    /// <summary>
    /// Minimum base size to use a 16-ramp system.
    /// </summary>
    public int minBaseSize16Ramps = 200;
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
    /// Sequenced 
    /// </summary>
    public Boolean ShowGUI = true;

    private float pyramid_inclination_tg = 0;
    private float ramp_inclination_tg;   
    private float ramp_inclination_atg;
    private float ramp_total_length=0;
    private float g = 9.80665f;
    private string dir;   
    private string textPath;

    private float x;
    private float z;

    private StreamWriter writer;
    private StreamWriter csviterwriter;
    private StreamWriter csvrowwriter;
    private StreamWriter csvheadwaywriter;

    private int indexblock = 0;
    private int lastLevel = 0;
    private int lastLevelBlocks = 0;
    private int numberOfBlocksFinish = 0;

    private List<GameObject> blocksMidle;
    private List<GameObject> blocksMidle2;    

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

    // Start is called before the first frame update
    void Start()
    {
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
            cam.transform.localPosition = new Vector3(-BaseSize * 4 / 5, Height * 3 / 4, -BaseSize * 4 / 5);
            //cam.transform.localPosition = new Vector3(BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, -BaseSize);
            cam.transform.LookAt(new Vector3(0, Height / 12, 0));
        }

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
            csvrowwriter.WriteLine("Row;blocks;ramp inclination;Ramp length (m);Ramp length total (m);distance blocks (Km);distance blocks Ramp (Km);distance blocks Horiz (Km);Sum force blocks (GJ);Sum Vert. force blocks (GJ);Sum Horiz. force blocks (GJ);Vert. force blocks row (GJ);Horiz. force blocks row (GJ);Total force blocks row (GJ);% Decrement blocks;% increase Distance;% increase Force");

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
        compute_size();
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

        // half pyramid
        if (halfPyramid)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "HalfPyramidCut";
            cube.transform.position = objParent.transform.position + new Vector3(BaseSize/2, Height / 2, 0);
            cube.transform.localScale = new Vector3(BaseSize, Height, BaseSize);
            cube.isStatic = true;
            cube.AddComponent<DeleteObject>();
            cube.GetComponent<DeleteObject>().CommonGameObject=objParent;
            cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube.GetComponent<MeshRenderer>().enabled = false;            
            cube.GetComponent<BoxCollider>().isTrigger = true;
        }

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
        Start();
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

    public void compute_size()
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
        path_length = compute_size_level(0, BaseSize, PathWide, PathSeparation, 0, 
                                         0, 0, 0, 0, 0, 0, 0, 0);
        if (showInfoLevel || showInfoLevelDec || showInfoLevelTotal || showInfoRow)
        {
            Debug.Log("Total length : " + path_length + ", Total block distance : " + totalLength + ", Total block force : " + totalForce + ", Total block force ramp : " + totalForceRamp + ", % force ramp : " + totalForceRamp * 100 / totalForce);
            writer.WriteLine("Total length : " + path_length + ", Total block distance : " + totalLength + ", Total block force : " + totalForce + ", Total block force ramp : " + totalForceRamp + ", % force ramp : " + totalForceRamp * 100 / totalForce);
        }
    }

    private float compute_size_level(int level, float base_size, float path_wide, float separation, float height, 
            float old_length, float beforeBlocks, float beforeDistance, float beforeForce, 
            float force_old_length, float force_old_vert, float force_old_horiz, 
            int row)
    {
        if (DrawUntilRow && row > DrawRow)
        {            
            return 0;
        }

        if (height > Height)
        {
            Debug.Log("Good solution! Total height: " + total_height);
            writer.WriteLine("Total height: " + total_height);
            return 0;
        }

        //float h = base_size * ramp_inclination_tg;  // height
        float h = base_size * ramp_inclination_tg * pyramid_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        // divide by height of block
        int ch = Mathf.RoundToInt(h / blockheight);
        h = ch * blockheight; // adjust
        //float sep = h / pyramid_inclination_tg; // separation
        float sep = base_size * ramp_inclination_tg / (ramp_inclination_tg + pyramid_inclination_tg);
        total_height += h;
        float heightGranite = 0;

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

        if (h < 0.524f)
        {
            if (height + h > Height)
                Debug.Log("Good solution! Total height: " + total_height);
            else
                Debug.Log("Bad solution! Total height: " + total_height);
            if (showInfoLevel)
                writer.WriteLine("Total height: " + total_height);
            return 0;
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
            return 0;
        }

        float bs2 = base_size / 2;
        Vector3 v0 = new Vector3(bs2, 0, bs2);
        Vector3 v1 = new Vector3(bs2 - sep, h, -(bs2 - sep));
        //float length = Mathf.Sqrt(new_base_size * new_base_size + h * h);
        float length = Vector3.Distance(v0, v1);

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
        float bh2 = blockheight / 2;
        float b1 = (base_size - sep) / ch;
        float bht2 = bh2 / pyramid_inclination_tg;
        float bhtl = Mathf.Sqrt(blockheight * blockheight + bht2 * bht2)+0.3f;       
        int biter = 0;
        int blockant = 0;
        int num_block_real = 0;
        float distant = 0;
        float forceant = 0;
        float inclirampant = 0;
        float forceblocksrow_horiz_total = force_old_horiz;
        float forceblocksrow_vert_total = force_old_vert;
        float forceblocksrow_total_total = force_old_length;
        for (int i = 0; i < ch; i++)
        {
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
            v0 = new Vector3(bs2, 0, bs2);
            if (optionRamp == 0)
            {
                v1 = new Vector3(bs2 - sepi, i * blockheight, -(bs2 - sepi));
                distramprow = Vector3.Distance(v0, v1);
                incliramprow = Mathf.Atan(i * blockheight / (base_size - sepi));
                forceramprow = (old_length + distramprow) * massBlock * g * (Mathf.Sin(incliramprow) + frictionCoef * Mathf.Cos(incliramprow));
            }
            else
            {
                v1 = new Vector3(bs2 - sep * (i + 1) / ch, i * blockheight, bs2 - b1 * i);
                if (i > 0)
                {
                    distramprow = Vector3.Distance(v0, v1);
                    incliramprow = Mathf.Atan(i * blockheight / distramprow);
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
            last_h = i * blockheight;
            last_v0 = v0;
            last_v1 = v1;
            numberOfBlocksX = 0;
            lastNumberOfBlockDrawnX = -1;
            GameObject[] createdObjectsArray = new GameObject[(int) (base_size / blockwide)+1];
            x = -bs2 + sepi + bw2;
            v0 = new Vector3(bs2 - sepi, i * blockheight, -(bs2 - sepi));
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
                            /*if (i==0)
                                obj = Instantiate(RockDivPrefab, new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                            else*/
                            obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                            obj.transform.localScale = new Vector3(blockwide - blockSeparation, blockheight, blockwide - blockSeparation);
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
                                    if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, blockheight, blockLayer))
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
                            large_cube.transform.localScale = new Vector3(blockwide-blockSeparation, blockheight, distance - blockwide - blockSeparation);
                            large_cube.GetComponent<MeshRenderer>().material = m_Material;
                            large_cube.tag = "Block";
                            large_cube.isStatic = isStatic || row == 0;
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
                                    if (Physics.Raycast(large_cube.transform.position, Vector3.down, out hit, blockheight , blockLayer))
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
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x - bw2 / 2, height + bh2 + i * blockheight, z), Quaternion.identity);
                                obj.transform.localScale = new Vector3(0.5f * blockwide, blockheight, blockwide);
                            }
                            else
                            if (x > bs2 - sepi - blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x + bw2 / 2, height + bh2 + i * blockheight, z), Quaternion.identity);
                                obj.transform.localScale = new Vector3(0.5f * blockwide, blockheight, blockwide);
                            }
                            else
                            if (z < -bs2 + sepi + blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z - bw2 / 2), Quaternion.identity);
                                obj.transform.localScale = new Vector3(blockwide, blockheight, 0.5f * blockwide);
                            }
                            else
                            if (z > bs2 - sepi - blockwide)
                            {
                                obj = Instantiate(RockPrefab[rnd], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z + bw2 / 2), Quaternion.identity);
                                obj.transform.localScale = new Vector3(blockwide, blockheight, 0.5f * blockwide);
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
                        if (DrawCover && (!halfPyramid || x < 0) && ((x < -bs2 + sepi + blockwide) || (z < -bs2 + sepi + blockwide)))
                        {
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            if (x < -bs2 + sepi + blockwide)
                            {
                                cube.transform.position = objParent.transform.position + new Vector3(x - bw2 - bht2, height + bh2 + i * blockheight, z);
                                cube.transform.localScale = new Vector3(0.1f, bhtl, blockwide);
                                cube.transform.rotation = Quaternion.Euler(0, 0, -(90 - PyramidInclination));
                            }
                            else
                            if (z < -bs2 + sepi + blockwide)
                            {
                                cube.transform.position = objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z - bw2 - bht2);
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
                        }
                    }
                    z += blockwide;
                    numberOfBlocks++;
                    bxi++;
                    biter++;
                    v0 = new Vector3(x, i * blockheight, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal
                    distblocksrow += old_length + distramprow + dist_horiz;   // distance ramp before + distance ramp + distance block
                    distblocksramprow += old_length + distramprow;                          // distance ramp before + distance ramp
                    forceblocksrow_horiz += dist_horiz * frictionCoef * massBlock * g;    // force horizontal row
                    forceblocksrow_vert += forceramprow;                                                // force vertical row
                    forceblocksrow_horiz_total += dist_horiz * frictionCoef * massBlock * g; // force row total
                    forceblocksrow_vert_total += forceramprow;
                    forceblocksrow_total += forceramprow + dist_horiz * frictionCoef * massBlock * g;
                    forceblocksrow += force_old_length + forceramprow + dist_horiz * frictionCoef * massBlock * g;
                    forceblocksramprow += forceramprow;
                    forceblocksrow_total_total += forceramprow + dist_horiz * frictionCoef * massBlock * g;

                    if (DrawUntilRow && row == DrawRow)
                        heightGranite = height + bh2 + i * blockheight;
                    // save the object in the array for later use
                    createdObjectsArray[numberOfBlocksZ] = obj;

                    if (maxBlocks > 0 && numberOfBlocks > maxBlocks) break;
                }
                // last block Z
                if ((!DrawOnlyRow || row == DrawRow) && (z != bs2 - sepi) && (!halfPyramid || x < 0))
                {
                    // adapt block size
                    scaleChange = new Vector3(blockwide- blockSeparation, blockheight, blockwide - blockSeparation);
                    scaleChange.z = bs2 - sepi - (z - bw2);
                    z = z - (blockwide - scaleChange.z) / 2;
                    /*if (i == 0)
                        obj = Instantiate(RockDivPrefab, new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                    else*/
                    obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
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
                            if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, blockheight, blockLayer))
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
                    if (DrawCover)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z + scaleChange.z / 2 + bht2);
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
                    }
                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + scaleChange.z / blockwide;
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, i * blockheight, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal
                    distblocksrow += old_length + distramprow + dist_horiz;
                    distblocksramprow += old_length + distramprow;
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
                scaleChange = new Vector3(blockwide - blockSeparation, blockheight, blockwide - blockSeparation);
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
                        obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
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
                                if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, blockheight, blockLayer))
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
                        if (DrawCover)
                        {
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            cube.transform.position = objParent.transform.position + new Vector3(x + scaleChange.x / 2 + bht2, height + bh2 + i * blockheight, z);
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
                        }
                    }
                    z += blockwide;
                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + scaleChange.x / blockwide;
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, i * blockheight, z);
                    var dist_horiz = Vector3.Distance(v0, v1);                              // distance horizontal
                    distblocksrow += old_length + distramprow + dist_horiz;
                    distblocksramprow += old_length + distramprow;
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
                    /*if (i == 0)
                        obj = Instantiate(RockDivPrefab, new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
                    else*/
                    obj = Instantiate(RockPrefab[UnityEngine.Random.Range(0, RockPrefab.Length)], objParent.transform.position + new Vector3(x, height + bh2 + i * blockheight, z), Quaternion.identity);
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
                            if (Physics.Raycast(obj.transform.position, Vector3.down, out hit, blockheight, blockLayer))
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
                    if (DrawCover)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.position = objParent.transform.position + new Vector3(x + scaleChange.x / 2 + bht2, height + bh2 + i * blockheight, z);
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
                    }

                    //numberOfBlocks++;                    
                    //bxi++;
                    //biter++;
                    num_block_real++;
                    blocksfraction = blocksfraction + (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    var blockscale = (scaleChange.z + scaleChange.x) / (2 * blockwide);
                    v0 = new Vector3(x, i * blockheight, z);
                    var dist_horiz = Vector3.Distance(v0, v1);
                    distblocksrow += old_length + distramprow + dist_horiz;
                    distblocksramprow += old_length + distramprow;
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
                    if (!Method4Ramp)
                        num_ramps_headways = 1;

                    float current_headway = MinHeadway + (old_length + distramprow) / ramp_total_length * (MaxHeadway - MinHeadway);
                    float bxi_ramp = Mathf.Round(bxi / num_ramps_headways);
                    // Row;blocks;up ramps;blocks per ramp;fixed headway(min);adaptative headway(min);total time(min);adaptative total time(min);;total time(working years);adaptativive total time(working years)                        
                    csvheadwaywriter.WriteLine(i + ";" + bxi + ";" + num_ramps_headways+ ";"+ bxi_ramp + ";" + AverageHeadway.ToString("F2") + ";" + current_headway.ToString("F2") + ";" + (bxi_ramp * AverageHeadway).ToString("F2") + ";" + (bxi_ramp * current_headway).ToString("F2") + ";" + (bxi_ramp * AverageHeadway / WorkingYearMinutes).ToString("F5") + ";" + (bxi_ramp * current_headway / WorkingYearMinutes).ToString("F5"));
                }

                if (blockant > 0)
                {
                    Debug.Log("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km : " + (distblocksrow/1000).ToString("F3") + ", force blocks (GJ): " + (forceblocksrow/1000000).ToString("F3") + ", Decrement - blocks : " + (bxi * 100 / blockant).ToString("F2") + " %, Distance : " + (distblocksrow * 100 / distant).ToString("F2") + " %, Force : " + (forceblocksrow * 100 / forceant).ToString("F2") + " %");
                    if (showInfoLevel)
                        writer.WriteLine("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km: " + (distblocksrow/1000).ToString("F3") + ", force blocks (GJ): " + (forceblocksrow/10000000).ToString("F3") + ", Decrement - blocks : " + (bxi * 100 / blockant).ToString("F2") + " %, Distance : " + (distblocksrow * 100 / distant).ToString("F2") + " %, Force : " + (forceblocksrow * 100 / forceant).ToString("F2") + " %");
                    if (showInfoRow)
                    {
                        // Row;blocks;ramp inclination;Ramp length (m);Ramp length total (m);distance blocks (Km);distance blocks Ramp (Km);distance blocks Horiz (Km);Sum force blocks (GJ);Sum Vert. force blocks (GJ);Sum Horiz. force blocks (GJ);Vert. force blocks row (GJ);Horiz. force blocks row (GJ);Total force blocks row (GJ);% Decrement blocks;% increase Distance;% increase Force
                        csvrowwriter.WriteLine(i + ";" + bxi + ";" + radians_to_degrees(incliramprow).ToString("F2") + ";" + distramprow.ToString("F2") + ";" + (old_length + distramprow).ToString("F2") + ";" + (distblocksrow / 1000).ToString("F3") + ";" + (distblocksramprow / 1000).ToString("F3") + ";" + ((distblocksrow - distblocksramprow) / 1000).ToString("F3") + ";" + (forceblocksrow_total_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert_total / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz / 1000000).ToString("F3") + ";" + (forceblocksrow_total / 1000000).ToString("F3") + ";" + bxi * 100 / blockant + ";" + (distblocksrow * 100 / distant).ToString("F2") + ";" + (forceblocksrow * 100 / forceant).ToString("F2"));                        
                    }
                }
                else
                {
                    Debug.Log("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow).ToString("F2") + ", Length ramp : " + distramprow.ToString("F2") + ", distance blocks Km: " + (distblocksrow/1000).ToString("F3") + ", force blocks (GJ): " + (forceblocksrow/1000000).ToString("F2"));
                    if (showInfoLevel) 
                        writer.WriteLine("  Row : " + i + ", blocks : " + bxi + ", ramp inclination : " + radians_to_degrees(incliramprow) + ", Length ramp : " + distramprow + ", distance blocks Km: " + (distblocksrow / 1000).ToString("F3") + ", force blocks : (GJ): " + (forceblocksrow / 1000000).ToString("F2"));
                    if (showInfoRow)
                        csvrowwriter.WriteLine(i + ";" + bxi + ";" + RampInclination.ToString("F2") + ";" + distramprow.ToString("F2") + ";" + (old_length + distramprow).ToString("F2") + ";" + (distblocksrow / 1000).ToString("F3") + ";" + (distblocksramprow / 1000).ToString("F3") + ";" + ((distblocksrow - distblocksramprow) / 1000).ToString("F3") + ";" + (forceblocksrow_total_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert_total / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz_total / 1000000).ToString("F3") + ";" + (forceblocksrow_vert / 1000000).ToString("F3") + ";" + (forceblocksrow_horiz / 1000000).ToString("F3") + ";" + (forceblocksrow_total / 1000000).ToString("F3"));
                }

            }

            // corners
            if (DrawCover)
            {
                // corner 1                        
                GameObject corner1 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner1.transform.position = objParent.transform.position + new Vector3(-bs2 + sepi - bht2, height + bh2 + i * blockheight, -bs2 + sepi - bht2);
                corner1.transform.rotation = Quaternion.Euler(0, 90, 0);
                /*if (objParent)
                    corner1.transform.parent = objParent.transform;*/
                corner1.transform.parent = row_gameObject.transform;
                // corner 2                        
                GameObject corner2 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner2.transform.position = objParent.transform.position + new Vector3(bs2 - sepi + bht2, height + bh2 + i * blockheight, -bs2 + sepi - bht2);
                corner2.transform.rotation = Quaternion.Euler(0, 0, 0);
                /*if (objParent)
                    corner2.transform.parent = objParent.transform;*/
                corner2.transform.parent = row_gameObject.transform;
                // corner 3                       
                GameObject corner3 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner3.transform.position = objParent.transform.position + new Vector3(bs2 - sepi + bht2, height + bh2 + i * blockheight, bs2 - sepi + bht2);
                corner3.transform.rotation = Quaternion.Euler(0, 270, 0);
                /*if (objParent)
                    corner3.transform.parent = objParent.transform;*/
                corner3.transform.parent = row_gameObject.transform;
                // corner 4                        
                GameObject corner4 = Instantiate(CornerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                corner4.transform.position = objParent.transform.position + new Vector3(-bs2 + sepi - bht2, height + bh2 + i * blockheight, bs2 - sepi + bht2);
                corner4.transform.rotation = Quaternion.Euler(0, 180, 0);
                /*if (objParent)
                    corner4.transform.parent = objParent.transform;*/
                corner4.transform.parent = row_gameObject.transform;
            }

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

        if (showRamps)
        {
            // draw ramps
            if (Method4Ramp && minBaseSize2Ramps < base_size)
            {
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
                    DrawRamps(level, base_size - 2 * blockwide, height, h, sep, length - blockwide,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
                else
                    DrawRamps(level, base_size, height, h, sep, length,
                            row, last_sepi, last_length, last_h, last_v0, last_v1);
            }
        }

        // show granite block King's Chamber
        if (DrawGranite && DrawUntilRow && (row + 1 > DrawRow) && !exportPyramidObj && !isRigidBody)
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
                        if (row % 4 == 0)
                        {
                            objGranite = Instantiate(graniteRockPrefab1, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 1)
                        {
                            objGranite = Instantiate(graniteRockPrefab1, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 2)
                        {
                            objGranite = Instantiate(graniteRockPrefab1, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 3)
                        {
                            objGranite = Instantiate(graniteRockPrefab1, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        //objGranite.transform.parent = objParent.transform;
                        objGranite.transform.parent = granite_gameObject.transform;
                    }
                }
                if (numOfGraniteRock2Def > 0 && graniteRockPrefab2)
                {
                    for (int i = 0; i < numOfGraniteRock2Def; i++)
                    {
                        GameObject objGranite = null;
                        if (row % 4 == 0)
                        {
                            objGranite = Instantiate(graniteRockPrefab2, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 1)
                        {
                            objGranite = Instantiate(graniteRockPrefab2, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 2)
                        {
                            objGranite = Instantiate(graniteRockPrefab2, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        else
                        if (row % 4 == 3)
                        {
                            objGranite = Instantiate(graniteRockPrefab1, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + graniteRockPrefab1.transform.localScale.y / 2, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);                            
                        }
                        //objGranite.transform.parent = objParent.transform;
                        objGranite.transform.parent = granite_gameObject.transform;
                    }
                }
            }
            if (piramidon)
            {
                if (row % 4 == 0)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + 1, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }
                else
                        if (row % 4 == 1)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + 1, UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }
                else
                        if (row % 4 == 2)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(-UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + 1, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }
                else
                        if (row % 4 == 3)
                {
                    GameObject objPiramidon = Instantiate(piramidon, objParent.transform.position + new Vector3(UnityEngine.Random.Range(0, new_base_size / 4), heightGranite + 1, -UnityEngine.Random.Range(0, new_base_size / 4)), Quaternion.identity);
                    //objPiramidon.transform.parent = objParent.transform;
                    objPiramidon.transform.parent = granite_gameObject.transform;
                    objPiramidon.transform.rotation = Quaternion.Euler(275, 0, 0);
                }                
            }
        }

        force_old_length += forceblocksrow_total_total;
        force_old_horiz += forceblocksrow_horiz_total;
        force_old_vert += forceblocksrow_vert_total;        

        //return length;
        if (maxBlocks > 0 && numberOfBlocks > maxBlocks)
            return length;
        else
            return length + compute_size_level(level + 1, new_base_size, path_wide, separation, height + h,
                                            old_length + length, biter, distblocks, forceblocks, force_old_length, force_old_vert, force_old_horiz, row);
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
        if (DrawUntilRow && row > DrawRow)
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
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Ramp_" + level + "_" + row;
        if (level % 4 == 0)
        {
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2, h / 2 + height + blockheight * holeHeight / 2, sep / 2);
            cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
        }
        else
        if (level % 4 == 1)
        {
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight / 2, -(base_size - sep) / 2);
            cube.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
        }
        else
        if (level % 4 == 2)
        {
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
            cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
        }
        else
        if (level % 4 == 3)
        {
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight / 2, (base_size - sep) / 2);
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

        //cube.transform.parent = objParent.transform;
        cube.transform.parent = ramp_gameObject.transform;
        cube.isStatic = true;
        cube.AddComponent<DeleteObject>();
        cube.GetComponent<DeleteObject>().generatePyramid = this;
        cube.GetComponent<DeleteObject>().CommonGameObject = objParent;
        cube.GetComponent<MeshRenderer>().enabled = false;
        //cube.GetComponent<ShowHideObject>().hide = true;
        cube.GetComponent<BoxCollider>().isTrigger = true;

        // ramp corner wide
        // not remove blocks in the first level
        if (level > 0)
        {
            GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube_c.name = "Ramp-corner_" + level + "_" + row;
            if (level % 4 == 0)
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
            else
            if (level % 4 == 1)
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
            else
            if (level % 4 == 2)
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
            else
            if (level % 4 == 3)
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
            cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
            //cube_c.transform.parent = objParent.transform;
            cube_c.transform.parent = ramp_gameObject.transform;
            cube_c.isStatic = true;
            cube_c.AddComponent<DeleteObject>();
            cube_c.GetComponent<DeleteObject>().generatePyramid = this;
            cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube_c.GetComponent<MeshRenderer>().enabled = false;
            cube_c.GetComponent<BoxCollider>().isTrigger = true;
        }

        // ramp floor
        if (DrawFloor)
        {
            GameObject cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "Ramp_floor_" + level + "_" + row;
            if (level % 4 == 0)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-1, height - bh2, 0);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - 1, h / 2 + height - bh2, sep / 2);
                cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            }
            else
            if (level % 4 == 1)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height - bh2, 1);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + 1);
                cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 2)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(1, height - bh2, 0);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + 1, h / 2 + height - bh2, -sep / 2);
                cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 3)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height - bh2, -1);
                else
                    cubefloor.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - 1);
                cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            }
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            else
                cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
        }

        if (DrawWall)
        {
            // ramp wall
            GameObject cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "Ramp_wall_" + level + "_" + row;

            if (level % 4 == 0)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-holeWide * blockwide / 2, height + blockheight * holeHeight, 0);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - holeWide * blockwide / 2, h / 2 + height + blockheight * holeHeight, sep / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            }
            else
            if (level % 4 == 1)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight, holeWide * blockwide / 2);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight, -(base_size - sep) / 2 + holeWide * blockwide / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 2)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(2 * blockwide, height + blockheight * holeHeight, 0);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + holeWide * blockwide / 2, h / 2 + height + blockheight * holeHeight, -sep / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            }
            else
            if (level % 4 == 3)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight, -holeWide * blockwide / 2);
                else
                    cubewall.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight, (base_size - sep) / 2 - holeWide * blockwide / 2);
                cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            }
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = objParent.transform.position + new Vector3(last_length - 8 * blockwide, blockheight * 9, 0.1f);
                else
                    cubewall.transform.localScale = objParent.transform.position + new Vector3(length - 8 * blockwide, blockheight * 9, 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 7 * blockwide, blockheight * holeHeight * 2, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * holeHeight * 2, 0.1f);
            }
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
        }

        // corner floor
        if (DrawFloor)
        {
            GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner.name = "Ramp_corner_" + level + "_" + row;
            if (level % 4 == 0)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide * 5 / 6), height, (base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, -27.0f, -32.0f);
            }
            else
            if (level % 4 == 1)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide * 5 / 6), height, -(base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, 53.0f, 48.0f);
            }
            else
            if (level % 4 == 2)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide * 5 / 6), height, -(base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, -42.0f, -48.0f);
            }
            else
            if (level % 4 == 3)
            {
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide * 5 / 6), height, (base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, 32.0f, 27.0f);
            }
            cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
            //cubecorner.transform.parent = objParent.transform;
            cubecorner.transform.parent = ramp_gameObject.transform;
            cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner.isStatic = true;
        }

        // corner wall
        if (DrawWall)
        {
            GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row;
            if (level % 4 == 0)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeWide * blockwide, height + blockheight * holeHeight, (base_size / 2) - holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, 60.0f, 0.0f);
            }
            else
            if (level % 4 == 1)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeWide * blockwide, height + blockheight * holeHeight, -(base_size / 2) + holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, -30.0f, 0.0f);
            }
            else
            if (level % 4 == 2)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeWide * blockwide, height + blockheight * holeHeight, -(base_size / 2) + holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, 60.0f, 0.0f);
            }
            else
            if (level % 4 == 3)
            {
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeWide * blockwide, height + blockheight * holeHeight, (base_size / 2) - holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, -30.0f, 0.0f);
            }
            cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight * 2, 0.1f);
            //cubecorner_wall.transform.parent = objParent.transform;
            cubecorner_wall.transform.parent = ramp_gameObject.transform;
            cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner_wall.isStatic = true;
        }

        // wooden cylinder
        if (DrawWoodenCyl && !exportPyramidObj)
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
        if (DrawEgyptians && stone_sled && height<Height*0.9f && !exportPyramidObj)
        {
            GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
            stone_sled1.name = "stone_sled_" + level + "_" + row;
            if (level % 4 == 0)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 1.0f * blockwide), height + 0.75f, (base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 95.0f, 7.0f);
            }
            else
            if (level % 4 == 1)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 2.5f * blockwide), height + 0.75f, -(base_size / 2 - 1.0f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 7.0f, -7.0f);
            }
            else
            if (level % 4 == 2)
            {
                stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - 1.0f * blockwide), height + 0.75f, -(base_size / 2 - 2.5f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 95.0f, -7.0f);
            }
            else
            if (level % 4 == 3)
            {
                stone_sled1.transform.position = new Vector3(-(base_size / 2 - 2.5f * blockwide), height + 0.75f, (base_size / 2 - 1.0f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 7.0f, 7.0f);
            }
            //stone_sled1.transform.parent = objParent.transform;
            stone_sled1.transform.parent = workers_gameObject.transform;
            stone_sled1.isStatic = true;
        }

        // egyptians
        if (DrawEgyptians && Egyptian_body && height < Height * 0.9f && !exportPyramidObj)
        {


            for (int i = 0; i < 12; i++)
            {
                // left hand
                GameObject Egyptian = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian.name = "Egyptian_left_" + level + "_" + row+"_"+i;
                if (level % 4 == 0)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (0.75f + 0.1f*i) * blockwide), height + 2.25f + 0.16f*i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 7.0f, 0.0f);
                }
                else
                if (level % 4 == 1)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (0.75f + 0.1f * i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 97.0f, 0.0f);
                }
                else
                if (level % 4 == 2)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (0.75f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 187.0f, 0.0f);
                }
                else
                if (level % 4 == 3)
                {
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (0.75f + 0.1f * i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, -75.0f, 0.0f);
                }
                //Egyptian.transform.parent = objParent.transform;
                Egyptian.transform.parent = workers_gameObject.transform;
                Egyptian.isStatic = true;
                // right hand
                GameObject Egyptian2 = Instantiate(Egyptian_body, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i;
                if (level % 4 == 0)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (1.5f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 7.0f, 0.0f);
                }
                else
                if (level % 4 == 1)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (1.5f + 0.1f * i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 97.0f, 0.0f);
                }
                else
                if (level % 4 == 2)
                {
                    Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (1.5f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 187.0f, 0.0f);
                }
                else
                if (level % 4 == 3)
                {
                    Egyptian2.transform.position = new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (1.5f + 0.1f * i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, -75.0f, 0.0f);
                }
                //Egyptian2.transform.parent = objParent.transform;
                Egyptian2.transform.parent = workers_gameObject.transform;
                Egyptian2.isStatic = true;
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
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "4Ramp_" + level + "_" + row + "_1";
        if (DrawUntilRow && row > DrawRow)
            cube.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
        else
            cube.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2, h / 2 + height + blockheight * holeHeight / 2, sep / 2);        
        //    cube.transform.position = new Vector3((base_size - sep) / 2, h / 2 + height + blockheight, sep / 2);
        cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
        if (MethodInsideRamp)
        {
            cube.transform.position += Vector3.left * blockwide;            
            if (DrawUntilRow && row > DrawRow)
                cube.transform.localScale = new Vector3(last_length*2 - blockwide, blockheight * (holeHeight+2), blockwide * (holeWide-1));
            else
                cube.transform.localScale = new Vector3(length - blockwide, blockheight * (holeHeight+2), blockwide * (holeWide-1));
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
        cube.GetComponent<MeshRenderer>().enabled = false;
        //cube.GetComponent<ShowHideObject>().hide = true;
        cube.GetComponent<BoxCollider>().isTrigger = true;
        GameObject cube1 = cube;

        if (minBaseSize2Ramps < base_size)
        {            
            // Ramp 2
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_2";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight/2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight/2, -(base_size - sep) / 2);
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
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;            
        }
        GameObject cube2 = cube;

        // Ramp 3
        cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "4Ramp_" + level + "_" + row + "_3";
        if (DrawUntilRow && row > DrawRow)
            cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3(0, height + holeHeight / 2, 0);
        else
            cube.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2, h / 2 + height + blockheight * holeHeight / 2, -sep / 2);
        cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));        
        //cube.transform.position = new Vector3(-(base_size - sep) / 2, h / 2 + height + blockheight, -sep / 2);
        cube.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
        if (MethodInsideRamp)
        {
            cube.transform.position -= Vector3.left * blockwide;
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
        cube.GetComponent<MeshRenderer>().enabled = false;
        //cube.GetComponent<ShowHideObject>().hide = true;
        cube.GetComponent<BoxCollider>().isTrigger = true;
        GameObject cube3 = cube;

        if (minBaseSize2Ramps < base_size)
        {
            // Ramp 4
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "4Ramp_" + level + "_" + row + "_4";
            if (DrawUntilRow && row > DrawRow)
                cube.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight / 2, 0);
            else
                cube.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight / 2, (base_size - sep) / 2);
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
            cube.GetComponent<MeshRenderer>().enabled = false;
            //cube.GetComponent<ShowHideObject>().hide = true;
            cube.GetComponent<BoxCollider>().isTrigger = true;
        }
        GameObject cube4 = cube;

        // ramp corner wide
        // not remove blocks in the first level
        if (level > 0)
        {
            GameObject cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube_c.name = "4Ramp-corner_" + level + "_" + row + "_1";
            cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
            cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
            //cube_c.transform.parent = objParent.transform;
            cube_c.transform.parent = ramp_gameObject.transform;
            cube_c.isStatic = true;
            cube_c.AddComponent<DeleteObject>();
            cube_c.GetComponent<DeleteObject>().generatePyramid = this;
            cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube_c.GetComponent<MeshRenderer>().enabled = false;
            cube_c.GetComponent<BoxCollider>().isTrigger = true;

            if (minBaseSize2Ramps < base_size)
            {
                cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_2";
                cube_c.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }

            cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube_c.name = "4Ramp-corner_" + level + "_" + row + "_3";
            cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
            cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
            //cube_c.transform.parent = objParent.transform;
            cube_c.transform.parent = ramp_gameObject.transform;
            cube_c.isStatic = true;
            cube_c.AddComponent<DeleteObject>();
            cube_c.GetComponent<DeleteObject>().generatePyramid = this;
            cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
            cube_c.GetComponent<MeshRenderer>().enabled = false;
            cube_c.GetComponent<BoxCollider>().isTrigger = true;

            if (minBaseSize2Ramps < base_size)
            {
                cube_c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube_c.name = "4Ramp-corner_" + level + "_" + row + "_4";
                cube_c.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + holeHeight * blockheight * 3 / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
                cube_c.transform.localScale = new Vector3((holeWide + 1) * blockwide, holeHeight * blockheight * 3, (holeWide + 1) * blockwide);
                //cube_c.transform.parent = objParent.transform;
                cube_c.transform.parent = ramp_gameObject.transform;
                cube_c.isStatic = true;
                cube_c.AddComponent<DeleteObject>();
                cube_c.GetComponent<DeleteObject>().generatePyramid = this;
                cube_c.GetComponent<DeleteObject>().CommonGameObject = objParent;
                cube_c.GetComponent<MeshRenderer>().enabled = false;
                cube_c.GetComponent<BoxCollider>().isTrigger = true;
            }
        }

        // ramp floor
        GameObject cubefloor = null;
        if (DrawFloor)
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_1";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-1, height - bh2, 0);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - 1, h / 2 + height - bh2, sep / 2);
            //cubefloor.transform.position = new Vector3((base_size - sep) / 2 - 1, h / 2 + height - bh2, sep / 2);
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            else
                cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
        }
        GameObject cubefloor1 = cubefloor;

        if (minBaseSize2Ramps < base_size && DrawFloor)
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_2";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height - bh2, 1);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + 1);
            //cubefloor.transform.position = new Vector3(sep / 2, h / 2 + height - bh2, -(base_size - sep) / 2 + 1);
            cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            else
                cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
        }
        GameObject cubefloor2 = cubefloor;

        if (DrawFloor)
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_3";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3(1, height - bh2 + 0.2f, 0);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + 1, h / 2 + height - bh2 + 0.2f, -sep / 2);
            //cubefloor.transform.position = new Vector3(-(base_size - sep) / 2 + 1, h / 2 + height - bh2, -sep / 2);
            cubefloor.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 1, bh2 + 0.5f, 3 * blockwide);
            else
                cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;
        }
        GameObject cubefloor3 = cubefloor;

        if (minBaseSize2Ramps < base_size && DrawFloor)
        {
            cubefloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubefloor.name = "4Ramp_floor_" + level + "_" + row + "_4";
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height - bh2, -1);
            else
                cubefloor.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - 1);
            //cubefloor.transform.position = new Vector3(-sep / 2, h / 2 + height - bh2, (base_size - sep) / 2 - 1);
            cubefloor.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            if (DrawUntilRow && row > DrawRow)
                cubefloor.transform.localScale = new Vector3(last_length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            else
                cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.localScale = new Vector3(length + 2 * blockwide - 1, bh2 + 0.4f, 3 * blockwide);
            //cubefloor.transform.parent = objParent.transform;
            cubefloor.transform.parent = ramp_gameObject.transform;
            cubefloor.GetComponent<MeshRenderer>().material = m_Material;
            if (m_Material_floor)
                cubefloor.GetComponent<MeshRenderer>().material = m_Material_floor;
            cubefloor.isStatic = true;          
        }
        GameObject cubefloor4 = cubefloor;

        // ramp wall
        GameObject cubewall = null;
        if (DrawWall)
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_1";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (last_v0 + last_v1) / 2 + new Vector3(-holeWide * blockwide / 2, height + blockheight * holeHeight, 0);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3((base_size - sep) / 2 - holeWide * blockwide / 2, h / 2 + height + blockheight * holeHeight, sep / 2);
            //cubewall.transform.position = new Vector3((base_size - sep) / 2 - 1.5f * blockwide, h / 2 + height + blockheight, sep / 2);
            cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 8 * blockwide, blockheight * 9, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 8 * blockwide, blockheight * 9, 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 7 * blockwide, blockheight * 6, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 6, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
        }
        GameObject cubewall1 = cubewall;

        if (minBaseSize2Ramps < base_size && DrawWall)
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_2";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 90f, 0) * last_v0 + Quaternion.Euler(0, 90f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight, holeWide * blockwide / 2);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(sep / 2, h / 2 + height + blockheight * holeHeight, -(base_size - sep) / 2 + holeWide * blockwide / 2);
            //cubewall.transform.position = new Vector3(sep / 2, h / 2 + height + blockheight, -(base_size - sep) / 2 + 1.5f * blockwide);
            cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 8 * blockwide, blockheight * 9, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 8 * blockwide, blockheight * 9, 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 7 * blockwide, blockheight * 6, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 6, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;           
        }
        GameObject cubewall2 = cubewall;

        if (DrawWall)
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_3";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 180f, 0) * last_v0 + Quaternion.Euler(0, 180f, 0) * last_v1) / 2 + new Vector3(holeWide * blockwide / 2, height + blockheight * holeHeight, 0);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(-(base_size - sep) / 2 + holeWide * blockwide / 2, h / 2 + height + blockheight * holeHeight, -sep / 2);
            //cubewall.transform.position = new Vector3(-(base_size - sep) / 2 + 1.5f * blockwide, h / 2 + height + blockheight, -sep / 2);
            cubewall.transform.rotation = Quaternion.Euler(0, 90 + radians_to_degrees(a1), -radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 8 * blockwide, blockheight * 9, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 8 * blockwide, blockheight * 9, 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 7 * blockwide, blockheight * 6, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 6, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;
        }
        GameObject cubewall3 = cubewall;

        if (minBaseSize2Ramps < base_size && DrawWall)
        {
            cubewall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubewall.name = "4Ramp_wall_" + level + "_" + row + "_4";
            if (DrawUntilRow && row > DrawRow)
                cubewall.transform.position = objParent.transform.position + (Quaternion.Euler(0, 270f, 0) * last_v0 + Quaternion.Euler(0, 270f, 0) * last_v1) / 2 + new Vector3(0, height + blockheight * holeHeight, -holeWide * blockwide / 2);
            else
                cubewall.transform.position = objParent.transform.position + new Vector3(-sep / 2, h / 2 + height + blockheight * holeHeight, (base_size - sep) / 2 - holeWide * blockwide / 2);
            //cubewall.transform.position = new Vector3(-sep / 2, h / 2 + height + blockheight, (base_size - sep) / 2 - 1.5f * blockwide);
            cubewall.transform.rotation = Quaternion.Euler(0, radians_to_degrees(a1), radians_to_degrees(a2));
            if (MethodInsideRamp)
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 8 * blockwide, blockheight * 9, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 8 * blockwide, blockheight * 9, 0.1f);
            }
            else
            {
                if (DrawUntilRow && row > DrawRow)
                    cubewall.transform.localScale = new Vector3(last_length - 7 * blockwide, blockheight * 6, 0.1f);
                else
                    cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 6, 0.1f);
            }
            //cubewall.transform.localScale = new Vector3(length - 7 * blockwide, blockheight * 3, 0.1f);
            //cubewall.transform.parent = objParent.transform;
            cubewall.transform.parent = ramp_gameObject.transform;
            cubewall.GetComponent<MeshRenderer>().material = m_Material;
            cubewall.isStatic = true;           
        }
        GameObject cubewall4 = cubewall;

        // corner floor
        if (DrawFloor)
        {
            GameObject cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner.name = "Ramp_corner_" + level + "_" + row + "_1";
            cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide * 5 / 6), height, (base_size / 2 - holeWide * blockwide * 5 / 6));
            cubecorner.transform.localRotation = Quaternion.Euler(100.0f, -27.0f, -32.0f);
            cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
            //cubecorner.transform.parent = objParent.transform;
            cubecorner.transform.parent = ramp_gameObject.transform;
            cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "Ramp_corner_" + level + "_" + row + "_2";
                cubecorner.transform.position = objParent.transform.position + new Vector3((base_size / 2 - holeWide * blockwide * 5 / 6), height, -(base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(100.0f, 53.0f, 48.0f);
                cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
                //cubecorner.transform.parent = objParent.transform;
                cubecorner.transform.parent = ramp_gameObject.transform;
                cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner.isStatic = true;
            }

            cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner.name = "Ramp_corner_" + level + "_" + row + "_3";
            cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide * 5 / 6), height, -(base_size / 2 - holeWide * blockwide * 5 / 6));
            cubecorner.transform.localRotation = Quaternion.Euler(80.0f, -42.0f, -48.0f);
            cubecorner.transform.localScale = new Vector3(holeWide * blockwide * 2, holeWide * blockwide * 2, 0.1f);
            //cubecorner.transform.parent = objParent.transform;
            cubecorner.transform.parent = ramp_gameObject.transform;
            cubecorner.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                cubecorner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner.name = "Ramp_corner_" + level + "_" + row + "_4";
                cubecorner.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - holeWide * blockwide * 5 / 6), height, (base_size / 2 - holeWide * blockwide * 5 / 6));
                cubecorner.transform.localRotation = Quaternion.Euler(80.0f, 32.0f, 27.0f);
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
            GameObject cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row + "_1";
            cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeWide * blockwide, height + blockheight * holeHeight, (base_size / 2) - holeWide * blockwide);
            cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, 60.0f, 0.0f);
            cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight * 2, 0.1f);
            //cubecorner_wall.transform.parent = objParent.transform;
            cubecorner_wall.transform.parent = ramp_gameObject.transform;
            cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner_wall.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row + "_2";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3((base_size / 2) - holeWide * blockwide, height + blockheight * holeHeight, -(base_size / 2) + holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, -30.0f, 0.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight * 2, 0.1f);
                //cubecorner_wall.transform.parent = objParent.transform;
                cubecorner_wall.transform.parent = ramp_gameObject.transform;
                cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
                cubecorner_wall.isStatic = true;
            }

            cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row + "_3";
            cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeWide * blockwide, height + blockheight * holeHeight, -(base_size / 2) + holeWide * blockwide);
            cubecorner_wall.transform.localRotation = Quaternion.Euler(0.0f, 60.0f, 90.0f);
            cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight * 2, 0.1f);
            //cubecorner_wall.transform.parent = objParent.transform;
            cubecorner_wall.transform.parent = ramp_gameObject.transform;
            cubecorner_wall.GetComponent<MeshRenderer>().material = m_Material_corner;
            cubecorner_wall.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                cubecorner_wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubecorner_wall.name = "Ramp_cornerwall_" + level + "_" + row + "_4";
                cubecorner_wall.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + holeWide * blockwide, height + blockheight * holeHeight, (base_size / 2) - holeWide * blockwide);
                cubecorner_wall.transform.localRotation = Quaternion.Euler(180.0f, -30.0f, 0.0f);
                cubecorner_wall.transform.localScale = new Vector3((holeWide + 1) * blockwide, blockheight * holeHeight * 2, 0.1f);
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
            GameObject woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_1";
            woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, (base_size / 2) - (holeWide + 1) * blockwide / 2);
            woodencyl.transform.localRotation = Quaternion.Euler(-8.3f, -36.3f, 48.0f);
            woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
            //woodencyl.transform.parent = objParent.transform;
            woodencyl.transform.parent = ramp_gameObject.transform;
            woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
            woodencyl.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_2";
                woodencyl.transform.position = objParent.transform.position + new Vector3((base_size / 2) - (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2 ) + (holeWide + 1) * blockwide / 2);
                woodencyl.transform.localRotation = Quaternion.Euler(45.0f, -35.0f, 7.0f);
                woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
                //woodencyl.transform.parent = objParent.transform;
                woodencyl.transform.parent = ramp_gameObject.transform;
                woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
                woodencyl.isStatic = true;
            }

            woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            woodencyl.name = "Ramp_wooden_cylinder_" + level + "_" + row + "_3";
            woodencyl.transform.position = objParent.transform.position + new Vector3(-(base_size / 2) + (holeWide + 1) * blockwide / 2, height + hypotenuse_wood / 2, -(base_size / 2) + (holeWide + 1) * blockwide / 2);
            woodencyl.transform.localRotation = Quaternion.Euler(45.0f, 14.0f, -23.0f);
            woodencyl.transform.localScale = new Vector3(0.3f, hypotenuse_wood, 0.3f);
            //woodencyl.transform.parent = objParent.transform;
            woodencyl.transform.parent = ramp_gameObject.transform;
            woodencyl.GetComponent<MeshRenderer>().material = m_Material_wood;
            woodencyl.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                woodencyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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
            GameObject stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
            stone_sled1.name = "stone_sled_" + level + "_" + row + "_1";
            stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 1.0f * blockwide), height + 0.75f, (base_size / 2 - 2.5f * blockwide));
            stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 95.0f, 7.0f);
            //stone_sled1.transform.parent = objParent.transform;
            stone_sled1.transform.parent = workers_gameObject.transform;
            stone_sled1.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_2";
                stone_sled1.transform.position = objParent.transform.position + new Vector3((base_size / 2 - 2.5f * blockwide), height + 0.75f, -(base_size / 2 - 1.0f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 7.0f, -7.0f);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
            }

            stone_sled1 = Instantiate(stone_sled, objParent.transform.position + new Vector3(0, 0, 0), Quaternion.identity);
            stone_sled1.name = "stone_sled_" + level + "_" + row + "_3";
            stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - 1.0f * blockwide), height + 0.75f, -(base_size / 2 - 2.5f * blockwide));
            stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 95.0f, -7.0f);
            //stone_sled1.transform.parent = objParent.transform;
            stone_sled1.transform.parent = workers_gameObject.transform;
            stone_sled1.isStatic = true;

            if (minBaseSize2Ramps < base_size)
            {
                stone_sled1 = Instantiate(stone_sled, new Vector3(0, 0, 0), Quaternion.identity);
                stone_sled1.name = "stone_sled_" + level + "_" + row + "_4";
                stone_sled1.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - 2.5f * blockwide), height + 0.75f, (base_size / 2 - 1.0f * blockwide));
                stone_sled1.transform.localRotation = Quaternion.Euler(0.0f, 7.0f, 7.0f);
                //stone_sled1.transform.parent = objParent.transform;
                stone_sled1.transform.parent = workers_gameObject.transform;
                stone_sled1.isStatic = true;
            }
        }

        // egyptians
        if (DrawEgyptians && Egyptian_body && height < Height * 0.9f && !exportPyramidObj)
        {
            for (int i = 0; i < 12; i++)
            {
                // left hand
                GameObject Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_1";
                Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (0.75f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 7.0f, 0.0f);
                //Egyptian.transform.parent = objParent.transform;
                Egyptian.transform.parent = workers_gameObject.transform;
                Egyptian.isStatic = true;

                if (minBaseSize2Ramps < base_size)
                {
                    Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_2";
                    Egyptian.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (0.75f + 0.1f * i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 97.0f, 0.0f);
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                }

                Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_3";
                Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (0.75f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                Egyptian.transform.localRotation = Quaternion.Euler(7.0f, 187.0f, 0.0f);
                //Egyptian.transform.parent = objParent.transform;
                Egyptian.transform.parent = workers_gameObject.transform;
                Egyptian.isStatic = true;

                if (minBaseSize2Ramps < base_size)
                {
                    Egyptian = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian.name = "Egyptian_left_" + level + "_" + row + "_" + i + "_4";
                    Egyptian.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (0.75f + 0.1f * i) * blockwide));
                    Egyptian.transform.localRotation = Quaternion.Euler(7.0f, -75.0f, 0.0f);
                    //Egyptian.transform.parent = objParent.transform;
                    Egyptian.transform.parent = workers_gameObject.transform;
                    Egyptian.isStatic = true;
                }

                // right hand
                GameObject Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_1";
                Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (1.5f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (4.5f + i) * blockwide));
                Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 7.0f, 0.0f);
                //Egyptian2.transform.parent = objParent.transform;
                Egyptian2.transform.parent = workers_gameObject.transform;
                Egyptian2.isStatic = true;

                if (minBaseSize2Ramps < base_size)
                {
                    Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_2";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3((base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (1.5f + 0.1f * i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 97.0f, 0.0f);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                }

                Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_3";
                Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (1.5f + 0.1f * i) * blockwide), height + 2.25f + 0.16f * i, -(base_size / 2 - (4.5f + i) * blockwide));
                Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, 187.0f, 0.0f);
                //Egyptian2.transform.parent = objParent.transform;
                Egyptian2.transform.parent = workers_gameObject.transform;
                Egyptian2.isStatic = true;

                if (minBaseSize2Ramps < base_size)
                {
                    Egyptian2 = Instantiate(Egyptian_body, new Vector3(0, 0, 0), Quaternion.identity);
                    Egyptian2.name = "Egyptian_right_" + level + "_" + row + "_" + i + "_4";
                    Egyptian2.transform.position = objParent.transform.position + new Vector3(-(base_size / 2 - (4.5f + i) * blockwide), height + 2.25f + 0.16f * i, (base_size / 2 - (1.5f + 0.1f * i) * blockwide));
                    Egyptian2.transform.localRotation = Quaternion.Euler(7.0f, -75.0f, 0.0f);
                    //Egyptian2.transform.parent = objParent.transform;
                    Egyptian2.transform.parent = workers_gameObject.transform;
                    Egyptian2.isStatic = true;
                }
            }
        }

        // only at level 0 if 8 ramps
        if (level == 0 && Method8Ramp && DrawUntilRow && row < 21 && minBaseSize8Ramps < base_size)
        {
            // Middle Ramp 1
            cube = Instantiate(cube1);
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
                cubefloor.transform.position += new Vector3(0, 0, -base_size / 2);

                cubefloor = Instantiate(cubefloor2);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_2";
                cubefloor.transform.position += new Vector3(-base_size / 2, 0, 0);

                cubefloor = Instantiate(cubefloor3);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_3";
                cubefloor.transform.position += new Vector3(0, 0, base_size / 2);

                cubefloor = Instantiate(cubefloor4);
                //cubefloor.transform.parent = objParent.transform;
                cubefloor.transform.parent = ramp_gameObject.transform;
                cubefloor.name = "Middle_4Ramp_floor_" + level + "_" + row + "_4";
                cubefloor.transform.position += new Vector3(base_size / 2, 0, 0);
            }

            // ramp wall
            if (DrawWall)
            {
                cubewall = Instantiate(cubewall1);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_1";
                cubewall.transform.position += new Vector3(0, 0, -base_size / 2);

                cubewall = Instantiate(cubewall2);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_2";
                cubewall.transform.position += new Vector3(-base_size / 2, 0, 0);

                cubewall = Instantiate(cubewall3);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_3";
                cubewall.transform.position += new Vector3(0, 0, base_size / 2);

                cubewall = Instantiate(cubewall4);
                //cubewall.transform.parent = objParent.transform;
                cubewall.transform.parent = ramp_gameObject.transform;
                cubewall.name = "Middle_4Ramp_wall_" + level + "_" + row + "_4";
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
                    cubefloor.transform.position += new Vector3(0, 0, -base_size * 3 / 4);

                    cubefloor = Instantiate(cubefloor1);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_2";
                    cubefloor.transform.position += new Vector3(0, 0, -base_size / 4);

                    cubefloor = Instantiate(cubefloor2);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_3";
                    cubefloor.transform.position += new Vector3(-base_size * 3 / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor2);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_4";
                    cubefloor.transform.position += new Vector3(-base_size / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor3);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_5";
                    cubefloor.transform.position += new Vector3(0, 0, base_size * 3 / 4);

                    cubefloor = Instantiate(cubefloor3);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_6";
                    cubefloor.transform.position += new Vector3(0, 0, base_size / 4);

                    cubefloor = Instantiate(cubefloor4);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_7";
                    cubefloor.transform.position += new Vector3(base_size * 3 / 4, 0, 0);

                    cubefloor = Instantiate(cubefloor4);
                    //cubefloor.transform.parent = objParent.transform;
                    cubefloor.transform.parent = ramp_gameObject.transform;
                    cubefloor.name = "Middle_8Ramp_floor_" + level + "_" + row + "_8";
                    cubefloor.transform.position += new Vector3(base_size / 4, 0, 0);
                }

                // ramp wall
                if (DrawWall)
                {
                    cubewall = Instantiate(cubewall1);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_1";
                    cubewall.transform.position += new Vector3(0, 0, -base_size * 3 / 4);

                    cubewall = Instantiate(cubewall1);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_2";
                    cubewall.transform.position += new Vector3(0, 0, -base_size / 4);

                    cubewall = Instantiate(cubewall2);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_3";
                    cubewall.transform.position += new Vector3(-base_size * 3 / 4, 0, 0);

                    cubewall = Instantiate(cubewall2);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_4";
                    cubewall.transform.position += new Vector3(-base_size / 4, 0, 0);

                    cubewall = Instantiate(cubewall3);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_5";
                    cubewall.transform.position += new Vector3(0, 0, base_size * 3 / 4);

                    cubewall = Instantiate(cubewall3);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_6";
                    cubewall.transform.position += new Vector3(0, 0, base_size / 4);

                    cubewall = Instantiate(cubewall4);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_7";
                    cubewall.transform.position += new Vector3(base_size * 3 / 4, 0, 0);

                    cubewall = Instantiate(cubewall4);
                    //cubewall.transform.parent = objParent.transform;
                    cubewall.transform.parent = ramp_gameObject.transform;
                    cubewall.name = "Middle_8Ramp_wall_" + level + "_" + row + "_8";
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
    }

    /// <summary>
    /// Restaura los valores de los parámetros a sus valores por defecto.
    /// </summary>
    public void ResetValues()
    {
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
}
