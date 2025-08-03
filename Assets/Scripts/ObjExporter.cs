using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ObjExporter : MonoBehaviour
{
    // Método principal unificado para exportar un GameObject y sus MeshFilters hijos a OBJ
    // La variable 'combineMeshes' controla si se exportan como un solo objeto o individualmente.
    public static void ExportGameObjectToObj(GameObject rootObject, string folderPath, string fileName, bool combineMeshes)
    {
        if (rootObject == null)
        {
            Debug.LogError("El GameObject raíz a exportar es nulo.");
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string objFullPath = Path.Combine(folderPath, fileName + ".obj");
        string mtlFullPath = Path.Combine(folderPath, fileName + ".mtl");

        StringBuilder objSb = new StringBuilder();
        StringBuilder mtlSb = new StringBuilder();

        List<MeshFilter> meshFilters = new List<MeshFilter>();
        rootObject.GetComponentsInChildren(true, meshFilters);

        List<MeshFilter> filteredMeshFilters = new List<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            // Filtrar GameObjects inactivos
            if (!mf.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"Omitiendo GameObject '{mf.name}' de la exportación OBJ porque está inactivo.");
                continue;
            }

            // Filtrar GameObjects con BoxCollider que es 'isTrigger'
            BoxCollider bc = mf.GetComponent<BoxCollider>();
            if (bc != null && bc.isTrigger)
            {
                Debug.LogWarning($"Omitiendo GameObject '{mf.name}' de la exportación OBJ porque tiene un BoxCollider que es 'isTrigger'.");
                continue;
            }

            // Asegurarse de que la malla y el renderizador sean válidos
            if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null || mf.sharedMesh.triangles.Length == 0)
            {
                Debug.LogWarning($"Omitiendo GameObject '{mf.name}' de la exportación OBJ porque no tiene malla válida o Renderer.");
                continue;
            }

            filteredMeshFilters.Add(mf);
        }

        if (filteredMeshFilters.Count == 0)
        {
            Debug.LogWarning($"Después de filtrar, el GameObject '{rootObject.name}' y sus hijos no tienen componentes MeshFilter válidos para exportar a OBJ.");
            return;
        }

        // --- Recopilar materiales únicos para el MTL (común a ambos modos) ---
        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        foreach (MeshFilter mf in filteredMeshFilters)
        {
            Renderer renderer = mf.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterials != null)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null) uniqueMaterials.Add(mat);
                }
            }
        }

        // --- Generar el contenido del archivo MTL (común a ambos modos) ---
        mtlSb.Append("# Material Library Exported from Unity by ObjExporter\n\n");
        foreach (Material mat in uniqueMaterials)
        {
            mtlSb.Append($"newmtl {mat.name}\n");

            Color color = Color.white;
            if (mat.HasProperty("_Color"))
            {
                color = mat.color;
            }
            mtlSb.Append($"Kd {color.r:F4} {color.g:F4} {color.b:F4}\n");

            if (color.a < 1.0f)
            {
                mtlSb.Append($"Tr {color.a:F4}\n");
                mtlSb.Append($"d {color.a:F4}\n");
            }

            if (mat.HasProperty("_MainTex") && mat.mainTexture != null)
            {
#if UNITY_EDITOR
                string textureFileName = mat.mainTexture.name + ".png";
                string textureSourcePath = AssetDatabase.GetAssetPath(mat.mainTexture);
                string textureDestPath = Path.Combine(folderPath, textureFileName);

                if (!string.IsNullOrEmpty(textureSourcePath))
                {
                    try
                    {
                        File.Copy(textureSourcePath, textureDestPath, true);
                        mtlSb.Append($"map_Kd {textureFileName}\n");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"No se pudo copiar la textura '{mat.mainTexture.name}' para el material '{mat.name}': {e.Message}");
                    }
                }
#endif
            }
            mtlSb.Append("\n");
        }


        // --- Lógica condicional para exportar como mallas combinadas o individuales ---
        if (combineMeshes)
        {
            // --- Lógica para Mallas Combinadas ---
            List<Vector3> combinedVertices = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            Dictionary<Material, List<int>> combinedTrianglesByMaterial = new Dictionary<Material, List<int>>();

            foreach (MeshFilter mf in filteredMeshFilters)
            {
                Mesh mesh = mf.sharedMesh;
                Renderer renderer = mf.GetComponent<Renderer>();
                Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;

                int currentVertexOffset = combinedVertices.Count;

                foreach (Vector3 v in mesh.vertices)
                {
                    combinedVertices.Add(localToWorld.MultiplyPoint3x4(v));
                }

                if (mesh.normals.Length > 0)
                {
                    foreach (Vector3 n in mesh.normals)
                    {
                        combinedNormals.Add(localToWorld.MultiplyVector(n).normalized);
                    }
                }

                if (mesh.uv.Length > 0)
                {
                    foreach (Vector2 uv in mesh.uv)
                    {
                        combinedUVs.Add(uv);
                    }
                }

                for (int materialIndex = 0; materialIndex < mesh.subMeshCount; materialIndex++)
                {
                    Material currentMaterial = null;
                    if (renderer.sharedMaterials.Length > materialIndex && renderer.sharedMaterials[materialIndex] != null)
                    {
                        currentMaterial = renderer.sharedMaterials[materialIndex];
                    }
                    // Si no hay material, para el modo combinado, podríamos asignarle un material por defecto
                    // o simplemente ignorar estos triángulos si no queremos un material "null" en el OBJ.
                    // Para este ejemplo, si currentMaterial es null, los triángulos no se añadirán al diccionario
                    // lo que significa que no se exportarán si no tienen un material válido.
                    // Esto es una decisión de diseño; podrías crear un material "default_unassigned" si lo necesitas.

                    if (currentMaterial != null)
                    {
                        if (!combinedTrianglesByMaterial.ContainsKey(currentMaterial))
                        {
                            combinedTrianglesByMaterial.Add(currentMaterial, new List<int>());
                        }

                        int[] triangles = mesh.GetTriangles(materialIndex);
                        for (int i = 0; i < triangles.Length; i++)
                        {
                            combinedTrianglesByMaterial[currentMaterial].Add(triangles[i] + currentVertexOffset);
                        }
                    }
                }
            }

            // --- Encabezado del OBJ Combinado ---
            objSb.Append("# Exportado desde Unity por ObjExporter (Mallas Combinadas)\n");
            objSb.Append($"# Objeto raíz: {rootObject.name}\n\n");
            objSb.Append($"mtllib {fileName}.mtl\n\n");

            objSb.Append($"o {fileName}_Combined\n");
            objSb.Append($"g {fileName}_Combined\n");

            foreach (Vector3 v in combinedVertices)
            {
                objSb.Append(string.Format("v {0} {1} {2}\n", -v.x, v.y, v.z));
            }
            objSb.Append("\n");

            if (combinedNormals.Count > 0)
            {
                foreach (Vector3 n in combinedNormals)
                {
                    objSb.Append(string.Format("vn {0} {1} {2}\n", -n.x, n.y, n.z));
                }
                objSb.Append("\n");
            }

            if (combinedUVs.Count > 0)
            {
                foreach (Vector2 uv in combinedUVs)
                {
                    objSb.Append(string.Format("vt {0} {1}\n", uv.x, uv.y));
                }
                objSb.Append("\n");
            }

            foreach (var entry in combinedTrianglesByMaterial)
            {
                Material mat = entry.Key;
                List<int> triangles = entry.Value;

                objSb.Append($"usemtl {mat.name}\n");
                objSb.Append($"usemap {mat.name}\n");

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    objSb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                        triangles[i] + 1, triangles[i + 1] + 1, triangles[i + 2] + 1));
                }
                objSb.Append("\n");
            }
        }
        else // if (!combineMeshes)
        {
            // --- Lógica para Mallas Individuales (original) ---
            objSb.Append("# Exportado desde Unity por ObjExporter (Mallas Individuales)\n");
            objSb.Append($"# Objeto raíz: {rootObject.name}\n\n");
            objSb.Append($"mtllib {fileName}.mtl\n\n");

            int vertexOffset = 0;

            foreach (MeshFilter mf in filteredMeshFilters)
            {
                Mesh mesh = mf.sharedMesh;
                Renderer renderer = mf.GetComponent<Renderer>();

                Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;

                objSb.Append($"o {mf.name}\n");
                objSb.Append($"g {mf.name}\n");

                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 worldVertex = localToWorld.MultiplyPoint3x4(v);
                    objSb.Append(string.Format("v {0} {1} {2}\n", -worldVertex.x, worldVertex.y, worldVertex.z));
                }
                objSb.Append("\n");

                if (mesh.normals.Length > 0)
                {
                    foreach (Vector3 n in mesh.normals)
                    {
                        Vector3 worldNormal = localToWorld.MultiplyVector(n).normalized;
                        objSb.Append(string.Format("vn {0} {1} {2}\n", -worldNormal.x, worldNormal.y, worldNormal.z));
                    }
                    objSb.Append("\n");
                }

                if (mesh.uv.Length > 0)
                {
                    foreach (Vector2 uv in mesh.uv)
                    {
                        objSb.Append(string.Format("vt {0} {1}\n", uv.x, uv.y));
                    }
                    objSb.Append("\n");
                }

                for (int materialIndex = 0; materialIndex < mesh.subMeshCount; materialIndex++)
                {
                    if (renderer.sharedMaterials.Length > materialIndex && renderer.sharedMaterials[materialIndex] != null)
                    {
                        objSb.Append($"usemtl {renderer.sharedMaterials[materialIndex].name}\n");
                        objSb.Append($"usemap {renderer.sharedMaterials[materialIndex].name}\n");
                    }
                    else
                    {
                        objSb.Append("usemtl default_material\n");
                    }

                    int[] triangles = mesh.GetTriangles(materialIndex);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        objSb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                            triangles[i] + 1 + vertexOffset,
                            triangles[i + 1] + 1 + vertexOffset,
                            triangles[i + 2] + 1 + vertexOffset));
                    }
                    objSb.Append("\n");
                }
                vertexOffset += mesh.vertices.Length;
            }
        }

        // --- Escribir los archivos ---
        try
        {
            File.WriteAllText(objFullPath, objSb.ToString());
            File.WriteAllText(mtlFullPath, mtlSb.ToString());
            Debug.Log($"GameObject '{rootObject.name}' exportado a OBJ ({(combineMeshes ? "Combinado" : "Individual")}) exitosamente a:\nOBJ: '{objFullPath}'\nMTL: '{mtlFullPath}'");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al exportar GameObject '{rootObject.name}' a OBJ/MTL: {e.Message}");
        }
    }
}