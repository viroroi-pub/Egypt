using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeleteObject : MonoBehaviour
{
    public GeneratePyramid  generatePyramid; // Reference to the GeneratePyramid script

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Block"))
        {
            Destroy(other.gameObject);
            if (generatePyramid != null)
            {
                generatePyramid.DeletedBlocks++; // Call the method to decrease the block count
            }            
        }
    }
}
