using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class PyramidSkeletonGenerator : MonoBehaviour
{
    public enum GenerationMode
    {
        BaseAndEdgesOnly,
        HorizontalLinesAtHeightStep,
        LinesByEdgeDistance,
        GeometricProgression,
        SetbackGeometricProgression,
        ConstantSlopeRamp,
        CustomHeights
    }

    public enum TargetFace
    {
        AllFaces,
        North,
        South,
        East,
        West
    }

    public enum RampDirection
    {
        Clockwise,
        CounterClockwise
    }

    [Header("Mode & Face Selection")]
    public GenerationMode generationMode = GenerationMode.BaseAndEdgesOnly;

    [Tooltip("Choose which face will show the horizontal lines, or 'All Faces' to display them around the entire perimeter.")]
    public TargetFace targetFace = TargetFace.AllFaces;

    [Header("Pyramid Parameters")]
    public float baseSize = 230.36f;        // Base width in meters
    public float height = 146.50f;          // Total height in meters
    public float inclinationAngle = 51.84f; // Face inclination angle in degrees

    [Header("Method Parameters")]
    public float heightStep = 10.0f;
    public float edgeSegmentDistance = 5.0f;

    [Tooltip("Number of levels in the geometric progression or constant slope ramp.")]
    public int totalLevels = 10;
    [Tooltip("Initial height step from the base (also used as baseline for setback progression).")]
    public float initialHeightStep = 27.61f;
    [Range(0.1f, 1.0f)]
    [Tooltip("Geometric factor multiplied to each subsequent step towards the apex.")]
    public float progressionFactor = 0.816f;

    [Header("Setback Progression Parameters (For SetbackGeometricProgression mode)")]
    [Range(0.1f, 1.0f)]
    [Tooltip("Geometric factor applied to subsequent setback distances.")]
    public float setbackProgressionFactor = 0.85f;

    [Header("Constant Slope Ramp Parameters (For ConstantSlopeRamp mode)")]
    [Tooltip("Target slope angle for the ramps in degrees.")]
    public float targetRampAngle = 7.5f;
    [Tooltip("Setback distance from the outer edges (0 = full pyramid width).")]
    public float rampSetback = 0.0f;

    [Header("Custom Heights (For CustomHeights mode)")]
    [Tooltip("Specific height values where horizontal lines will be drawn.")]
    public List<float> customHeights = new List<float> {
        27.61f, 50.16f, 68.58f, 83.62f, 95.90f, 105.93f, 114.12f, 120.81f, 126.27f, 130.73f
    };

    [Header("Ramps & Pattern Filtering")]
    [Tooltip("Enable to draw directional ramps across the selected face, starting from the base (h=0).")]
    public bool drawRamps = false;
    [Tooltip("Direction of the ramp segments (Clockwise or CounterClockwise).")]
    public RampDirection rampDirection = RampDirection.Clockwise;
    [Tooltip("Offset distance from the edges used both for inner setback geometry and ramp positioning (0 = touches the edge).")]
    public float rampEdgeOffset = 0.0f;
    [Tooltip("Color of the connecting ramp lines.")]
    public Color rampColor = Color.green;
    [Tooltip("If true, alternates drawing ramps (draw one segment, skip one), keeping all horizontal lines.")]
    public bool skipAlternatingLevels = false;
    [Tooltip("If true, sequence starts at the second level instead of the first.")]
    public bool startFromSecondLevel = false;

    [Header("Skeleton Styling")]
    public float lineWidth = 0.5f;
    public Color baseAndEdgesColor = Color.cyan;
    public Color horizontalLinesColor = Color.yellow;

    private LineRenderer baseAndEdgesRenderer;
    private GameObject horizontalLinesContainer;
    private GameObject rampsContainer;

    private void Awake()
    {
        SetupRenderers();
    }

    private void Start()
    {
        // Automatically execute the selected generation mode on startup
        ExecuteGenerationMode();
    }

    private void SetupRenderers()
    {
        // Configure main object as Base and Edges renderer
        baseAndEdgesRenderer = GetComponent<LineRenderer>();
        ConfigureSingleRenderer(baseAndEdgesRenderer, baseAndEdgesColor);

        // Setup container for individual level renderers
        Transform horizContainerTrans = transform.Find("HorizontalLinesContainer");
        if (horizContainerTrans == null)
        {
            horizontalLinesContainer = new GameObject("HorizontalLinesContainer");
            horizontalLinesContainer.transform.SetParent(transform, false);
        }
        else
        {
            horizontalLinesContainer = horizContainerTrans.gameObject;
        }

        // Setup container for ramp renderers
        Transform rampsContainerTrans = transform.Find("RampsContainer");
        if (rampsContainerTrans == null)
        {
            rampsContainer = new GameObject("RampsContainer");
            rampsContainer.transform.SetParent(transform, false);
        }
        else
        {
            rampsContainer = rampsContainerTrans.gameObject;
        }
    }

    private void ConfigureSingleRenderer(LineRenderer lr, Color col)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.useWorldSpace = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(col, 0.0f), new GradientColorKey(col, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        lr.colorGradient = gradient;
    }

    public void ExecuteGenerationMode()
    {
        SetupRenderers();
        GenerateBaseAndEdges();
        ClearContainers();

        List<float> calculatedHeights = new List<float>();

        switch (generationMode)
        {
            case GenerationMode.BaseAndEdgesOnly:
                break;
            case GenerationMode.HorizontalLinesAtHeightStep:
                calculatedHeights = GetHeightsAtStep(heightStep);
                break;
            case GenerationMode.LinesByEdgeDistance:
                calculatedHeights = GetHeightsByEdgeDistance(edgeSegmentDistance);
                break;
            case GenerationMode.GeometricProgression:
                calculatedHeights = GetHeightsByGeometricProgression(totalLevels, initialHeightStep, progressionFactor);
                break;
            case GenerationMode.SetbackGeometricProgression:
                calculatedHeights = GetHeightsBySetbackProgression(totalLevels, initialHeightStep, setbackProgressionFactor);
                break;
            case GenerationMode.ConstantSlopeRamp:
                calculatedHeights = GetHeightsByConstantSlopeRamp(totalLevels, targetRampAngle, rampSetback);
                break;
            case GenerationMode.CustomHeights:
                calculatedHeights = GetValidCustomHeights(customHeights);
                break;
        }

        // Horizontal lines only respect startFromSecondLevel if set
        List<float> horizontalHeights = FilterHorizontalHeights(calculatedHeights);

        // Build all horizontal lines and selectively filtered ramps
        BuildLevelsFromHeights(horizontalHeights, calculatedHeights);
    }

    private List<float> FilterHorizontalHeights(List<float> rawHeights)
    {
        List<float> filtered = new List<float>();
        int startIndex = startFromSecondLevel ? 1 : 0;

        for (int i = startIndex; i < rawHeights.Count; i++)
        {
            filtered.Add(rawHeights[i]);
        }
        return filtered;
    }

    private void ClearContainers()
    {
        if (horizontalLinesContainer != null)
        {
            foreach (Transform child in horizontalLinesContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }
        if (rampsContainer != null)
        {
            foreach (Transform child in rampsContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void GenerateBaseAndEdges()
    {
        List<Vector3> points = new List<Vector3>();
        float halfBase = baseSize / 2f;

        Vector3 c1 = new Vector3(-halfBase, 0, -halfBase);
        Vector3 c2 = new Vector3(halfBase, 0, -halfBase);
        Vector3 c3 = new Vector3(halfBase, 0, baseSize / 2f);
        Vector3 corner1 = new Vector3(-halfBase, 0, -halfBase);
        Vector3 corner2 = new Vector3(halfBase, 0, -halfBase);
        Vector3 corner3 = new Vector3(halfBase, 0, halfBase);
        Vector3 corner4 = new Vector3(-halfBase, 0, halfBase);
        Vector3 apex = new Vector3(0, height, 0);

        // Base perimeter segments
        AddLineSegment(points, corner1, corner2);
        AddLineSegment(points, corner2, corner3);
        AddLineSegment(points, corner3, corner4);
        AddLineSegment(points, corner4, corner1);

        // The 4 main edges
        AddLineSegment(points, corner1, apex);
        AddLineSegment(points, corner2, apex);
        AddLineSegment(points, corner3, apex);
        AddLineSegment(points, corner4, apex);

        baseAndEdgesRenderer.positionCount = points.Count;
        baseAndEdgesRenderer.SetPositions(points.ToArray());

        Debug.Log("Base skeleton and 4 edges generated successfully.");
    }

    private List<float> GetHeightsAtStep(float step)
    {
        List<float> heights = new List<float>();
        Debug.Log("--- Height Step Calculated Levels ---");
        int index = 1;
        float halfBase = baseSize / 2f;
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));
        float prevEdgeDist = 0f;

        for (float h = step; h < height; h += step)
        {
            heights.Add(h);
            float currentEdgeDist = (h / height) * edgeLength;
            float edgeSegmentDist = currentEdgeDist - prevEdgeDist;
            Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                index++, h, step, currentEdgeDist, edgeSegmentDist));
            prevEdgeDist = currentEdgeDist;
        }
        return heights;
    }

    private List<float> GetHeightsByEdgeDistance(float segmentDistance)
    {
        List<float> heights = new List<float>();
        float halfBase = baseSize / 2f;
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));

        Debug.Log("--- Edge Distance Calculated Levels ---");
        int index = 1;
        float prevH = 0f;
        float prevEdgeDist = 0f;

        for (float d = segmentDistance; d < edgeLength; d += segmentDistance)
        {
            float ratio = d / edgeLength;
            float h = height * ratio;
            if (h < height)
            {
                heights.Add(h);
                float stepVal = h - prevH;
                float edgeSegmentDist = d - prevEdgeDist;
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                    index++, h, stepVal, d, edgeSegmentDist));
                prevH = h;
                prevEdgeDist = d;
            }
        }
        return heights;
    }

    private List<float> GetHeightsByGeometricProgression(int levels, float initialStep, float factor)
    {
        List<float> heights = new List<float>();
        float currentH = 0f;
        float currentStep = initialStep;
        float halfBase = baseSize / 2f;
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));
        float prevEdgeDist = 0f;
        float prevEdgeSegDist = 0f;

        Debug.Log("--- Geometric Progression Calculated Heights & Edge Metrics ---");
        for (int i = 0; i < levels; i++)
        {
            currentH += currentStep;
            if (currentH >= height) break;

            heights.Add(currentH);

            float currentEdgeDist = (currentH / height) * edgeLength;
            float edgeSegmentDist = currentEdgeDist - prevEdgeDist;
            float progressionRatio = (prevEdgeSegDist > 0f) ? (edgeSegmentDist / prevEdgeSegDist) : 0f;

            if (i == 0)
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                    i + 1, currentH, currentStep, currentEdgeDist, edgeSegmentDist));
            }
            else
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m) | Progression Ratio: {5:F3}",
                    i + 1, currentH, currentStep, currentEdgeDist, edgeSegmentDist, progressionRatio));
            }

            prevEdgeDist = currentEdgeDist;
            prevEdgeSegDist = edgeSegmentDist;
            currentStep *= factor;
        }
        return heights;
    }

    private List<float> GetHeightsBySetbackProgression(int levels, float initialHeightStepVal, float factor)
    {
        List<float> heights = new List<float>();
        float halfBase = baseSize / 2f;
        float slopeRatio = halfBase / height;
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));

        float currentSetback = initialHeightStepVal * slopeRatio;
        float accumulatedSetback = 0f;
        float prevEdgeDist = 0f;
        float prevEdgeSegDist = 0f;

        Debug.Log("--- Setback Geometric Progression Calculated Heights & Edge Metrics ---");
        for (int i = 0; i < levels; i++)
        {
            accumulatedSetback += currentSetback;
            float currentH = accumulatedSetback / slopeRatio;

            if (currentH >= height) break;

            heights.Add(currentH);

            float currentEdgeDist = (currentH / height) * edgeLength;
            float edgeSegmentDist = currentEdgeDist - prevEdgeDist;
            float progressionRatio = (prevEdgeSegDist > 0f) ? (edgeSegmentDist / prevEdgeSegDist) : 0f;

            if (i == 0)
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Setback Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                    i + 1, currentH, currentSetback, currentEdgeDist, edgeSegmentDist));
            }
            else
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Setback Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m) | Progression Ratio: {5:F3}",
                    i + 1, currentH, currentSetback, currentEdgeDist, edgeSegmentDist, progressionRatio));
            }

            prevEdgeDist = currentEdgeDist;
            prevEdgeSegDist = edgeSegmentDist;
            currentSetback *= factor;
        }
        return heights;
    }

    private List<float> GetHeightsByConstantSlopeRamp(int levels, float angleDeg, float setback)
    {
        List<float> heights = new List<float>();
        float halfBase = baseSize / 2f;
        float tanAlpha = Mathf.Tan(angleDeg * Mathf.Deg2Rad);
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));

        float h = 0f;
        int index = 1;
        float prevEdgeDist = 0f;
        float prevEdgeSegDist = 0f;

        Debug.Log("--- Constant Slope Ramp Calculated Heights & Edge Metrics ---");
        while (h < height && index <= levels)
        {
            float faceWidth = 2f * halfBase * (1f - (h / height));
            float rampSpan = faceWidth - (2f * setback);

            if (rampSpan <= 0f) break;

            float deltaH = rampSpan * tanAlpha;
            float nextH = h + deltaH;

            if (nextH >= height) break;

            heights.Add(nextH);

            float currentEdgeDist = (nextH / height) * edgeLength;
            float edgeSegmentDist = currentEdgeDist - prevEdgeDist;
            float progressionRatio = (prevEdgeSegDist > 0f) ? (edgeSegmentDist / prevEdgeSegDist) : 0f;

            if (index == 1)
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Rise: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                    index, nextH, deltaH, currentEdgeDist, edgeSegmentDist));
            }
            else
            {
                Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Rise: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m) | Progression Ratio: {5:F3}",
                    index, nextH, deltaH, currentEdgeDist, edgeSegmentDist, progressionRatio));
            }

            prevEdgeDist = currentEdgeDist;
            prevEdgeSegDist = edgeSegmentDist;
            h = nextH;
            index++;
        }
        return heights;
    }

    private List<float> GetValidCustomHeights(List<float> heightsList)
    {
        List<float> heights = new List<float>();
        float halfBase = baseSize / 2f;
        float edgeLength = Mathf.Sqrt((halfBase * halfBase) * 2f + (height * height));

        Debug.Log("--- Custom Heights Loaded & Edge Metrics ---");
        int index = 1;
        float prevH = 0f;
        float prevEdgeDist = 0f;
        float prevEdgeSegDist = 0f;

        foreach (float h in heightsList)
        {
            if (h > 0 && h < height)
            {
                heights.Add(h);
                float stepVal = h - prevH;
                float currentEdgeDist = (h / height) * edgeLength;
                float edgeSegmentDist = currentEdgeDist - prevEdgeDist;
                float progressionRatio = (prevEdgeSegDist > 0f) ? (edgeSegmentDist / prevEdgeSegDist) : 0f;

                if (index == 1)
                {
                    Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m)",
                        index, h, stepVal, currentEdgeDist, edgeSegmentDist));
                }
                else
                {
                    Debug.Log(string.Format("Level {0}: Height = {1:F2} m (Step: {2:F2} m) | Edge Dist: {3:F2} m (Seg Dist: {4:F2} m) | Progression Ratio: {5:F3}",
                        index, h, stepVal, currentEdgeDist, edgeSegmentDist, progressionRatio));
                }

                prevH = h;
                prevEdgeDist = currentEdgeDist;
                prevEdgeSegDist = edgeSegmentDist;
                index++;
            }
        }
        return heights;
    }

    private void BuildLevelsFromHeights(List<float> horizontalHeights, List<float> rawCalculatedHeights)
    {
        float halfBase = baseSize / 2f;

        // 1. Create horizontal lines for all filtered horizontal height levels
        for (int i = 0; i < horizontalHeights.Count; i++)
        {
            float h = horizontalHeights[i];
            float currentHalfBase = halfBase * (1f - (h / height));
            CreateLevelObject(currentHalfBase, h, targetFace);
        }

        // 2. Create directional ramps connecting consecutive levels, applying skipAlternatingLevels strictly to ramps
        if (drawRamps && rawCalculatedHeights.Count > 0 && targetFace != TargetFace.AllFaces)
        {
            List<float> sequence = new List<float>();
            sequence.Add(0.0f); // Base start
            sequence.AddRange(rawCalculatedHeights);

            Debug.Log("--- Ramp Segments Metrics (Length & Angles) ---");
            int rampSegmentIndex = 0;

            int startIndex = startFromSecondLevel ? 1 : 0;

            for (int i = 0; i < sequence.Count - 1; i++)
            {
                // Check if this ramp segment should be skipped according to skipAlternatingLevels
                bool includeRamp = true;
                if (skipAlternatingLevels)
                {
                    int relativeIndex = i - startIndex;
                    // Ensure the last available ramp segment reaching the topmost level is always included
                    bool isLastSegment = (i == sequence.Count - 2);
                    if (!isLastSegment && (relativeIndex < 0 || relativeIndex % 2 != 0))
                    {
                        includeRamp = false;
                    }
                }
                else if (startFromSecondLevel && i == 0)
                {
                    includeRamp = false;
                }

                if (includeRamp && i >= startIndex)
                {
                    float h1 = sequence[i];
                    float h2 = sequence[i + 1];

                    float hb1 = halfBase * (1f - (h1 / height));
                    float hb2 = halfBase * (1f - (h2 / height));

                    GetFaceEndpointsWithOffset(targetFace, h1, hb1, rampEdgeOffset, out Vector3 startLeft, out Vector3 startRight);
                    GetFaceEndpointsWithOffset(targetFace, h2, hb2, rampEdgeOffset, out Vector3 endLeft, out Vector3 endRight);

                    Vector3 startPoint = (rampDirection == RampDirection.Clockwise) ? startLeft : startRight;
                    Vector3 endPoint = (rampDirection == RampDirection.Clockwise) ? endRight : endLeft;

                    CreateRampSegment(startPoint, endPoint, rampSegmentIndex);

                    float rampLength = Vector3.Distance(startPoint, endPoint);
                    float verticalDelta = Mathf.Abs(endPoint.y - startPoint.y);
                    float surfaceAngleDeg = (generationMode == GenerationMode.ConstantSlopeRamp) ? targetRampAngle :
                        Mathf.Asin(Mathf.Clamp01(verticalDelta / rampLength)) * Mathf.Rad2Deg;

                    float fullFaceWidthAtH1 = 2f * hb1;
                    float edgeToEdgeAngleDeg = Mathf.Atan2(verticalDelta, fullFaceWidthAtH1) * Mathf.Rad2Deg;

                    Debug.Log(string.Format("Ramp Segment {0} (H: {1:F2}m -> {2:F2}m): Length = {3:F2} m | Surface Slope = {4:F2}° | Edge-to-Edge Slope = {5:F2}°",
                        rampSegmentIndex + 1, h1, h2, rampLength, surfaceAngleDeg, edgeToEdgeAngleDeg));

                    rampSegmentIndex++;
                }
            }
        }
    }

    private void GetFaceEndpointsWithOffset(TargetFace face, float h, float currentHalfBase, float offset, out Vector3 left, out Vector3 right)
    {
        Vector3 nw = new Vector3(-currentHalfBase, h, -currentHalfBase);
        Vector3 ne = new Vector3(currentHalfBase, h, -currentHalfBase);
        Vector3 se = new Vector3(currentHalfBase, h, currentHalfBase);
        Vector3 sw = new Vector3(-currentHalfBase, h, currentHalfBase);

        Vector3 rawLeft, rawRight;

        switch (face)
        {
            case TargetFace.North: // Edge NE to SE (+Z)
                rawLeft = ne; rawRight = se; break;
            case TargetFace.South: // Edge SW to NW (-Z)
                rawLeft = sw; rawRight = nw; break;
            case TargetFace.East:  // Edge SE to SW (+X)
                rawLeft = se; rawRight = sw; break;
            case TargetFace.West:  // Edge NW to NE (-X)
                rawLeft = nw; rawRight = ne; break;
            default:
                rawLeft = nw; rawRight = ne; break;
        }

        // Apply offset inwards along the horizontal segment direction
        Vector3 dir = (rawRight - rawLeft).normalized;
        left = rawLeft + dir * offset;
        right = rawRight - dir * offset;
    }

    private void AddLineSegment(List<Vector3> points, Vector3 start, Vector3 end)
    {
        points.Add(start);
        points.Add(end);
    }

    private void CreateLevelObject(float currentHalfBase, float h, TargetFace face)
    {
        GameObject levelObj = new GameObject(string.Format("Level_H_{0:F2}", h));
        levelObj.transform.SetParent(horizontalLinesContainer.transform, false);
        LineRenderer lr = levelObj.AddComponent<LineRenderer>();
        ConfigureSingleRenderer(lr, horizontalLinesColor);

        List<Vector3> levelPoints = new List<Vector3>();

        Vector3 nw = new Vector3(-currentHalfBase, h, -currentHalfBase);
        Vector3 ne = new Vector3(currentHalfBase, h, -currentHalfBase);
        Vector3 se = new Vector3(currentHalfBase, h, currentHalfBase);
        Vector3 sw = new Vector3(-currentHalfBase, h, currentHalfBase);

        switch (face)
        {
            case TargetFace.AllFaces:
                AddLineSegment(levelPoints, nw, ne);
                AddLineSegment(levelPoints, ne, se);
                AddLineSegment(levelPoints, se, sw);
                AddLineSegment(levelPoints, sw, nw);
                break;
            case TargetFace.North:
                AddLineSegment(levelPoints, ne, se);
                break;
            case TargetFace.South:
                AddLineSegment(levelPoints, sw, nw);
                break;
            case TargetFace.East:
                AddLineSegment(levelPoints, se, sw);
                break;
            case TargetFace.West:
                AddLineSegment(levelPoints, nw, ne);
                break;
        }

        lr.positionCount = levelPoints.Count;
        lr.SetPositions(levelPoints.ToArray());
    }

    private void CreateRampSegment(Vector3 start, Vector3 end, int index)
    {
        GameObject rampObj = new GameObject(string.Format("Ramp_Segment_{0}", index));
        rampObj.transform.SetParent(rampsContainer.transform, false);
        LineRenderer lr = rampObj.AddComponent<LineRenderer>();
        ConfigureSingleRenderer(lr, rampColor);

        Vector3[] points = new Vector3[] { start, end };
        lr.positionCount = points.Length;
        lr.SetPositions(points);
    }
}