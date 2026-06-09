using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ConstructionEffectManager : MonoBehaviour
{
    [Header("References")]
    public GeneratePyramid pyramidGenerator;

    [Header("Rain Effect Configuration")]
    [Tooltip("Height from which blocks will fall (meters above final position).")]
    public float dropHeight = 100f;

    [Tooltip("Time it takes for a single block to fall.")]
    public float fallDuration = 1.0f;

    [Tooltip("Delay between layers/groups of blocks.")]
    public float layerDelay = 0.05f;

    [Tooltip("Time to remain hidden before rain starts.")]
    public float hiddenTime = 1.0f;

    [Tooltip("Number of blocks processed per frame during setup (prevents freezing).")]
    public int setupBatchSize = 1000;

    [Header("Auto-Start Configuration")]
    public bool autoStartEffect = true;
    public float autoStartDelay = 3.0f;

    // --- Estructura interna para manejar el movimiento sin Corrutinas ---
    private struct MovingBlock
    {
        public PyramidBlockController controller;
        public Vector3 startPos;
        public Vector3 targetPos;
        public float startTime;
        public float duration;
    }

    private List<PyramidBlockController> allBlocksSorted = new List<PyramidBlockController>();
    private List<MovingBlock> activeMovingBlocks = new List<MovingBlock>();

    private bool isEffectRunning = false;
    private bool isSetupComplete = false;

    private void Start()
    {
        if (autoStartEffect)
        {
            StartCoroutine(MonitorGenerationStatus());
        }
    }

    private void Update()
    {
        // Bucle centralizado de movimiento (Mucho más rápido que 10k corrutinas)
        if (isEffectRunning && activeMovingBlocks.Count > 0)
        {
            float currentTime = Time.time;

            // Iteramos hacia atrás para poder eliminar elementos de la lista eficientemente
            for (int i = activeMovingBlocks.Count - 1; i >= 0; i--)
            {
                MovingBlock mb = activeMovingBlocks[i];
                float elapsed = currentTime - mb.startTime;

                if (elapsed < mb.duration)
                {
                    // Interpolación cuadrática para efecto de gravedad (aceleración)
                    float t = elapsed / mb.duration;
                    float t_curved = t * t;

                    // Movemos el transform directamente
                    mb.controller.transform.position = Vector3.LerpUnclamped(mb.startPos, mb.targetPos, t_curved);
                }
                else
                {
                    // Ha terminado: Aseguramos posición final y eliminamos de la lista activa
                    mb.controller.transform.position = mb.targetPos;
                    activeMovingBlocks.RemoveAt(i);
                }
            }
        }
    }

    private IEnumerator MonitorGenerationStatus()
    {
        while (true)
        {
            if (pyramidGenerator != null)
            {
                // Esperar inicio
                while (!pyramidGenerator.isGenerating) yield return null;
                // Esperar fin
                while (pyramidGenerator.isGenerating) yield return null;

                Debug.Log($"Generation complete. Waiting {autoStartDelay}s...");
                yield return new WaitForSeconds(autoStartDelay);

                if (autoStartEffect && !isEffectRunning)
                {
                    StartCoroutine(RunOptimizedSequence());
                }
                yield break;
            }
            yield return null;
        }
    }

    public IEnumerator RunOptimizedSequence()
    {
        if (isEffectRunning) yield break;
        isEffectRunning = true;
        activeMovingBlocks.Clear();

        // Preparación Asíncrona (evita el "congelamiento" inicial)
        yield return StartCoroutine(PrepareBlocksAsync());

        if (allBlocksSorted.Count == 0)
        {
            Debug.LogWarning("No blocks found.");
            isEffectRunning = false;
            yield break;
        }

        yield return new WaitForSeconds(hiddenTime);

        Debug.Log("Starting optimized rain...");

        // Bucle de Disparo (Spawning Loop)
        // Solo añade bloques a la lista de "activos", el Update los mueve.
        float currentHeightThreshold = -9999f;
        float heightTolerance = 0.5f;

        // Recorremos la lista ordenada
        int batchCount = 0;
        for (int i = 0; i < allBlocksSorted.Count; i++)
        {
            PyramidBlockController block = allBlocksSorted[i];
            float originalY = block.GetOriginalHeight();

            // Detectar cambio de capa
            if (originalY > currentHeightThreshold + heightTolerance)
            {
                currentHeightThreshold = originalY;
                yield return new WaitForSeconds(layerDelay);
            }

            // Añadir a la lista de movimiento (Logic Centralizada)
            ActivateBlockFalling(block);

            // Pequeña optimización: si lanzamos muchísimos en el mismo frame, espera un frame
            batchCount++;
            if (batchCount > 100)
            {
                batchCount = 0;
                yield return null;
            }
        }

        // Esperar a que terminen de caer los últimos
        while (activeMovingBlocks.Count > 0)
        {
            yield return null;
        }

        isEffectRunning = false;
        Debug.Log("Construction complete.");
    }

    private void ActivateBlockFalling(PyramidBlockController block)
    {
        block.SetVisible(true);

        Vector3 finalPos = block.transform.position; // Asumimos que está en su posición original (reseteada en Prepare)
        Vector3 startPos = finalPos + Vector3.up * dropHeight;

        // Colocamos arriba inmediatamente
        block.transform.position = startPos;

        // Añadimos a la lista del Update
        MovingBlock mb = new MovingBlock();
        mb.controller = block;
        mb.startPos = startPos;
        mb.targetPos = finalPos;
        mb.startTime = Time.time;
        // Pequeña variación random en la duración para naturalidad
        mb.duration = fallDuration * Random.Range(0.8f, 1.2f);

        activeMovingBlocks.Add(mb);
    }

    // --- Preparación optimizada para no congelar la pantalla ---
    private IEnumerator PrepareBlocksAsync()
    {
        Debug.Log("Preparing blocks (Async)...");
        allBlocksSorted.Clear();

        GameObject[] foundBlocks = GameObject.FindGameObjectsWithTag("Block");

        if (foundBlocks.Length == 0) yield break;

        int processed = 0;

        // Recopilar y añadir componente (si falta)
        foreach (GameObject obj in foundBlocks)
        {
            if (pyramidGenerator != null && pyramidGenerator.objParent != null)
            {
                if (!obj.transform.IsChildOf(pyramidGenerator.objParent.transform)) continue;
            }

            PyramidBlockController bl = obj.GetComponent<PyramidBlockController>();
            if (bl == null) bl = obj.AddComponent<PyramidBlockController>();

            bl.Initialize(); // Guarda posición original
            bl.SetVisible(false); // Ocultar

            allBlocksSorted.Add(bl);

            processed++;
            // Cada X bloques, devolvemos el control a Unity para que renderice un frame
            if (processed >= setupBatchSize)
            {
                processed = 0;
                yield return null;
            }
        }

        // Ordenar por altura
        // El Sort de List es muy rápido (QuickSort), pero si son 50k elementos podría notarse un tirón.
        // Lo dejamos directo porque suele ser aceptable (<100ms para 50k ints).
        allBlocksSorted.Sort((a, b) => a.GetOriginalHeight().CompareTo(b.GetOriginalHeight()));

        Debug.Log($"Blocks prepared: {allBlocksSorted.Count}");
    }
}