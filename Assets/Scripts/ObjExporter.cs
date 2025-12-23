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

        // --- 1. Filter and Sort Meshes ---
        List<MeshFilter> allMeshFilters = new List<MeshFilter>();
        rootObject.GetComponentsInChildren(true, allMeshFilters);

        List<MeshFilter> nonTriggerMeshes = new List<MeshFilter>();
        List<MeshFilter> triggerMeshes = new List<MeshFilter>();
        List<MeshFilter> rampsMeshes = new List<MeshFilter>();

        foreach (MeshFilter mf in allMeshFilters)
        {
            // Skip inactive GameObjects
            if (!mf.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Skip meshes with no renderer or no valid mesh
            if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null || mf.sharedMesh.triangles.Length == 0)
            {
                Debug.LogWarning($"Skipping GameObject '{mf.name}' from OBJ export: no valid mesh or Renderer.");
                continue;
            }

            // Check for BoxCollider trigger
            BoxCollider bc = mf.GetComponent<BoxCollider>();
            if (bc != null && bc.isTrigger)
            {
                // This is a trigger, add it to the trigger list
                triggerMeshes.Add(mf);
            }
            else
            if (mf.tag == "Ramp")
            {
                // This is a ramp mesh
                rampsMeshes.Add(mf);
            }
            else
            {
                // This is a normal, non-trigger mesh
                nonTriggerMeshes.Add(mf);
            }
        }

        // --- 2. Export the Non-Trigger (Visual) Meshes ---
        if (nonTriggerMeshes.Count > 0)
        {
            WriteMeshListToObj(nonTriggerMeshes, folderPath, fileName, combineMeshes);
        }
        else
        {
            Debug.LogWarning($"No non-trigger meshes found to export for '{fileName}'.");
        }

        // --- 3. Export the Trigger Meshes to a separate file ---
        if (triggerMeshes.Count > 0)
        {
            string triggerFileName = fileName + "_Triggers";
            WriteMeshListToObj(triggerMeshes, folderPath, triggerFileName, combineMeshes);
        }

        // --- 4. Export the Ramp Meshes to a separate file ---
        if (rampsMeshes.Count > 0)
        {
            string triggerFileName = fileName + "_Ramps";
            WriteMeshListToObj(rampsMeshes, folderPath, triggerFileName, combineMeshes);
        }
    }

    /// <summary>
    /// Private helper function that performs the actual mesh data collection and file writing.
    /// </summary>
    private static void WriteMeshListToObj(List<MeshFilter> meshList, string folderPath, string fileName, bool combineMeshes)
    {
        string objFullPath = Path.Combine(folderPath, fileName + ".obj");
        string mtlFullPath = Path.Combine(folderPath, fileName + ".mtl");

        StringBuilder objSb = new StringBuilder();
        StringBuilder mtlSb = new StringBuilder();

        // --- Collect unique materials for the MTL ---
        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        foreach (MeshFilter mf in meshList)
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

        // --- Generate MTL file content ---
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
                        Debug.LogWarning($"Could not copy texture '{mat.mainTexture.name}' for material '{mat.name}': {e.Message}");
                    }
                }
#endif
            }
            mtlSb.Append("\n");
        }

        // Add a default material for meshes that might not have one (like triggers)
        if (uniqueMaterials.Count == 0)
        {
            mtlSb.Append("newmtl default_material\n");
            mtlSb.Append("Kd 0.8 0.8 0.8\n");
        }

        // --- Logic to export as combined or individual meshes ---
        if (combineMeshes)
        {
            // --- Combined Mesh Logic ---
            List<Vector3> combinedVertices = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            // Use 'Material' (nullable) as key to handle null materials
            Dictionary<Material, List<int>> combinedTrianglesByMaterial = new Dictionary<Material, List<int>>();

            foreach (MeshFilter mf in meshList)
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
                    // Handle null or out-of-bounds materials
                    Material currentMaterial = null;
                    if (renderer.sharedMaterials.Length > materialIndex && renderer.sharedMaterials[materialIndex] != null)
                    {
                        currentMaterial = renderer.sharedMaterials[materialIndex];
                    }

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

            // --- Write Combined OBJ ---
            objSb.Append("# Exported from Unity by ObjExporter (Combined Meshes)\n");
            objSb.Append($"mtllib {fileName}.mtl\n\n");
            objSb.Append($"o {fileName}_Combined\n");
            objSb.Append($"g {fileName}_Combined\n");

            foreach (Vector3 v in combinedVertices)
            {
                objSb.Append(string.Format("v {0} {1} {2}\n", -v.x, v.y, v.z)); // Flipped X-axis for Unity to standard OBJ
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

                if (mat != null)
                {
                    objSb.Append($"usemtl {mat.name}\n");
                    objSb.Append($"usemap {mat.name}\n");
                }
                else
                {
                    objSb.Append("usemtl default_material\n"); // Use default for triggers
                }

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
            // --- Individual Mesh Logic ---
            objSb.Append("# Exported from Unity by ObjExporter (Individual Meshes)\n");
            objSb.Append($"mtllib {fileName}.mtl\n\n");

            int vertexOffset = 0;

            foreach (MeshFilter mf in meshList)
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
                        objSb.Append("usemtl default_material\n"); // Use default for triggers
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

        // --- Write the files ---
        try
        {
            File.WriteAllText(objFullPath, objSb.ToString());
            File.WriteAllText(mtlFullPath, mtlSb.ToString());
            Debug.Log($"Successfully exported '{fileName}' ({(combineMeshes ? "Combined" : "Individual")}) to:\nOBJ: '{objFullPath}'\nMTL: '{mtlFullPath}'");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error exporting '{fileName}' to OBJ/MTL: {e.Message}");
        }
    }
}