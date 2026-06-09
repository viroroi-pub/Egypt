using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidEffectManager : MonoBehaviour
{
    [Header("Referencias")]
    public GeneratePyramid pyramidGenerator;

    [Header("Configuración del Efecto")]
    [Tooltip("Altura extra base que se elevarán los bloques.")]
    public float levitationHeight = 50f;

    [Tooltip("Tiempo total que dura la transición de toda la pirámide.")]
    public float waveDuration = 5.0f;

    [Tooltip("Tiempo que se quedan flotando antes de bajar.")]
    public float hoverTime = 8.0f;

    [Tooltip("Capa de los bloques para detectarlos (ej. Default o Blocks)")]
    public LayerMask blockLayer;

    [Header("Configuración de Auto-Inicio")]
    [Tooltip("Si es true, el efecto se lanza solo cuando la pirámide termina de generarse.")]
    public bool autoStartEffect = true;

    [Tooltip("Segundos a esperar después de que termine la generación para iniciar el efecto.")]
    public float autoStartDelay = 3.0f;

    private List<BlockLevitation> affectedBlocks = new List<BlockLevitation>();
    private bool isEffectRunning = false;

    private void Start()
    {
        // Iniciamos el monitor de estado si está activada la opción
        if (autoStartEffect)
        {
            StartCoroutine(MonitorGenerationStatus());
        }
    }

    /// <summary>
    /// Vigila el estado de GeneratePyramid para lanzar el efecto automáticamente
    /// </summary>
    private IEnumerator MonitorGenerationStatus()
    {
        while (true)
        {
            if (pyramidGenerator != null)
            {
                // Esperar a que COMIENCE la generación (isGenerating se vuelve true)
                // Esto es necesario para no disparar el efecto nada más dar al Play si ya hay una pirámide vieja
                while (!pyramidGenerator.isGenerating)
                {
                    yield return null;
                }

                // Esperar a que TERMINE la generación (isGenerating se vuelve false)
                while (pyramidGenerator.isGenerating)
                {
                    yield return null;
                }

                // La generación ha terminado. Esperar el tiempo de delay definido.
                Debug.Log($"Generación completada. Esperando {autoStartDelay} segundos para iniciar efecto...");
                yield return new WaitForSeconds(autoStartDelay);

                // Iniciar la secuencia si está habilitado y no está corriendo ya
                if (autoStartEffect && !isEffectRunning)
                {
                    StartCoroutine(RunDeconstructionSequence());
                }
            }
            else
            {
                yield return null;
            }
        }
    }

    public IEnumerator RunDeconstructionSequence()
    {
        if (isEffectRunning) yield break;
        isEffectRunning = true;

        // Preparar los bloques (Añadir script si no lo tienen)
        PrepareBlocks();

        if (affectedBlocks.Count == 0)
        {
            Debug.LogWarning("No se encontraron bloques con el tag 'block' para animar.");
            isEffectRunning = false;
            yield break;
        }

        // Calcular la altura máxima actual para normalizar los tiempos
        float maxY = 0f;
        foreach (var block in affectedBlocks)
        {
            if (block.transform.position.y > maxY) maxY = block.transform.position.y;
        }

        Debug.Log($"Iniciando elevación de {affectedBlocks.Count} bloques. Altura Max: {maxY}");

        // Fase de ELEVACIÓN (Deconstrucción)
        // Los de arriba (Y cercano a maxY) tienen delay 0.
        // Los de abajo (Y cercano a 0) tienen delay máximo.
        foreach (var block in affectedBlocks)
        {
            float currentY = block.transform.position.y;

            // Inverso: Cuanto más alto, MENOR delay.
            // (maxY - currentY) da 0 para el tope, y un valor alto para la base.
            float normalizedHeight = (maxY - currentY) / maxY; // 0 arriba, 1 abajo

            // Añadimos un poco de ruido aleatorio (Random.value * 0.5f) para que no sea una línea perfecta
            float delay = (normalizedHeight * waveDuration) + (Random.value * 0.5f);

            block.Levitate(delay, levitationHeight);
        }

        // Esperar mientras flotan
        yield return new WaitForSeconds(waveDuration + hoverTime);

        Debug.Log("Iniciando reconstrucción...");

        // Fase de RECONSTRUCCIÓN (Bajada)
        // Los de abajo (Y bajo) caen primero. Los de arriba caen al final.
        foreach (var block in affectedBlocks)
        {
            float currentY = block.transform.position.y; // Usamos la posición Y original teórica, o podríamos haberla guardado.
                                                         // Nota: BlockLevitation guarda su originalPosition, pero para calcular el delay usamos la altura relativa.
                                                         // Como queremos que los de abajo caigan primero:
                                                         // Cuanto más bajo sea Y, MENOR delay.

            float normalizedHeight = currentY / maxY; // 0 abajo, 1 arriba

            float delay = (normalizedHeight * waveDuration) + (Random.value * 0.5f);

            block.ReturnToBase(delay);
        }

        yield return new WaitForSeconds(waveDuration + 2.0f);
        isEffectRunning = false;
        Debug.Log("Ciclo completado.");
    }

    private void PrepareBlocks()
    {
        affectedBlocks.Clear();

        // Buscar todos los objetos en la escena que tengan el tag "Block"
        GameObject[] foundBlocks = GameObject.FindGameObjectsWithTag("Block");

        if (foundBlocks.Length > 0)
        {
            foreach (GameObject obj in foundBlocks)
            {
                // Verificación opcional: 
                // Asegurarse de que estos bloques pertenecen a nuestra pirámide generada
                // para evitar mover objetos que no tocan.
                if (pyramidGenerator != null && pyramidGenerator.objParent != null)
                {
                    if (!obj.transform.IsChildOf(pyramidGenerator.objParent.transform))
                    {
                        continue; // Si no es hijo de la pirámide actual, lo ignoramos
                    }
                }

                BlockLevitation bl = obj.GetComponent<BlockLevitation>();

                // Si el bloque no tiene el script de levitación, se lo añadimos
                if (bl == null)
                {
                    bl = obj.AddComponent<BlockLevitation>();
                }

                affectedBlocks.Add(bl);
            }
        }
        else
        {
            Debug.LogWarning("No se encontraron objetos con el tag 'block'. Asegúrate de asignar el Tag a tus prefabs.");
        }
    }
}