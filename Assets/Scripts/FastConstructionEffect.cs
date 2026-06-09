using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.Collections.Unicode;

public class FastConstructionEffect : MonoBehaviour
{
    [Header("References")]
    public GeneratePyramid pyramidGenerator;

    [Header("Configuration")]
    public float dropHeight = 15.0f;
    public float fallDuration = 0.5f;
    public float speedVariation = 0.2f;
    public int blocksPerFrame = 150;
    public float chaosAmount = 8.0f;

    [Header("Flow Configuration")]
    [Tooltip("How many blocks start falling each second (independent of FPS).")]
    public float blocksPerSecond = 5000f;

    private bool isAnimating = false;
    private float startTime;

    private class FallingBlock
    {
        public Matrix4x4 targetMatrix;
        public Mesh mesh;
        public Material material;
        public float delay;
        public float individualDuration;
        public int materialID;
        public int meshID;
    }

    private List<FallingBlock> allFallingBlocks = new List<FallingBlock>();
    private Matrix4x4[] batchBuffer = new Matrix4x4[1023];

    public void StartWaiting() => StartCoroutine(WaitForGenerationAndStart());

    private IEnumerator WaitForGenerationAndStart()
    {
        while (pyramidGenerator.isGenerating) yield return null;
        yield return new WaitForSeconds(0.5f);
        PrepareAndStartRain();
    }

    public void PrepareAndStartRain()
    {
        pyramidGenerator.canDrawGPU = false;
        allFallingBlocks.Clear();

        ExtractAllMatrices();

        if (allFallingBlocks.Count == 0) return;

		// We group by Material/Mesh to optimize rendering
		allFallingBlocks = allFallingBlocks
            .OrderBy(b => b.materialID)
            .ThenBy(b => b.meshID)
            .ToList();

		// We calculate the order of appearance by height
		var appearanceOrder = allFallingBlocks
            .Select((block, index) => new { block, originalIndex = index })
            .OrderBy(x => x.block.targetMatrix.m13 + Random.Range(-chaosAmount, chaosAmount))
            .ToList();

        for (int i = 0; i < appearanceOrder.Count; i++)
        {
            var item = appearanceOrder[i];

			// gives us an exact time in seconds, regardless of whether the game runs at 30 or 300 FPS.
			item.block.delay = i / blocksPerSecond;

            item.block.individualDuration = fallDuration + Random.Range(-speedVariation, speedVariation);
        }

        startTime = Time.time;
        isAnimating = true;
    }

    void Update()
    {
        if (!isAnimating || allFallingBlocks.Count == 0) return;

        float currentTime = Time.time - startTime;
        bool stillAnimating = false;

        int currentBatchCount = 0;
        Mesh lastMesh = null;
        Material lastMat = null;

        for (int i = 0; i < allFallingBlocks.Count; i++)
        {
            var block = allFallingBlocks[i];
            float elapsed = currentTime - block.delay;

			// If the block shouldn't appear yet, we skip it.
			if (elapsed < 0) continue;

            Matrix4x4 mat = block.targetMatrix;
            if (elapsed < block.individualDuration)
            {
                stillAnimating = true;
                float t = Mathf.Clamp01(elapsed / block.individualDuration);
                float t_curved = t * t * (3f - 2f * t);

                float finalY = block.targetMatrix.m13;
                mat.m13 = Mathf.Lerp(finalY + dropHeight, finalY, t_curved);
            }

            if (currentBatchCount == 0)
            {
                lastMesh = block.mesh;
                lastMat = block.material;
            }

			// Should we close the current batch and draw?
			if (block.mesh != lastMesh || block.material != lastMat || currentBatchCount >= 1023)
            {
                Graphics.DrawMeshInstanced(lastMesh, 0, lastMat, batchBuffer, currentBatchCount);
                currentBatchCount = 0;
                lastMesh = block.mesh;
                lastMat = block.material;
            }

            batchBuffer[currentBatchCount] = mat;
            currentBatchCount++;
        }

		// Draw the final remainder
		if (currentBatchCount > 0)
        {
            Graphics.DrawMeshInstanced(lastMesh, 0, lastMat, batchBuffer, currentBatchCount);
        }

        if (!stillAnimating && currentTime > (fallDuration + speedVariation + 1.0f))
        {
            isAnimating = false;
            pyramidGenerator.canDrawGPU = true;
        }
    }

    void ExtractAllMatrices()
    {
        foreach (var entry in pyramidGenerator.gpuBatches)
        {
            Material mat = entry.Key;
            Mesh mesh = pyramidGenerator.materialToMesh[mat];
            int matID = mat.GetInstanceID();
            int meshID = mesh.GetInstanceID();

            foreach (var batch in entry.Value)
            {
                foreach (var matrix in batch)
                {
                    allFallingBlocks.Add(new FallingBlock
                    {
                        targetMatrix = matrix,
                        mesh = mesh,
                        material = mat,
                        materialID = matID,
                        meshID = meshID
                    });
                }
            }
        }

        MeshFilter[] allFilters = GameObject.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

        foreach (var mf in allFilters)
        {
			// We only process those that have the tag and are active.
			if (!mf.CompareTag("Block") || !mf.gameObject.activeInHierarchy) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr.enabled && mr.gameObject.activeInHierarchy && mr.CompareTag("Block"))
            {
                if (mf && mf.sharedMesh)
                {
                    allFallingBlocks.Add(new FallingBlock
                    {
                        targetMatrix = mr.transform.localToWorldMatrix,
                        mesh = mf.sharedMesh,
                        material = mr.sharedMaterial,
                        materialID = mr.sharedMaterial.GetInstanceID(),
                        meshID = mf.sharedMesh.GetInstanceID()
                    });

					// We deactivated the object so that it wouldn't interfere with the animation.
					mr.gameObject.SetActive(false);
                }
            }
        }
    }
}