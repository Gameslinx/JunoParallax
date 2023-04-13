using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScatterManager : MonoBehaviour         //Manages scatters on a planet
{
    public ScatterRenderer scatterRenderer;
    public delegate void QuadUpdate();
    public QuadUpdate OnQuadUpdate;         
    void OnEnable()
    {
        Debug.Log("Scatter manager enabled");
    }

    void Update()
    {
        Debug.Log("OnQuadUpdate");
        OnQuadUpdate();                             //Distance checks, recalculate matrix, etc
    }

    void OnDisable()
    {
        Debug.Log("Scatter manager disabled");
    }
}
