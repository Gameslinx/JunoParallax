using Assets.Scripts;
using Assets.Scripts.Terrain;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScatterManager : MonoBehaviour         //Manages scatters on a planet
{
    public List<ScatterRenderer> scatterRenderers = new List<ScatterRenderer>();
    public delegate void QuadUpdate(Matrix4x4d mat);
    public QuadUpdate OnQuadUpdate;
    public QuadSphereScript quadSphere;
    public Matrix4x4d m = new Matrix4x4d();

    void OnEnable()
    {
        Debug.Log("Scatter manager enabled");
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Initialize();
        }
        //scatterRenderer?.Initialize();             //OnEnable can be called before the renderer is assigned, in which case the renderer is manually initialized
    }

    void Update()
    {
        m.SetTRS(quadSphere.FramePosition, new Quaterniond(quadSphere.transform.parent.localRotation), Vector3.one);    //Responsible for computing quadToWorld matrix
        if (OnQuadUpdate != null)
        {
            OnQuadUpdate(m);                         //Distance checks, recalculate matrix, etc
        }
    }

    void OnDisable()
    {
        Debug.Log("Scatter manager disabled");
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Cleanup();
        }
    }
    private void OnDestroy()
    {
        Debug.Log("Scatter manager destroyed - Unregistering");
        foreach (QuadData quad in Mod.Instance.quadData.Values)     //I kinda wanna change this, it's a bit hacky
        {
            quad.Cleanup();
        }
    }
}
