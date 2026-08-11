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
    public int startRow = 0;
    public int endRow = 1000;

    [Header("Orbit Camera Settings")]
    public bool orbitCamera = false;          
    public float orbitSpeed = 15f;            
    public float orbitDistanceFactor = 1.5f;  
    public float orbitHeightFactor = 0.5f;    

    private float currentOrbitAngle = 0f;

    private float HeightCornerInitialRamp = 0.0f;
    private float HeightCornerInitialRamp2 = 0.0f;
    private float HeightCornerInitialRamp3 = 0.0f;
    private float HeightCornerInitialRamp4 = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GeneratePyramid = GetComponent<GeneratePyramid>();
        GeneratePyramid.DrawUntilRow = true;
        GeneratePyramid.DrawRow = startRow;
        GeneratePyramid.Dromader = null;
        GeneratePyramid.Palm = null;
        GeneratePyramid.Egyptian_body = null;
        GeneratePyramid.stone_sled = null;
        GeneratePyramid.Sequenced = true;
        GeneratePyramid.useGPUInstancing = false;

        HeightCornerInitialRamp = GeneratePyramid.HeightCornerInitialRamp;
        HeightCornerInitialRamp2 = GeneratePyramid.HeightCornerInitialRamp2;
        HeightCornerInitialRamp3 = GeneratePyramid.HeightCornerInitialRamp3;
        HeightCornerInitialRamp4 = GeneratePyramid.HeightCornerInitialRamp4;

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
        GeneratePyramid.DrawRow = startRow;
        GeneratePyramid.total_height = 0;

        // La condición está directamente en el bucle.
        while (GeneratePyramid.total_height < GeneratePyramid.Height || GeneratePyramid.DrawRow<endRow)
        {
            ticks++;
            Debug.Log($"Tick número: {ticks}");

            // delete previous row
            GeneratePyramid.ClearPyramid(false);

            //GeneratePyramid.cam.transform.localPosition = new Vector3(-GeneratePyramid.BaseSize * 3 / 4, GeneratePyramid.Height * 3 / 4, -GeneratePyramid.BaseSize * 3 / 4);
            //cam.transform.localPosition = new Vector3(BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, BaseSize);
            //cam.transform.localPosition = new Vector3(-BaseSize, Height, -BaseSize);

            GeneratePyramid.HeightCornerInitialRamp = HeightCornerInitialRamp;
            GeneratePyramid.HeightCornerInitialRamp2 = HeightCornerInitialRamp2;
            GeneratePyramid.HeightCornerInitialRamp3 = HeightCornerInitialRamp3;
            GeneratePyramid.HeightCornerInitialRamp4 = HeightCornerInitialRamp4;

            Debug.Log("DrawRow:" + GeneratePyramid.DrawRow);
            Debug.Log("HeightCornerInitialRamp:" + HeightCornerInitialRamp);
            Debug.Log("HeightCornerInitialRamp2:" + HeightCornerInitialRamp2);
            Debug.Log("HeightCornerInitialRamp3:" + HeightCornerInitialRamp3);
            Debug.Log("HeightCornerInitialRamp4:" + HeightCornerInitialRamp4);

            GeneratePyramid.total_height = 0;
            yield return StartCoroutine(GeneratePyramid.compute_size());

            yield return new WaitUntil(() => !GeneratePyramid.isGenerating);

            Debug.Log("Total height " + GeneratePyramid.total_height);

            if (orbitCamera)
            {
                currentOrbitAngle += orbitSpeed * lapse;
                float rad = currentOrbitAngle * Mathf.Deg2Rad;

                Vector3 targetCenter = new Vector3(0, GeneratePyramid.total_height / 2f, 0);

                float distance = GeneratePyramid.BaseSize * orbitDistanceFactor;
                float camHeight = GeneratePyramid.Height * orbitHeightFactor;

                Vector3 newCamPos = targetCenter + new Vector3(Mathf.Sin(rad) * distance, camHeight, Mathf.Cos(rad) * distance);

                GeneratePyramid.cam.transform.position = newCamPos;
                GeneratePyramid.cam.transform.LookAt(targetCenter);
            }
            else
            if (GeneratePyramid.cameraPositionFace == CameraPositionFace.InfrontRamp)
            {
                Debug.Log("Ramp Target Position : " + GeneratePyramid.lastRampMidPoint.ToString());
                Vector3 lastRampMidPointCam = GeneratePyramid.GetTargetPositionFromCenter(GeneratePyramid.lastRampMidPoint, GeneratePyramid.BaseSize);
                Debug.Log("Camera Ramp Target Position : " + lastRampMidPointCam.ToString());
                GeneratePyramid.cam.transform.localPosition = lastRampMidPointCam;
                GeneratePyramid.cam.transform.LookAt(GeneratePyramid.lastRampMidPoint);
            }

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

        // Crear una RenderTexture con las dimensiones de la pantalla.
        // Una RenderTexture es una textura en la que la cámara puede dibujar directamente.
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);

        // Asignar temporalmente esta RenderTexture a la cámara.
        GeneratePyramid.cam.targetTexture = renderTexture;
        GeneratePyramid.cam.Render(); // Forzar a la cámara a renderizar en nuestra RenderTexture.

        // Restaurar la configuración original de la cámara.
        GeneratePyramid.cam.targetTexture = null;

        // Leer los píxeles de la RenderTexture.
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply(); // Aplica los cambios a la textura.
        RenderTexture.active = null; // Liberar la RenderTexture activa.
        Destroy(renderTexture); // Limpiar la RenderTexture de la memoria.

        // Codificar la textura a formato PNG.
        // El resultado es un array de bytes que representa el archivo de imagen.
        byte[] bytes = screenshot.EncodeToPNG();
        Destroy(screenshot); // Limpiar la textura de la memoria.

        // Definir la ruta y el nombre del archivo.
        // Usamos Application.persistentDataPath, que es una carpeta segura y escribible en todas las plataformas.
        string folderPath = Application.persistentDataPath;
        string fileName = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        string filePath = Path.Combine(folderPath, fileName);

        // Guardar el archivo en disco.
        File.WriteAllBytes(filePath, bytes);

        // Mostrar un mensaje de confirmación en la consola con la ruta del archivo.
        Debug.Log($"¡Captura de pantalla guardada! Ruta: {filePath}");
    }
}
