using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Terrain;
using ModApi.Planet;
using ModApi.Planet.Events;
using ModApi.Settings.Core;
using ModApi.Settings.Core.Events;
using RootMotion;
using System;
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
    public Scatter[] activeScatters = new Scatter[0];

    // Stuff for capturing screen texture
    public GameObject cameraObject;
    EventHandler<QuadSphereFrameStateRecalculatedEventArgs> floatingOriginEvent;

    void OnEnable()
    {
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Initialize();
        }
        RegisterEvents();
    }
    int i = 0;
    public void Update()
    {
        if (!Game.Instance.SceneManager.InFlightScene) { return; }
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
    void FixedUpdate()
    {
        if (!ParallaxSettings.enableColliders)
        {
            return;
        }
        for (i = 0; i < Mod.ParallaxInstance.activeScatters.Length; i++)
        {
            if (Mod.ParallaxInstance.activeScatters[i].collisionLevel >= ParallaxSettings.collisionSizeThreshold)
            {
                Mod.ParallaxInstance.activeScatters[i].ProcessColliderData();
            }
        }
    }
    public Matrix4x4d RequestPlanetMatrixNow()
    {
        m.SetTRS(quadSphere.FramePosition, new Quaterniond(quadSphere.transform.parent.localRotation), Vector3.one);
        return m;
    }
    public void RegisterEvents()
    {
        if (CameraManagerScript.Instance == null) { return; }
        CameraManagerScript.Instance.CameraModeChanged += OnCameraModeChanged;
    }
    public void UnregisterEvents()
    {
        if (CameraManagerScript.Instance == null) { return; }
        CameraManagerScript.Instance.CameraModeChanged -= OnCameraModeChanged;
    }
    public void RegisterFloatingOriginEvent()
    {
        floatingOriginEvent = new EventHandler<QuadSphereFrameStateRecalculatedEventArgs>(OnFloatingOriginUpdated);
        quadSphere.FrameStateRecalculated += floatingOriginEvent;
    }
    public void UnregisterFloatingOriginEvent()
    {
        quadSphere.FrameStateRecalculated -= floatingOriginEvent;
    }
    void OnFloatingOriginUpdated(object sender, EventArgs e)
    {
        return;
        if (quadSphere == null || mainCamera == null) { return; }
        Update();
    }
    void OnCameraModeChanged(CameraMode newMode, CameraMode oldMode)
    {
        // Hacky way of getting the main camera (IT IS NOT CAMERA.MAIN THAT DOESN'T WORK IN SOME CASES) but only needed on camera mode changed
        mainCamera = CameraManagerScript.Instance.CurrentCameraController.CameraTransform.GetComponent<Camera>();
    }
    void OnDisable()
    {
        foreach (ScatterRenderer renderer in scatterRenderers)
        {
            renderer.Cleanup();
        }
        TextureLoader.UnloadAll();
        UnregisterEvents();
        UnregisterFloatingOriginEvent();
    }
    private void OnDestroy()
    {
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
