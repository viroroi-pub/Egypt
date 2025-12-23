using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeleteObject : MonoBehaviour
{
    public GeneratePyramid  generatePyramid; // Reference to the GeneratePyramid script
    public GameObject CommonGameObject;
    public bool decreaseBlocks = true;
    public bool deleteObject = true;
    public bool mainRamp = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Block"))
        {
            if (CommonGameObject == null)
            {
                if (mainRamp)
                {
                    if (generatePyramid.lastRampMidPoint.y < other.transform.position.y)
                    {
                        generatePyramid.lastRampMidPoint = other.transform.position;
                    }
                    else
                    if (generatePyramid.lastRampMidPoint.y == other.transform.position.y && generatePyramid.lastRampMidPoint.magnitude < other.transform.position.magnitude)
                    {
                        generatePyramid.lastRampMidPoint = other.transform.position;
                    }
                }

                if (deleteObject)
                    Destroy(other.gameObject);
                else
                {
                    other.gameObject.SetActive(false);
                    generatePyramid.AddDetectedBlock(other.gameObject);
                }
                if (generatePyramid != null && decreaseBlocks)
                {
                    generatePyramid.DeletedBlocks++; // Call the method to decrease the block count
                }
            }
            else
            {
                if (FindAncestor(other.transform, CommonGameObject.transform))
                {
                    if (mainRamp)
                    {
                        if (generatePyramid.lastRampMidPoint.y < other.transform.position.y)
                        {
                            generatePyramid.lastRampMidPoint = other.transform.position;
                        }
                        else
                        if (generatePyramid.lastRampMidPoint.y == other.transform.position.y && generatePyramid.lastRampMidPoint.magnitude < other.transform.position.magnitude)
                        {
                            generatePyramid.lastRampMidPoint = other.transform.position;
                        }
                    }

                    if (deleteObject)
                        Destroy(other.gameObject);
                    else
                    { 
                        other.gameObject.SetActive(false);
                        generatePyramid.AddDetectedBlock(other.gameObject);
                    }
                    if (generatePyramid != null && decreaseBlocks)
                    {
                        generatePyramid.DeletedBlocks++; // Call the method to decrease the block count
                    }
                }
            }
        }
    }

    public static Transform FindAncestor(Transform transformA, Transform transformB)
    {
        if (transformA == null | transformB == null)
        {
            return null;
        }

        int depth = 0;
        while (transformA.parent != null)
        {
            if (transformA.parent == transformB)
            {
                return transformB;
            }
            transformA = transformA.parent;
            depth++;
        }        

        return null; // No se encontró un ancestro común
    }

    public static Transform FindCommonAncestor(Transform transformA, Transform transformB)
    {
        if (transformA == null | transformB == null)
        {
            return null;
        }

        int depthA = GetDepth(transformA);
        int depthB = GetDepth(transformB);

        // Normalizar profundidades
        while (depthA > depthB)
        {
            transformA = transformA.parent;
            depthA--;
        }

        while (depthB > depthA)
        {
            transformB = transformB.parent;
            depthB--;
        }

        // Ascender hasta encontrar el ancestro común
        while (transformA != null && transformB != null)
        {
            if (transformA == transformB)
            {
                return transformA;
            }
            transformA = transformA.parent;
            transformB = transformB.parent;
        }

        return null; // No se encontró un ancestro común
    }

    private static int GetDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            t = t.parent;
            depth++;
        }
        return depth;
    }
}
