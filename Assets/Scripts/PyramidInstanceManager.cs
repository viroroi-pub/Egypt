using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Motor gráfico centralizado.
/// Recibe datos de bloques desde GeneratePyramid y los dibuja usando GPU Instancing.
/// Incluye la lógica de animación de construcción (caída).
/// </summary>
public class PyramidInstanceManager : MonoBehaviour
{
    // Singleton para acceso fácil
    public static PyramidInstanceManager Instance;

    [Header("Configuración de Animación")]
    public bool animateConstruction = false;
    public float dropHeight = 100f;
    public float fallDuration = 1.0f;
    public float layerDelay = 0.05f;
    public float startDelay = 2.0f;

    // --- Estructuras Internas ---
    
    // Identificador único para agrupar (Mesh + Material)
    struct BatchKey
    {
        public int meshID;
        public int matID;
        // Override de Equals y HashCode para usar en Diccionario
        public override int GetHashCode() { return meshID ^ matID; }
        public override bool Equals(object obj) { return obj is BatchKey && this == (BatchKey)obj; }
        public static bool operator ==(BatchKey x, BatchKey y) { return x.meshID == y.meshID && x.matID == y.matID; }
        public static bool operator !=(BatchKey x, BatchKey y) { return !(x == y); }
    }

    // Grupo de renderizado
    class BatchGroup
    {
        public Mesh mesh;
        public Material material;
        
        // Matrices de bloques que ya han aterrizado (estáticos)
        public List<Matrix4x4> landedMatrices = new List<Matrix4x4>();
        
        // Bloques que están cayendo o esperando caer
        public List<FallingBlock> fallingBlocks = new List<FallingBlock>();
    }

    class FallingBlock
    {
        public Matrix4x4 targetMatrix; // Dónde debe acabar
        public Vector3 targetPos;      // Posición final caché
        public Quaternion rotation;    // Rotación caché
        public Vector3 scale;          // Escala caché
        public float originalY;        // Altura para ordenar
        public float startTime;        // Cuándo empieza a caer (-1 si espera)
        public bool isFalling;
    }

    // Almacén principal
    private Dictionary<BatchKey, BatchGroup> batches = new Dictionary<BatchKey, BatchGroup>();
    private bool isGenerating = false;
    private bool isAnimating = false;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Llamar a esto antes de empezar a generar una nueva pirámide
    /// </summary>
    public void ClearAll()
    {
        batches.Clear();
        isGenerating = true;
        isAnimating = false;
        StopAllCoroutines();
    }

    /// <summary>
    /// Método optimizado para registrar un bloque sin crear GameObject.
    /// </summary>
    public void AddBlock(GameObject prefabRef, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (!prefabRef) return;

        MeshFilter mf = prefabRef.GetComponent<MeshFilter>();
        MeshRenderer mr = prefabRef.GetComponent<MeshRenderer>();

        if (!mf || !mr) return;

        // Comprobación de seguridad GPU Instancing
        if (!mr.sharedMaterial.enableInstancing)
        {
            Debug.LogWarning($"El material de {prefabRef.name} no tiene GPU Instancing activado.");
            return;
        }

        BatchKey key = new BatchKey { meshID = mf.sharedMesh.GetInstanceID(), matID = mr.sharedMaterial.GetInstanceID() };

        if (!batches.ContainsKey(key))
        {
            batches[key] = new BatchGroup { mesh = mf.sharedMesh, material = mr.sharedMaterial };
        }

        FallingBlock block = new FallingBlock
        {
            targetPos = position,
            rotation = rotation,
            scale = scale,
            // Pre-calculamos la matriz final para ahorrar CPU luego
            targetMatrix = Matrix4x4.TRS(position, rotation, scale),
            originalY = position.y,
            startTime = -1f,
            isFalling = false
        };

        batches[key].fallingBlocks.Add(block);
    }

    /// <summary>
    /// Llamar cuando GeneratePyramid termine su bucle
    /// </summary>
    public void FinalizeGeneration()
    {
        isGenerating = false;
        
        // Ordenar todos los bloques por altura para la animación
        foreach (var batch in batches.Values)
        {
            batch.fallingBlocks.Sort((a, b) => a.originalY.CompareTo(b.originalY));
        }

        if (animateConstruction)
        {
            StartCoroutine(RunConstructionSequence());
        }
        else
        {
            // Si no hay animación, pasamos todo a "landed" inmediatamente
            foreach (var batch in batches.Values)
            {
                foreach(var b in batch.fallingBlocks)
                {
                    batch.landedMatrices.Add(b.targetMatrix);
                }
                batch.fallingBlocks.Clear();
            }
        }
    }

