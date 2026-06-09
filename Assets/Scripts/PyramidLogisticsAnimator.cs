using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions; // Importante para la limpieza de nombres

/// <summary>
/// Manages the logistics animation of the pyramid construction.
/// Moves blocks from a virtual quarry, up the ramps ("4Ramp_" or "Ramp_"),
/// to their final position using GPU Instancing for high performance.
/// </summary>
public class PyramidLogisticsAnimator : MonoBehaviour
{
    [Header("References")]
    public GeneratePyramid pyramidGenerator;

    [Header("Logistics Configuration")]
    [Tooltip("Distance from the ramp start where blocks spawn (Quarry).")]
    public float quarryDistance = 100f;

    [Tooltip("Movement speed of the blocks (meters/second).")]
    public float moveSpeed = 15f;

    [Tooltip("Rotation speed for orientation.")]
    public float rotateSpeed = 5f;

    [Tooltip("Target number of blocks to release per second. Higher values = faster flow.")]
    public float blocksPerSecond = 500f;

    [Tooltip("Delay between the start of each block layer.")]
    public float layerDelay = 0.15f;

    [Tooltip("Wait time after pyramid generation finishes before starting.")]
    public float startDelay = 0.5f;

    [Tooltip("If true, automatically waits for pyramid generation to start.")]
    public bool autoStart = true;

    [Header("Performance")]
    [Tooltip("Number of blocks extracted per frame during initialization.")]
    public int extractionBatchSize = 5000;

    // --- Internal Data Structures ---

    class LogisticsPath
    {
        public List<Vector3> waypoints = new List<Vector3>();
        public Vector3 startPoint; // Entry point at the base (Lowest point of ramp)
    }

    class LogisticsBlock
    {
        // Fixed Data
        public int id;
        public Vector3 finalPos;
        public Quaternion finalRot;
        public Vector3 scale;
        public Matrix4x4 targetMatrix;

        // Dynamic State
        public Vector3 currentPos;
        public Quaternion currentRot;
        public int rampIndex;
        public int currentWaypoint;
        public int constructionLayer;

        // Used to vary the entry point slightly so they don't form a perfect line immediately
        public Vector3 entryOffset;

        // State Machine: 0:Quarry, 1:Approach, 2:Ascent, 3:Placement, 4:Finished
        public int state;
        public float startTime;
    }

    class RenderBatch
    {
        public Mesh mesh;
        public Material material;
        public List<LogisticsBlock> activeBlocks = new List<LogisticsBlock>();
        public List<Matrix4x4> finishedMatrices = new List<Matrix4x4>();
        public List<Matrix4x4> renderBuffer = new List<Matrix4x4>();
    }

    private Dictionary<int, RenderBatch> renderBatches = new Dictionary<int, RenderBatch>();
    private List<LogisticsPath> rampPaths = new List<LogisticsPath>();
    private bool isAnimating = false;

    // OPTIMIZATION: Reusable buffer to avoid GC allocation every frame in DrawMatrixList
    private List<Matrix4x4> drawBatchBuffer = new List<Matrix4x4>(1023);

    void Start()
    {
        if (autoStart) StartWaiting();
    }

    public void StartWaiting() => StartCoroutine(WaitForGenerationAndStart());

    private IEnumerator WaitForGenerationAndStart()
    {
        while (pyramidGenerator != null && pyramidGenerator.isGenerating)
            yield return null;

        yield return new WaitForSeconds(startDelay);
        PrepareAndStartLogistics();
    }

    public void PrepareAndStartLogistics()
    {
        StartCoroutine(SetupSequence());
    }

    IEnumerator SetupSequence()
    {
        isAnimating = true;

        yield return StartCoroutine(AnalyzeRamps());
        yield return StartCoroutine(ExtractAllMatrices());

        if (renderBatches.Count == 0)
        {
            Debug.LogWarning("[Logistics] No blocks found to animate.");
            isAnimating = false;
            yield break;
        }

        Debug.Log("[Logistics] Starting construction sequence...");

        // Flatten list only for sorting logic, actual data stays in batches
        List<LogisticsBlock> allBlocks = new List<LogisticsBlock>();
        foreach (var batch in renderBatches.Values) allBlocks.AddRange(batch.activeBlocks);

        // Sort by Height (Y)
        allBlocks.Sort((a, b) => a.finalPos.y.CompareTo(b.finalPos.y));

        float currentTime = Time.time;
        float currentHeightThreshold = -9999f;
        float tolerance = 0.5f;
        float safeBPS = Mathf.Max(1f, blocksPerSecond);
        float staggerPerBlock = 1f / safeBPS;

        foreach (var block in allBlocks)
        {
            if (block.finalPos.y > currentHeightThreshold + tolerance)
            {
                currentTime += layerDelay;
                currentHeightThreshold = block.finalPos.y;
            }

            currentTime += staggerPerBlock;
            block.startTime = currentTime;
            block.state = 0;

            InitializeBlockPosition(block);
        }
    }

