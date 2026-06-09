using System.Collections.Generic;
using UnityEngine;

public class BlockPooler : MonoBehaviour
{
    // Diccionario que guarda: <Nombre del Prefab, Lista de objetos desactivados>
    private Dictionary<string, List<GameObject>> poolDictionary = new Dictionary<string, List<GameObject>>();

    // Método para preparar el pool al inicio
    public void PreWarm(GameObject prefab, int amount)
    {
        if (prefab == null) return;

        string key = prefab.name;

        if (!poolDictionary.ContainsKey(key))
        {
            poolDictionary[key] = new List<GameObject>();
        }

        for (int i = 0; i < amount; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.name = key; // Mantenemos el nombre para identificarlo
            obj.SetActive(false);
            poolDictionary[key].Add(obj);
        }
    }

    // Método para obtener el objeto específico
    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string key = prefab.name;

        if (!poolDictionary.ContainsKey(key)) return null;

        // Buscamos uno libre en su lista correspondiente
        foreach (GameObject obj in poolDictionary[key])
        {
            if (!obj.activeInHierarchy)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                return obj;
            }
        }

        return null; // Opcionalmente aquí podrías instanciar uno extra si se acaba el pool
    }
}