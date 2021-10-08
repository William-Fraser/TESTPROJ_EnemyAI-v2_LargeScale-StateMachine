using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trigger : MonoBehaviour
{
    [HideInInspector]
    public bool triggered = false;
    [HideInInspector]
    public Rigidbody triggeringObject = new Rigidbody();
    [HideInInspector]
    public GameObject newTarget;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<Rigidbody>())
        { 
            triggered = true;
            triggeringObject = other.gameObject.GetComponent<Rigidbody>();
            newTarget = other.gameObject;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        triggered = false;
    }
}