    IEnumerator AnalyzeRamps()
    {
        rampPaths.Clear();
        for (int i = 0; i < 4; i++) rampPaths.Add(new LogisticsPath());

        string prefix = (pyramidGenerator != null && pyramidGenerator.Method4Ramp) ? "4Ramp_" : "Ramp_";

        GameObject[] rampObjs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith(prefix)).ToArray();

        Vector3 center = Vector3.zero;
        if (pyramidGenerator != null && pyramidGenerator.objParent != null)
            center = pyramidGenerator.objParent.transform.position;
        else
            center = transform.position;

        if (rampObjs.Length == 0)
        {
            Debug.LogError($"[Logistics] Error: No ramps found with prefix '{prefix}'. Using corner fallback.");
            for (int i = 0; i < 4; i++)
            {
                float rad = (i * 90 + 45) * Mathf.Deg2Rad;
                // Fallback relative to CENTER
                rampPaths[i].startPoint = center + new Vector3(Mathf.Cos(rad) * 150, 0, Mathf.Sin(rad) * 150);
            }
            yield break;
        }

        List<GameObject>[] buckets = new List<GameObject>[4];
        for (int i = 0; i < 4; i++) buckets[i] = new List<GameObject>();

        foreach (var r in rampObjs)
        {
            int index = -1;

            // CLEAN NAME: Remove "(Clone)" and space+parenthesis+digits at end e.g. " (1)"
            string rName = r.name.Replace("(Clone)", "").Trim();
            rName = Regex.Replace(rName, @"\s\(\d+\)$", ""); // Removes " (1)", " (2)" etc.

            if (rName.EndsWith("_1")) index = 0;
            else if (rName.EndsWith("_2")) index = 1;
            else if (rName.EndsWith("_3")) index = 2;
            else if (rName.EndsWith("_4")) index = 3;

            // Only add if we successfully identified the ramp
            if (index != -1)
            {
                buckets[index].Add(r);
            }
            // Removing fallback to bucket[0] to prevent contaminating Ramp 1 with unknown segments
        }

        for (int i = 0; i < 4; i++)
        {
            var sortedSegments = buckets[i].OrderBy(go => go.transform.position.y).ToList();

            foreach (var seg in sortedSegments)
            {
                Collider col = seg.GetComponent<Collider>();
                Vector3 wp = col ? col.bounds.center : seg.transform.position;
                rampPaths[i].waypoints.Add(wp);
            }

            if (sortedSegments.Count > 0)
                rampPaths[i].startPoint = GetLowestPoint(sortedSegments[0]);
            else
            {
                // Fallback for empty buckets: Use Corner position
                float rad = (i * 90 + 45) * Mathf.Deg2Rad;
                rampPaths[i].startPoint = center + new Vector3(Mathf.Cos(rad) * 150, 0, Mathf.Sin(rad) * 150);
            }
        }

