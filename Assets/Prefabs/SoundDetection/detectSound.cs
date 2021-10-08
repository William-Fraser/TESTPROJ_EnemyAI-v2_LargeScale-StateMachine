using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class detectSound : MonoBehaviour
{
    [Header("Detection")]
    public GameObject detectedObject = null;
    public Ranges ranges;

    [HideInInspector]
    public bool objectDetected = false;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (ranges.autoDetect.triggered)
        {
            objectDetected = true;
            detectedObject = ranges.autoDetect.newTarget;
        }
        else if (ranges.crounchDetect.triggered)
        {
            if (ranges.crounchDetect.triggeringObject.velocity.magnitude >= 2)
            {
                objectDetected = true;
                detectedObject = ranges.crounchDetect.newTarget;
            }
        }
        else if (ranges.walkDetect.triggered)
        {
            if (ranges.walkDetect.triggeringObject.velocity.magnitude >= 4)
            {
                objectDetected = true;
                detectedObject = ranges.walkDetect.newTarget;
            }
        }
        else if (ranges.runDetect.triggered)
        {
            if (ranges.runDetect.triggeringObject.velocity.magnitude >= 8)
            {
                objectDetected = true;
                detectedObject = ranges.runDetect.newTarget;
            }
        }
        else
        {
            objectDetected = false;
        }
    }
}
[System.Serializable]
public class Ranges
{
    public Trigger autoDetect;
    public Trigger crounchDetect;
    public Trigger walkDetect;
    public Trigger runDetect;
}