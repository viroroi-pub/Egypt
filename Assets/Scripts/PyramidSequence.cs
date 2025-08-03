using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class PyramidSequence : MonoBehaviour
{
    private GeneratePyramid GeneratePyramid;
 
    public bool capture= true; // Variable para controlar la captura de pantalla
    public float lapse = 0.2f; // Tiempo de espera entre capturas

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GeneratePyramid = GetComponent<GeneratePyramid>();
        GeneratePyramid.DrawUntilRow = true;
        GeneratePyramid.DrawRow = 0;
        GeneratePyramid.Dromader = null;
        GeneratePyramid.Palm = null;
        GeneratePyramid.Egyptian_body = null;
        GeneratePyramid.stone_sled = null;

        for (int i = GeneratePyramid.objParent.transform.childCount - 1; i >= 0; i--)
        {
            // Destroy the child GameObject.
            GameObject.Destroy(GeneratePyramid.objParent.transform.GetChild(i).gameObject);
        }

        StartCoroutine(RunSequence());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator RunSequence()
    {
        int ticks = 0;
        GeneratePyramid.DrawRow = 0;
        GeneratePyramid.total_height = 0;

        // La condición está directamente en el bucle.
        while (GeneratePyramid.total_height < GeneratePyramid.Height)
        {
            ticks++;
            Debug.Log($"Tick número: {ticks}");
            
            for (int i = GeneratePyramid.objParent.transform.childCount - 1; i >= 0; i--)
            {
                // delete only is trigger
                if (GeneratePyramid.DrawOnlyRow)
                {
                    BoxCollider bc = GeneratePyramid.objParent.transform.GetChild(i).gameObject.GetComponent<BoxCollider>();
                    if (bc && bc.isTrigger)
                        GameObject.Destroy(GeneratePyramid.objParent.transform.GetChild(i).gameObject);
                }
                else
                    // Destroy the child GameObject.
                    GameObject.Destroy(GeneratePyramid.objParent.transform.GetChild(i).gameObject);
            }

            GeneratePyramid.cam.transform.localPosition = new Vector3(-GeneratePyramid.BaseSize * 3 / 4, GeneratePyramid.Height * 3 / 4, -GeneratePyramid.BaseSize * 3 / 4);
            //cam.transform.localPosition = new Vector3(BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, -BaseSize);

            GeneratePyramid.total_height = 0;            
            GeneratePyramid.compute_size();

            Debug.Log("Total height " + GeneratePyramid.total_height);

            yield return new WaitForSeconds(lapse);

            if (capture)
            {
                StartCoroutine(CaptureAndSave());

                yield return new WaitForSeconds(lapse);
            }

            GeneratePyramid.DrawRow++;
        }

        Debug.Log("La corrutina ha finalizado.");
    }

    private IEnumerator CaptureAndSave()
    {
        // Espera hasta el final del frame actual.
        // Esto es crucial para asegurar que toda la renderización, incluyendo la UI, esté completa.
        yield return new WaitForEndOfFrame();

        // 1. Crear una RenderTexture con las dimensiones de la pantalla.
        // Una RenderTexture es una textura en la que la cámara puede dibujar directamente.
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);

        // 2. Asignar temporalmente esta RenderTexture a la cámara.
        GeneratePyramid.cam.targetTexture = renderTexture;
        GeneratePyramid.cam.Render(); // Forzar a la cámara a renderizar en nuestra RenderTexture.

        // 3. Restaurar la configuración original de la cámara.
        GeneratePyramid.cam.targetTexture = null;

        // 4. Leer los píxeles de la RenderTexture.
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply(); // Aplica los cambios a la textura.
        RenderTexture.active = null; // Liberar la RenderTexture activa.
        Destroy(renderTexture); // Limpiar la RenderTexture de la memoria.

        // 5. Codificar la textura a formato PNG.
        // El resultado es un array de bytes que representa el archivo de imagen.
        byte[] bytes = screenshot.EncodeToPNG();
        Destroy(screenshot); // Limpiar la textura de la memoria.

        // 6. Definir la ruta y el nombre del archivo.
        // Usamos Application.persistentDataPath, que es una carpeta segura y escribible en todas las plataformas.
        string folderPath = Application.persistentDataPath;
        string fileName = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        string filePath = Path.Combine(folderPath, fileName);

        // 7. Guardar el archivo en disco.
        File.WriteAllBytes(filePath, bytes);

        // 8. Mostrar un mensaje de confirmación en la consola con la ruta del archivo.
        Debug.Log($"¡Captura de pantalla guardada! Ruta: {filePath}");
    }
}
