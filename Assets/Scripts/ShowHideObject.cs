using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowHideObject : MonoBehaviour
{
    public bool hide=true;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Block"))        
            other.gameObject.SetActive(!hide);     
    }
}