    IEnumerator RunConstructionSequence()
    {
        Debug.Log("[Instancer] Preparando animación...");
        yield return new WaitForSeconds(startDelay);
        isAnimating = true;

        float currentHeightThreshold = -9999f;
        float heightTolerance = 0.5f;

        // Lista plana de todos los bloques pendientes de todas las batches para orquestar la caída global
        // (Esto consume un poco de memoria temporal, pero asegura el orden visual correcto entre tipos de bloque)
        List<(FallingBlock block, BatchGroup batch)> constructionQueue = new List<(FallingBlock, BatchGroup)>();
        
        foreach (var batch in batches.Values)
        {
            foreach (var block in batch.fallingBlocks)
            {
                constructionQueue.Add((block, batch));
            }
        }

        // Ordenar globalmente
        constructionQueue.Sort((a, b) => a.block.originalY.CompareTo(b.block.originalY));

        Debug.Log($"[Instancer] Iniciando caída de {constructionQueue.Count} bloques.");
        float startTime = Time.time;

        int index = 0;
        while (index < constructionQueue.Count)
        {
            var item = constructionQueue[index];
            
            // Control de capas
            if (item.block.originalY > currentHeightThreshold + heightTolerance)
            {
                if (currentHeightThreshold > -9000f)
                {
                    yield return new WaitForSeconds(layerDelay);
                }
                currentHeightThreshold = item.block.originalY;
            }

            // Activar bloque
            item.block.startTime = Time.time;
            item.block.isFalling = true;
            
            index++;
            
            // Límite de activaciones por frame para suavidad
            if (index % 500 == 0) yield return null;
        }
    }

    void Update()
    {
        // Renderizado constante
        float time = Time.time;
        
        foreach (var batch in batches.Values)
        {
            // Dibujar bloques estáticos (ya aterrizados)
            DrawBatch(batch.mesh, batch.material, batch.landedMatrices);

            // Animar y dibujar bloques cayendo
            if (batch.fallingBlocks.Count > 0)
            {
                List<Matrix4x4> activeFrameMatrices = new List<Matrix4x4>();
                
                // Iterar al revés para poder moverlos a "landed" eficientemente
                for (int i = batch.fallingBlocks.Count - 1; i >= 0; i--)
                {
                    FallingBlock fb = batch.fallingBlocks[i];

                    // Si aún no ha empezado (startTime == -1), no se dibuja (invisible)
                    if (fb.startTime < 0) continue;

                    float elapsed = time - fb.startTime;
                    
                    if (elapsed < fallDuration)
                    {
                        // En el aire
                        float t = elapsed / fallDuration;
                        float t_curved = t * t; // Gravedad

                        Vector3 startPos = fb.targetPos + Vector3.up * dropHeight;
                        Vector3 currentPos = Vector3.LerpUnclamped(startPos, fb.targetPos, t_curved);
                        
                        // Crear matriz temporal para este frame
                        activeFrameMatrices.Add(Matrix4x4.TRS(currentPos, fb.rotation, fb.scale));
                    }
                    else
                    {
                        // Aterrizado
                        batch.landedMatrices.Add(fb.targetMatrix);
                        batch.fallingBlocks.RemoveAt(i); // Sacar de la lista de pendientes
                        
                        // (Opcional: Añadir un pequeño DrawMesh aquí si se nota parpadeo en el cambio de lista,
                        // pero al ser secuencial en el frame suele ir bien)
                    }
                }

                // Dibujar los que están en el aire
                DrawBatch(batch.mesh, batch.material, activeFrameMatrices);
            }
        }
    }

    // Helper para dibujar lotes de 1023
    void DrawBatch(Mesh mesh, Material mat, List<Matrix4x4> matrices)
    {
        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            var batchList = matrices.GetRange(i, count);
            Graphics.DrawMeshInstanced(mesh, 0, mat, batchList);
        }
    }
}