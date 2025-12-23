using UnityEngine;

[ExecuteInEditMode] // Para que funcione también sin dar a Play (opcional)
public class ClickPositionDebugger : MonoBehaviour
{
    void Update()
    {
        // Solo si se hace clic izquierdo (0)
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log($"Punto de Impacto (Global): {hit.point}");
                Debug.Log($"Objeto: {hit.collider.name}");

                // Si quieres el centroide del objeto tocado:
                Debug.Log($"Centro del Objeto: {hit.transform.position}");

                // Si el objeto tiene malla y quieres el vértice más cercano (aproximado):
                // Esto requiere un MeshCollider
                if (hit.collider is MeshCollider)
                {
                    // Lógica más compleja para vértices exactos, pero el punto de impacto suele bastar.
                }
            }
        }
    }
}