        Debug.Log($"[Logistics] Ramps Analyzed. Count per index: [0]:{buckets[0].Count}, [1]:{buckets[1].Count}, [2]:{buckets[2].Count}, [3]:{buckets[3].Count}");
        yield return null;
    }

    Vector3 GetLowestPoint(GameObject obj)
    {
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3[] verts = mf.sharedMesh.vertices;
            Transform tr = obj.transform;
            float minY = float.MaxValue;
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (Vector3 v in verts)
            {
                Vector3 worldV = tr.TransformPoint(v);
                if (worldV.y < minY) minY = worldV.y;
            }
            foreach (Vector3 v in verts)
            {
                Vector3 worldV = tr.TransformPoint(v);
                if (Mathf.Abs(worldV.y - minY) < 0.05f) { sum += worldV; count++; }
            }
            if (count > 0) return sum / count;
        }

        Collider col = obj.GetComponent<Collider>();
        if (col != null) return new Vector3(col.bounds.center.x, col.bounds.min.y, col.bounds.center.z);

        return obj.transform.position;
    }

    IEnumerator ExtractAllMatrices()
    {
        pyramidGenerator.canDrawGPU = false;
        renderBatches.Clear();
        int count = 0;

        if (pyramidGenerator.gpuBatches == null) yield break;

        foreach (var entry in pyramidGenerator.gpuBatches)
        {
            Material mat = entry.Key;
            if (pyramidGenerator.materialToMesh == null || !pyramidGenerator.materialToMesh.ContainsKey(mat)) continue;
            Mesh mesh = pyramidGenerator.materialToMesh[mat];

            int id = mesh.GetInstanceID() + mat.GetInstanceID();

            if (!renderBatches.ContainsKey(id))
                renderBatches[id] = new RenderBatch { mesh = mesh, material = mat };

            foreach (var batch in entry.Value)
            {
                foreach (var matrix in batch)
                {
                    LogisticsBlock lb = new LogisticsBlock();
                    lb.id = count;
                    lb.finalPos = matrix.GetPosition();
                    lb.finalRot = matrix.rotation;
                    lb.scale = matrix.lossyScale;
                    lb.targetMatrix = matrix;

                    // Assign closest ramp by distance at target height
                    lb.rampIndex = GetClosestRampIndex(lb.finalPos);

                    renderBatches[id].activeBlocks.Add(lb);
                    count++;

                    if (count % extractionBatchSize == 0) yield return null;
                }
            }
        }
    }

    int GetClosestRampIndex(Vector3 targetPos)
    {
        int bestIndex = 0;
        float globalMinDistSq = float.MaxValue;

        for (int i = 0; i < rampPaths.Count; i++)
        {
            var waypoints = rampPaths[i].waypoints;

            // KEY FIX: Default to StartPoint if waypoints list is empty (Fallback Scenario)
            Vector3 bestPointOnRamp = rampPaths[i].startPoint;

            // If we have real waypoints, find the best height match
            if (waypoints != null && waypoints.Count > 0)
            {
                Vector3 bestWP = waypoints[0];
                float minYDiff = Mathf.Abs(waypoints[0].y - targetPos.y);

                for (int w = 1; w < waypoints.Count; w++)
                {
                    float diff = Mathf.Abs(waypoints[w].y - targetPos.y);
                    if (diff < minYDiff) { minYDiff = diff; bestWP = waypoints[w]; }
                    else if (diff > minYDiff) break;
                }
                bestPointOnRamp = bestWP;
            }

            // Calc Distance (XZ Only)
            float distSq = (targetPos.x - bestPointOnRamp.x) * (targetPos.x - bestPointOnRamp.x) +
                           (targetPos.z - bestPointOnRamp.z) * (targetPos.z - bestPointOnRamp.z);

            if (distSq < globalMinDistSq) { globalMinDistSq = distSq; bestIndex = i; }
        }
        return bestIndex;
    }

    void InitializeBlockPosition(LogisticsBlock block)
    {
        Vector3 center = Vector3.zero;
        if (pyramidGenerator != null && pyramidGenerator.objParent != null)
            center = pyramidGenerator.objParent.transform.position;
        else
            center = transform.position;

        Vector3 rampStart = rampPaths[block.rampIndex].startPoint;

        Vector3 outwardDir = (rampStart - center).normalized;
        if (outwardDir == Vector3.zero) outwardDir = Vector3.forward;

        System.Random rnd = new System.Random(block.id);

        float randomAngle = (float)(rnd.NextDouble() * 90.0 - 45.0);
        Quaternion rotation = Quaternion.Euler(0, randomAngle, 0);
        Vector3 randomizedDir = rotation * outwardDir;

        float distFactor = (float)(rnd.NextDouble() * 0.5 + 0.75); // 0.75 to 1.25
        float finalDistance = quarryDistance * distFactor;

        block.currentPos = rampStart + (randomizedDir * finalDistance);
        block.currentPos.y = rampStart.y;

        block.currentRot = Quaternion.LookRotation(rampStart - block.currentPos);

        float offX = (float)(rnd.NextDouble() * 4.0 - 2.0);
        float offZ = (float)(rnd.NextDouble() * 4.0 - 2.0);
        block.entryOffset = new Vector3(offX, 0, offZ);

        block.state = 0;
    }

    void Update()
    {
        if (!isAnimating) return;

        float dt = Time.deltaTime;
        float time = Time.time;

        foreach (var batch in renderBatches.Values)
        {
            // Clear current frame buffer
            batch.renderBuffer.Clear();

            // Draw Finished Blocks (Static)
            DrawMatrixList(batch.mesh, batch.material, batch.finishedMatrices);

            // Move Active Blocks
            for (int i = batch.activeBlocks.Count - 1; i >= 0; i--)
            {
                LogisticsBlock b = batch.activeBlocks[i];

                if (b.state == 0)
                {
                    if (time >= b.startTime) b.state = 1;
                    else continue;
                }

                bool finishedStep = false;
                Vector3 target = Vector3.zero;
                LogisticsPath path = rampPaths[b.rampIndex];

                switch (b.state)
                {
                    case 1: // Approach
                        target = path.startPoint + b.entryOffset;
                        b.currentPos = Vector3.MoveTowards(b.currentPos, target, moveSpeed * dt);

                        if (target != b.currentPos)
                            b.currentRot = Quaternion.Lerp(b.currentRot, Quaternion.LookRotation(target - b.currentPos), rotateSpeed * dt);

                        if (Vector3.SqrMagnitude(b.currentPos - target) < 1f)
                        {
                            b.state = 2; // Enter Ramp
                            b.currentWaypoint = 0;
                        }
                        break;

                    case 2: // Ascent
                        if (path.waypoints.Count == 0) { b.state = 3; break; }

                        target = path.waypoints[b.currentWaypoint];

                        if (target.y > b.finalPos.y + 0.5f)
                        {
                            b.state = 3;
                            b.currentPos.y = b.finalPos.y;
                            break;
                        }

                        b.currentPos = Vector3.MoveTowards(b.currentPos, target, moveSpeed * dt);

                        if (target != b.currentPos)
                            b.currentRot = Quaternion.Lerp(b.currentRot, Quaternion.LookRotation(target - b.currentPos), rotateSpeed * dt);

                        if (Vector3.SqrMagnitude(b.currentPos - target) < 2f)
                        {
                            b.currentWaypoint++;
                            if (b.currentWaypoint >= path.waypoints.Count) b.state = 3;
                        }
                        break;

                    case 3: // Horizontal Placement
                        target = b.finalPos;
                        Vector3 nextPos = Vector3.MoveTowards(b.currentPos, target, moveSpeed * dt);
                        nextPos.y = b.finalPos.y;
                        b.currentPos = nextPos;

                        float dist = Vector3.Distance(b.currentPos, target);
                        if (dist < 5f) b.currentRot = Quaternion.Lerp(b.currentRot, b.finalRot, rotateSpeed * dt * 2f);
                        else if (target != b.currentPos) b.currentRot = Quaternion.Lerp(b.currentRot, Quaternion.LookRotation(target - b.currentPos), rotateSpeed * dt);

                        if (Vector3.SqrMagnitude(b.currentPos - target) < 0.01f)
                        {
                            b.state = 4;
                            finishedStep = true;
                        }
                        break;
                }

                if (finishedStep)
                {
                    batch.finishedMatrices.Add(b.targetMatrix);

                    int lastIndex = batch.activeBlocks.Count - 1;
                    batch.activeBlocks[i] = batch.activeBlocks[lastIndex];
                    batch.activeBlocks.RemoveAt(lastIndex);
                }
                else
                {
                    batch.renderBuffer.Add(Matrix4x4.TRS(b.currentPos, b.currentRot, b.scale));
                }
            }

            DrawMatrixList(batch.mesh, batch.material, batch.renderBuffer);
        }
    }

    void DrawMatrixList(Mesh mesh, Material mat, List<Matrix4x4> matrices)
    {
        int count = matrices.Count;
        for (int i = 0; i < count; i += 1023)
        {
            int batchSize = Mathf.Min(1023, count - i);

            drawBatchBuffer.Clear();
            for (int j = 0; j < batchSize; j++)
            {
                drawBatchBuffer.Add(matrices[i + j]);
            }

            Graphics.DrawMeshInstanced(mesh, 0, mat, drawBatchBuffer);
        }
    }
}