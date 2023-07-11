using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Terrain;
using ModApi.Planet;
using ModApi.Settings.Core;
using ModApi.Settings.Core.Events;
using RootMotion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScatterManager : MonoBehaviour         //Manages scatters on a planet
{
    public List<ScatterRenderer> scatterRenderers = new List<ScatterRenderer>();
    public delegate void QuadUpdate(Matrix4x4d mat);
    public QuadUpdate OnQuadUpdate;
    public QuadSphereScript quadSphere;
    public Matrix4x4d m = new Matrix4x4d();
    public IPlanet planet;
    // Avoid using Camera.main as this causes a search through all active cameras. Assign main camera reference here.
    public Camera mainCamera;

    public float splitDist;
    public float[] lodDistances;

    void OnEnable()
    {
        Debug.Log("Scatter manager enabled");
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Initialize();
        }
        RegisterEvents();
        //scatterRenderer?.Initialize();             //OnEnable can be called before the renderer is assigned, in which case the renderer is manually initialized
    }
    void Update()
    {
        m.SetTRS(quadSphere.FramePosition, new Quaterniond(quadSphere.transform.parent.localRotation), Vector3.one);    //Responsible for computing quadToWorld matrix
        if (OnQuadUpdate != null)
        {
            OnQuadUpdate(m);                         //Distance checks, recalculate matrix, etc
        }
        // Hacky as all hell but... seems to work?
        if (mainCamera.farClipPlane < 10000)
        {
            mainCamera.farClipPlane = 10000;
        }
    }
    public Matrix4x4d RequestPlanetMatrixNow()
    {
        m.SetTRS(quadSphere.FramePosition, new Quaterniond(quadSphere.transform.parent.localRotation), Vector3.one);
        return m;
    }
    public void RegisterEvents()
    {
        if (CameraManagerScript.Instance == null) { Debug.Log("Camera manager instance is null, not registering events"); return; }
        CameraManagerScript.Instance.CameraModeChanged += OnCameraModeChanged;
    }
    public void UnregisterEvents()
    {
        if (CameraManagerScript.Instance == null) { Debug.Log("Camera manager instance is null, not unregistering events"); return; }
        CameraManagerScript.Instance.CameraModeChanged -= OnCameraModeChanged;
    }
    void OnCameraModeChanged(CameraMode newMode, CameraMode oldMode)
    {
        Debug.Log("Camera Mode Changed from " + oldMode?.Name + " to " + newMode?.Name);
        // Hacky way of getting the main camera (IT IS NOT CAMERA.MAIN THAT DOESN'T WORK IN SOME CASES) but only needed on camera mode changed
        mainCamera = CameraManagerScript.Instance.CurrentCameraController.CameraTransform.GetComponent<Camera>();
    }
    void OnDisable()
    {
        Debug.Log("Scatter manager disabled");
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Cleanup();
        }
        TextureLoader.UnloadAll();
        UnregisterEvents();
    }
    private void OnDestroy()
    {
        Debug.Log("Scatter manager destroyed - Unregistering");
        foreach (QuadData quad in Mod.Instance.quadData.Values)     //I kinda wanna change this, it's a bit hacky
        {
            quad.Cleanup();
        }
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Cleanup();
        }
        TextureLoader.UnloadAll();
    }
}
